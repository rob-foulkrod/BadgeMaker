using Azure;
using Azure.AI.OpenAI;
using Azure.Messaging.ServiceBus;
using BadgeMaker.Components.Interfaces;
using System.Text.Json;

namespace BadgeMaker.Components.Models;

public class BadgeGeneratorViewModel
{

    private readonly IOpenAIService openAIService;
    private readonly IServiceBusService serviceBusService;

    public string UserPrompt { get; set; } = "";
    public string? ImageUri { get; set; }
    public string Message { get; set; } = "";

    private const string loadingMessage = "Loading...";

    public BadgeGeneratorViewModel(IOpenAIService openAIService, IServiceBusService serviceBusService)
    {
        this.openAIService = openAIService;
        this.serviceBusService = serviceBusService;
    }

    public bool IsLoading => Message == loadingMessage;

    public bool CanApprove => ImageUri != null && serviceBusService.IsConfigured;

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
            Message = loadingMessage;
        }

        try
        {

            ImageGenerationOptions options = new ImageGenerationOptions()
            {
                Prompt = "We are creating a collection of badges for a training course. Every day needs it own badge. I will provide a description of the badge and you will create a badge image. Every badge will be round and contain an image and the badge number. The badges will be created in Pixar animation. --- " + UserPrompt,
                Size = ImageSize.Size1024x1024,
                ImageCount = 1
            };

            ImageUri = await openAIService.GenerateImageUriAsync(UserPrompt, options);
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

    public async Task ApproveImage()
    {

        if (ImageUri == null)
        {
            Message = "No Image to Approve.";
            return;
        }

        Message = loadingMessage;

        var messageBody = new
        {
            url = ImageUri, // Assuming the first image is the one to be approved
            approvalTimeStamp = DateTime.UtcNow.ToString("o"), // ISO 8601 format
            userPrompt = UserPrompt
        };

        string jsonMessage = JsonSerializer.Serialize(messageBody);

        // create a Service Bus message

        await serviceBusService.SendMessageAsync(jsonMessage);

        Message = "Image approved and message sent to Service Bus.";
        ImageUri = null;

    }
}
