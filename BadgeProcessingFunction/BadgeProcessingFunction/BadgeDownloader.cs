using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BadgeProcessingFunction
{
    public class BadgeProcessingFunctionClass
    {
        [FunctionName("BadgeDownloader")]
        public async Task Run(
            [ServiceBusTrigger("badgeapproved", Connection = "badgeservicebus")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,

            [Blob("badges/{rand-guid}.png", FileAccess.Write, Connection = "AzureWebJobsStorage")]
            BlobClient blobClient,
            ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {message.Body.ToString()}");

            var content = JsonConvert.DeserializeObject<MessageContent>(message.Body.ToString());
            if (content == null)
            {
                log.LogError("Failed to deserialize message body");
                await messageActions.DeadLetterMessageAsync(message, "Failed to deserialize message body");
                throw new InvalidOperationException("Failed to deserialize message body");
            }
            else if (string.IsNullOrEmpty(content.Url))
            {
                log.LogError("Message does not contain a valid image URL");
                await messageActions.DeadLetterMessageAsync(message, "Message does not contain a valid image URL");
                throw new InvalidOperationException("Message does not contain a valid image URL");
            }

            var httpClient = new System.Net.Http.HttpClient();
            var results = await httpClient.GetAsync(content.Url);

            if (results.IsSuccessStatusCode)
            {
                var stream = await results.Content.ReadAsStreamAsync();
                await blobClient.UploadAsync(stream);

                blobClient.SetMetadata(new System.Collections.Generic.Dictionary<string, string>
                {
                    { "approval-timestamp", content.ApprovalTimeStamp },
                    { "user-prompt", content.UserPrompt }
                });
            }
            else
            {
                log.LogError($"Failed to download image from {content.Url}");
                await messageActions.DeadLetterMessageAsync(message, $"Failed to download image from {content.Url}\")");
                throw new InvalidOperationException($"Failed to download image from {content.Url}");
            }
            await messageActions.CompleteMessageAsync(message);
        }
    }

    internal class MessageContent
    {
        public string Url { get; }
        public string ApprovalTimeStamp { get; }
        public string UserPrompt { get; }

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
