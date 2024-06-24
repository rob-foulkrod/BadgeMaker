using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BadgeProcessingFunction
{
    public class BadgeProcessingFunctionClass
    {
        [FunctionName("ListOrchestrationsClient")]
        public static async Task<IActionResult> Run(
                            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orchestrations")] HttpRequest req,
                            [DurableClient] IDurableOrchestrationClient client,
                            ILogger log)
        {
            log.LogInformation("Received request to list current orchestrations");

            // Define the query condition
            var condition = new OrchestrationStatusQueryCondition
            {
                RuntimeStatus = new List<OrchestrationRuntimeStatus>
                {
                    OrchestrationRuntimeStatus.Running,
                    OrchestrationRuntimeStatus.Pending,
                    OrchestrationRuntimeStatus.ContinuedAsNew
                }
            };

            // Query the orchestration instances
            var statusList = await client.ListInstancesAsync(condition, CancellationToken.None);

            // Return the list of orchestration instances
            return new OkObjectResult(statusList.DurableOrchestrationState);
        }

        [FunctionName("GetOrchestrationHistory")]
        public static async Task<IActionResult> GetOrchestrationHistory(
                    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orchestrations/{instanceId}/history")] HttpRequest req,
                    [DurableClient] IDurableOrchestrationClient client,
                    string instanceId,
                    ILogger log)
        {
            log.LogInformation($"Received request to get history for instance ID: {instanceId}");

            // Get the status of the orchestration instance
            var status = await client.GetStatusAsync(instanceId, showHistory: true, showHistoryOutput: true);

            if (status == null)
            {
                log.LogError($"No orchestration instance found with ID: {instanceId}");
                return new NotFoundResult();
            }

            // Return the history of the orchestration instance
            return new OkObjectResult(status.History);
        }

        [FunctionName("BadgeDownloaderWorkflow")]
        public async Task Starter(
            [ServiceBusTrigger("badgeapproved", Connection = "badgeservicebus")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            [DurableClient] IDurableOrchestrationClient starter,
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

            await starter.StartNewAsync("BadgeValidationOrchestrator", content);
        }


        [FunctionName("GetPromptForValidation")]
        public static async Task<IActionResult> GetPromptForValidation(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orchestrations/{instanceId}/prompt")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            string instanceId,
            ILogger log)
        {
            log.LogInformation($"Received request to get prompt for instance ID: {instanceId}");

            // Get the status of the orchestration instance
            var status = await client.GetStatusAsync(instanceId);

            if (status == null)
            {
                log.LogError($"No orchestration instance found with ID: {instanceId}");
                return new NotFoundResult();
            }

            // Extract the input data (MessageContent)
            var content = status.Input.ToObject<MessageContent>();

            if (content == null)
            {
                log.LogError($"No input data found for instance ID: {instanceId}");
                return new BadRequestObjectResult("No input data found for the specified instance ID");
            }

            // Return the prompt
            return new OkObjectResult(content.UserPrompt);
        }

        [FunctionName("GetUrlForValidation")]
        public static async Task<IActionResult> GetUrlForValidation(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orchestrations/{instanceId}/url")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client,
            string instanceId,
            ILogger log)
        {
            log.LogInformation($"Received request to get URL for instance ID: {instanceId}");

            // Get the status of the orchestration instance
            var status = await client.GetStatusAsync(instanceId);

            if (status == null)
            {
                log.LogError($"No orchestration instance found with ID: {instanceId}");
                return new NotFoundResult();
            }

            // Extract the input data (MessageContent)
            var content = status.Input.ToObject<MessageContent>();

            if (content == null)
            {
                log.LogError($"No input data found for instance ID: {instanceId}");
                return new BadRequestObjectResult("No input data found for the specified instance ID");
            }

            // Return the URL
            return new OkObjectResult(content.Url);
        }

        [FunctionName("BadgeValidationOrchestrator")]
        public static async Task<ValidationWorkflow> Orchestrate(
                              [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new ValidationWorkflow();
            var content = context.GetInput<MessageContent>();
            context.SetCustomStatus("ValidatePrompt");

            // Wait for the ValidatePrompt external event
            var promptResponse = await context.WaitForExternalEvent<ValidationResponse>("ValidatePrompt");

            // Add the validation result to the workflow
            outputs.Add(new ValidationSteps
            {
                StepName = "ValidatePrompt",
                Approved = promptResponse.Approved,
                Description = promptResponse.Description,
                DateTime = DateTime.UtcNow
            });

            if (!promptResponse.Approved)
            {
                context.SetCustomStatus("Prompt was invlaid. Rejecting");
                return outputs;
            }

            context.SetCustomStatus("ValidateImage");

            var imageResponse = await context.WaitForExternalEvent<ValidationResponse>("ValidateImage");

            // Add the validation result to the workflow
            outputs.Add(new ValidationSteps
            {
                StepName = "ValidateImage",
                Approved = imageResponse.Approved,
                Description = imageResponse.Description,
                DateTime = DateTime.UtcNow
            });
            if (!imageResponse.Approved)
            {
                context.SetCustomStatus("Image was invlaid. Rejecting");
                return outputs;
            }

            context.SetCustomStatus("DownloadingImage");
            // Call the DownloadBlobActivity function
            var blobUri = await context.CallActivityAsync<string>("DownloadBlobActivity", (content, context.InstanceId));

            // Add a validation step to the workflow
            outputs.Add(new ValidationSteps
            {
                StepName = "DownloadBlob",
                Approved = true,
                Description = $"Blob downloaded to {blobUri}",
                DateTime = DateTime.UtcNow
            });


            return outputs;
        }


        [FunctionName("ValidatePrompt")]
        public static async Task<IActionResult> ValidatePrompt(
                        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orchestrations/{instanceId}/validatePrompt")] HttpRequest req,
                        [DurableClient] IDurableOrchestrationClient client,
                        string instanceId,
                        ILogger log)
        {
            log.LogInformation($"Received request to validate prompt for instance ID: {instanceId}");

            // Read the request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var validationResponse = JsonConvert.DeserializeObject<ValidationResponse>(requestBody);

            if (validationResponse == null || validationResponse.InstanceId != instanceId)
            {
                log.LogError("Invalid request body or instance ID mismatch");
                return new BadRequestObjectResult("Invalid request body or instance ID mismatch");
            }

            // Raise the external event to the orchestration instance
            await client.RaiseEventAsync(instanceId, "ValidatePrompt", validationResponse);

            return new OkResult();
        }

        [FunctionName("ValidateImage")]
        public static async Task<IActionResult> ValidateImage(
                        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orchestrations/{instanceId}/validateImage")] HttpRequest req,
                        [DurableClient] IDurableOrchestrationClient client,
                        string instanceId,
                        ILogger log)
        {
            log.LogInformation($"Received request to validate image for instance ID: {instanceId}");

            // Read the request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var validationResponse = JsonConvert.DeserializeObject<ValidationResponse>(requestBody);

            if (validationResponse == null || validationResponse.InstanceId != instanceId)
            {
                log.LogError("Invalid request body or instance ID mismatch");
                return new BadRequestObjectResult("Invalid request body or instance ID mismatch");
            }

            // Raise the external event to the orchestration instance
            await client.RaiseEventAsync(instanceId, "ValidateImage", validationResponse);

            return new OkResult();
        }


        [FunctionName("DownloadBlobActivity")]
        public static async Task<string> DownloadBlobActivity(
            [ActivityTrigger] (MessageContent content, string blobUri) input,
            ILogger log)
        {
            var (content, blobUri) = input;
            var httpClient = new System.Net.Http.HttpClient();

            var results = await httpClient.GetAsync(content.Url);

            if (results.IsSuccessStatusCode)
            {
                var stream = await results.Content.ReadAsStreamAsync();
                // get Blob Service Client from the connection string
                var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                // get a reference to the container
                var containerName = Environment.GetEnvironmentVariable("BlobContainerName");
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                // get a reference to the blob
                var blobClient = containerClient.GetBlobClient($"{blobUri}.png");

                await blobClient.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" },
                    Metadata = new Dictionary<string, string>
                    {
                        { "approvalTimestamp", content.ApprovalTimeStamp },
                        { "userPrompt", content.UserPrompt }
                    }
                });

                return blobClient.Uri.ToString();
            }
            else
            {
                log.LogError($"Failed to download image from {content.Url}");
                throw new InvalidOperationException($"Failed to download image from {content.Url}");
            }
        }



    }

    public class MessageContent
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

    public class ValidationWorkflow : List<ValidationSteps>
    {


    }

    public class ValidationSteps
    {
        public string StepName { get; set; }

        public bool Approved { get; set; }

        public string Description { get; set; }

        public DateTime DateTime { get; set; } = DateTime.UtcNow;

    }

    public class ValidationResponse
    {
        public string InstanceId { get; set; }
        public bool Approved { get; set; }

        public string Description { get; set; }

    }

}

