using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BPF2
{
    public class BadgeDownloader
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BadgeDownloader> _logger;
        private static readonly ActivitySource ActivitySource = new("BadgeMaker.Function");

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
            var traceParent = message.ApplicationProperties.TryGetValue("traceparent", out var traceParentObj)
                ? traceParentObj?.ToString()
                : null;
            var traceState = message.ApplicationProperties.TryGetValue("tracestate", out var traceStateObj)
                ? traceStateObj?.ToString()
                : null;

            using var activity = !string.IsNullOrWhiteSpace(traceParent)
                ? ActivitySource.StartActivity("ProcessBadgeApproval", ActivityKind.Consumer, traceParent)
                : ActivitySource.StartActivity("ProcessBadgeApproval", ActivityKind.Consumer);

            activity?.SetTag("servicebus.message.id", message.MessageId);
            activity?.SetTag("servicebus.delivery_count", message.DeliveryCount);

            string body = message.Body.ToString() ?? string.Empty;
            activity?.SetTag("servicebus.message_size_bytes", body?.Length ?? 0);

            _logger.LogInformation("Processing badge approval message");

            if (string.IsNullOrWhiteSpace(body))
            {
                const string emptyBodyReason = "Message body was empty";
                _logger.LogError(emptyBodyReason);
                activity?.SetStatus(ActivityStatusCode.Error, emptyBodyReason);
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: emptyBodyReason);
                throw new InvalidOperationException(emptyBodyReason);
            }

            MessageContent? content = JsonConvert.DeserializeObject<MessageContent>(body);

            if (content == null)
            {
                _logger.LogError("Failed to deserialize message body");
                activity?.SetStatus(ActivityStatusCode.Error, "Deserialization failed");
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Failed to deserialize message body");
                throw new InvalidOperationException("Failed to deserialize message body");
            }
            else if (string.IsNullOrEmpty(content.Url))
            {
                _logger.LogError("Message does not contain a valid image URL");
                activity?.SetStatus(ActivityStatusCode.Error, "Missing image URL");
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Message does not contain a valid image URL");
                throw new InvalidOperationException("Message does not contain a valid image URL");
            }

            activity?.SetTag("badge.prompt", content.UserPrompt);
            activity?.SetTag("badge.approval.timestamp", content.ApprovalTimeStamp);
            if (!string.IsNullOrWhiteSpace(content.TraceId))
            {
                activity?.SetTag("trace.id", content.TraceId);
            }

            using var httpClient = new HttpClient();
            var stopwatch = Stopwatch.StartNew();
            var results = await httpClient.GetAsync(content.Url);
            stopwatch.Stop();
            activity?.SetTag("http.get.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("http.status_code", (int)results.StatusCode);

            if (results.IsSuccessStatusCode)
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient("badges");
                await containerClient.CreateIfNotExistsAsync();
                var blobName = $"badge-{Guid.NewGuid()}.png";
                var blobClient = containerClient.GetBlobClient(blobName);
                var stream = await results.Content.ReadAsStreamAsync();

                var metadata = new Dictionary<string, string>
                {
                    { "approvaltimestamp", content.ApprovalTimeStamp },
                    { "userprompt", content.UserPrompt }
                };

                if (!string.IsNullOrWhiteSpace(content.TraceId))
                {
                    metadata["traceid"] = content.TraceId!;
                }
                if (!string.IsNullOrWhiteSpace(content.SpanId))
                {
                    metadata["spanid"] = content.SpanId!;
                }

                await blobClient.UploadAsync(stream, new BlobUploadOptions { Metadata = metadata });

                activity?.SetTag("blob.name", blobName);
                activity?.SetTag("processing.success", true);
                activity?.AddEvent(new ActivityEvent("BadgeStored"));
                _logger.LogInformation("Badge stored as {BlobName}", blobName);
            }
            else
            {
                var messageText = $"Failed to download image from {content.Url}";
                _logger.LogError(messageText);
                activity?.SetStatus(ActivityStatusCode.Error, messageText);
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: messageText);
                throw new InvalidOperationException(messageText);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
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

        [JsonProperty("traceId")]
        public string? TraceId { get; }

        [JsonProperty("spanId")]
        public string? SpanId { get; }

        [JsonConstructor]
        public MessageContent(string url, string approvalTimeStamp, string userPrompt, string? traceId = null, string? spanId = null)
        {
            Url = url;
            ApprovalTimeStamp = approvalTimeStamp;
            UserPrompt = userPrompt;
            TraceId = traceId;
            SpanId = spanId;
        }

    public override bool Equals(object? obj)
        {
            return obj is MessageContent other &&
                   Url == other.Url &&
                   ApprovalTimeStamp == other.ApprovalTimeStamp &&
                   UserPrompt == other.UserPrompt &&
                   TraceId == other.TraceId &&
                   SpanId == other.SpanId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Url, ApprovalTimeStamp, UserPrompt, TraceId, SpanId);
        }
    }
}
