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

                    await blobClient.SetMetadataAsync(new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "approvaltimestamp", content.ApprovalTimeStamp },
                        { "userprompt", content.UserPrompt }
                    });

                    _logger.LogInformation($"Successfully processed badge and saved as {blobName}");
                    
                    // Complete the message to prevent reprocessing
                    await messageActions.CompleteMessageAsync(message);
                }
                else
                {
                    _logger.LogError($"Failed to download image from {content.Url}. Status: {response.StatusCode}");
                    await messageActions.DeadLetterMessageAsync(message, deadLetterReason: $"Failed to download image from {content.Url}. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Service Bus message");
                // Let the message retry by not completing it, unless it's a permanent error
                throw;
            }
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
