using System;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BPF2
{
    public class BadgeDownloader
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BadgeDownloader> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public BadgeDownloader(BlobServiceClient blobServiceClient, ILogger<BadgeDownloader> logger, IHttpClientFactory httpClientFactory)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [Function(nameof(BadgeDownloader))]
        public async Task Run(
            [ServiceBusTrigger("%badgeQueueName%", Connection = "badgeservicebus")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            var body = message.Body.ToString();
            _logger.LogInformation($"C# ServiceBus queue trigger function processed message: {body}");

            try
            {
                MessageContent? content = JsonConvert.DeserializeObject<MessageContent>(body);

                if (content == null)
                {
                    _logger.LogError("Failed to deserialize message body");
                    await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Failed to deserialize message body");
                    return;
                }

                if (string.IsNullOrEmpty(content.Url))
                {
                    _logger.LogError("Message does not contain a valid image URL");
                    await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Message does not contain a valid image URL");
                    return;
                }

                var httpClient = _httpClientFactory.CreateClient();
                using var response = await httpClient.GetAsync(content.Url);

                if (response.IsSuccessStatusCode)
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient("badges");
                    var blobName = $"badge-{Guid.NewGuid()}.png";
                    var blobClient = containerClient.GetBlobClient(blobName);
                    
                    using var stream = await response.Content.ReadAsStreamAsync();
                    await blobClient.UploadAsync(stream, overwrite: true);

                    // Sanitize metadata values to ensure they're valid for Azure Blob Storage
                    // Metadata must be ASCII and cannot contain control characters or newlines
                    var sanitizedPrompt = SanitizeMetadataValue(content.UserPrompt);
                    var sanitizedTimestamp = SanitizeMetadataValue(content.ApprovalTimeStamp);

                    try
                    {
                        await blobClient.SetMetadataAsync(new System.Collections.Generic.Dictionary<string, string>
                        {
                            { "approvaltimestamp", sanitizedTimestamp },
                            { "userprompt", sanitizedPrompt }
                        });
                    }
                    catch (Exception metadataEx)
                    {
                        // If metadata setting fails, log it but don't fail the entire operation
                        // The badge image is already uploaded successfully
                        _logger.LogWarning(metadataEx, $"Failed to set metadata for {blobName}, but image was uploaded successfully");
                    }

                    _logger.LogInformation($"Successfully processed badge and saved as {blobName}");
                    
                    // Complete the message to prevent reprocessing
                    await messageActions.CompleteMessageAsync(message);
                }
                else
                {
                    _logger.LogError($"Failed to download image from {content.Url}. Status: {response.StatusCode}");
                    // Dead-letter the message (it will NOT be reprocessed after this)
                    await messageActions.DeadLetterMessageAsync(message, deadLetterReason: $"Failed to download image from {content.Url}. Status: {response.StatusCode}");
                    return; // CRITICAL: Exit after dead-lettering to prevent fall-through to catch block
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Service Bus message");
                
                // Try to dead-letter the message for permanent errors
                try
                {
                    await messageActions.DeadLetterMessageAsync(message, 
                        deadLetterReason: "Processing failed with exception", 
                        deadLetterErrorDescription: ex.Message);
                    _logger.LogInformation("Message dead-lettered due to processing exception");
                }
                catch (Exception deadLetterEx)
                {
                    _logger.LogError(deadLetterEx, "Failed to dead-letter message, it will be retried");
                    // If we can't dead-letter, let it retry by throwing
                    throw;
                }
            }
        }

        /// <summary>
        /// Sanitizes a string value to be safe for Azure Blob Storage metadata.
        /// Metadata values must be ASCII and cannot contain control characters.
        /// </summary>
        private static string SanitizeMetadataValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Remove or replace characters that are not valid in Azure Blob metadata
            // Keep only printable ASCII characters (32-126) and replace others with space
            var sanitized = new System.Text.StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c >= 32 && c <= 126)
                {
                    sanitized.Append(c);
                }
                else
                {
                    sanitized.Append(' '); // Replace invalid characters with space
                }
            }

            // Trim and limit length (metadata values have a max length of 8KB)
            var result = sanitized.ToString().Trim();
            return result.Length > 8000 ? result.Substring(0, 8000) : result;
        }
    }

    internal class MessageContent
    {
        [JsonProperty("url")]
        public string Url { get; }

        [JsonProperty("approvalTimeStamp")]
        public string ApprovalTimeStamp { get; }

        [JsonProperty("userPrompt")]
        public string UserPrompt { get; }

        [JsonConstructor]
        public MessageContent(string url, string approvalTimeStamp, string userPrompt)
        {
            Url = url;
            ApprovalTimeStamp = approvalTimeStamp;
            UserPrompt = userPrompt;
        }

        public override bool Equals(object? obj)
        {
            return obj is MessageContent other &&
                   Url == other.Url &&
                   ApprovalTimeStamp == other.ApprovalTimeStamp &&
                   UserPrompt == other.UserPrompt;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Url, ApprovalTimeStamp, UserPrompt);
        }
    }
}
