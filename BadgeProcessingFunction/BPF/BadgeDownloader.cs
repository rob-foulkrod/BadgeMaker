using System;
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

        public BadgeDownloader(BlobServiceClient blobServiceClient, ILogger<BadgeDownloader> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        [Function(nameof(BadgeDownloader))]
        public async Task Run(
            [ServiceBusTrigger("%badgeQueueName%", Connection = "badgeservicebus")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)

        {
            var body = message.Body.ToString();
            _logger.LogInformation($"C# ServiceBus queue trigger function processed message: {body}");

            MessageContent content = JsonConvert.DeserializeObject<MessageContent>(body);


            if (content == null)
            {
                _logger.LogError("Failed to deserialize message body");
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Failed to deserialize message body");
                throw new InvalidOperationException("Failed to deserialize message body");
            }
            else if (string.IsNullOrEmpty(content.Url))
            {
                _logger.LogError("Message does not contain a valid image URL");
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Message does not contain a valid image URL");
                throw new InvalidOperationException("Message does not contain a valid image URL");
            }

            var httpClient = new System.Net.Http.HttpClient();
            var results = await httpClient.GetAsync(content.Url);

            if (results.IsSuccessStatusCode)
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient("badges");
                containerClient.CreateIfNotExists();
                var blobName = $"badge-{Guid.NewGuid()}.png";
                var blobClient = containerClient.GetBlobClient(blobName);
                var stream = await results.Content.ReadAsStreamAsync();
                await blobClient.UploadAsync(stream);

                blobClient.SetMetadata(new System.Collections.Generic.Dictionary<string, string>
                {
                    { "approvaltimestamp", content.ApprovalTimeStamp },
                    { "userprompt", content.UserPrompt }
                });
            }
            else
            {
                _logger.LogError($"Failed to download image from {content.Url}");
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: $"Failed to download image from {content.Url}\")");
                throw new InvalidOperationException($"Failed to download image from {content.Url}");
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

        public override bool Equals(object obj)
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
