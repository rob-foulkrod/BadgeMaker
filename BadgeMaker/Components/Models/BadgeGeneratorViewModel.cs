using Azure;
using Azure.AI.OpenAI;
using Azure.Messaging.ServiceBus;
using BadgeMaker.Components.Models;
using System.Text.Json;

public class BadgeGeneratorViewModel
{
    private readonly OpenAIConfig openAIconfig;
    private readonly ServiceBusConfig serviceBusConfig;

    public string UserPrompt { get; set; } = "";
    public string? ImageUri { get; private set; }
    public string Message { get; set; } = "";

    private const string loadingMessage = "Loading...";

    public BadgeGeneratorViewModel(OpenAIConfig openAIconfig, ServiceBusConfig serviceBusConfig)
    {
        this.openAIconfig = openAIconfig;
        this.serviceBusConfig = serviceBusConfig;
    }

    public async Task GenerateBadge()
    {
        ImageUri = null;

        if (string.IsNullOrWhiteSpace(UserPrompt))
        {
            Message = "Please enter a prompt";
            return;
        }
        else
        {
            Message = "Loading...";
        }

        try
        {
            OpenAIClient client = new OpenAIClient(new Uri(openAIconfig.endpoint), new AzureKeyCredential(openAIconfig.apiKey));
            var imageGenerations = await client.GetImageGenerationsAsync(new ImageGenerationOptions()
            {
                DeploymentName = openAIconfig.deployment,
                Prompt = "Description --- " + UserPrompt,
                Size = ImageSize.Size1024x1024,
                ImageCount = 1
            });

            ImageUri = imageGenerations.Value.Data.Select(data => data.Url.ToString()).First();
            Message = "";
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            Message = "You have made too many requests. Please wait a while before trying again.";
        }
        catch (Exception ex)
        {
            Message = $"An error occurred: {ex.Message}";
        }
    }

    private async Task ApproveImage()
    {

        if (ImageUri == null)
        {
            return;
        }

        Message = loadingMessage;

        var serviceBusClient = new ServiceBusClient(serviceBusConfig.connectionString);
        var sender = serviceBusClient.CreateSender(serviceBusConfig.queueName);

        var messageBody = new
        {
            url = ImageUri, // Assuming the first image is the one to be approved
            approvalTimeStamp = DateTime.UtcNow.ToString("o"), // ISO 8601 format
            userPrompt = UserPrompt
        };

        string jsonMessage = JsonSerializer.Serialize(messageBody);

        // create a Service Bus message
        ServiceBusMessage messageToSend = new ServiceBusMessage(jsonMessage);

        // send the message
        await sender.SendMessageAsync(messageToSend);

        Message = "Image approved and message sent to Service Bus.";
        ImageUri = null;

    }
}
