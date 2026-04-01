using Azure;
using Azure.AI.OpenAI;
using Azure.Messaging.ServiceBus;
using BadgeMaker.Components.Interfaces;
using OpenAI.Images;
using System.Text.Json;

namespace BadgeMaker.Components.Models;

public class BadgeGeneratorViewModel
{

    private readonly IOpenAIService openAIService;
    private readonly IServiceBusService serviceBusService;
    private readonly IBlobStorageService blobStorageService;

    public string UserPrompt { get; set; } = "";
    public string? ImageUri { get; set; }
    public string Message { get; set; } = "";
    public Func<Task>? OnStateChanged { get; set; }
    public string SelectedQuality { get; set; } = "low";

    public static readonly (string Label, string Quality)[] QualityOptions =
    [
        ("Fast (low quality)", "low"),
        ("Standard (medium quality)", "medium"),
        ("High (high quality)", "high"),
    ];

    private bool _isLoading;

    public BadgeGeneratorViewModel(IOpenAIService openAIService, IServiceBusService serviceBusService, IBlobStorageService blobStorageService)
    {
        this.openAIService = openAIService;
        this.serviceBusService = serviceBusService;
        this.blobStorageService = blobStorageService;
    }

    public bool IsLoading => _isLoading;

    private async Task UpdateMessage(string message, bool loading = false)
    {
        Message = message;
        _isLoading = loading;
        if (OnStateChanged != null)
            await OnStateChanged.Invoke();
    }

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
            await UpdateMessage("Generating image...", loading: true);
        }

        try
        {

            ImageGenerationOptions options = new ImageGenerationOptions() { 
                Size = GeneratedImageSize.W1024xH1024,
                Quality = new GeneratedImageQuality(SelectedQuality)
            };

            var imageData = await openAIService.GenerateImageAsync(UserPrompt, options);

            await UpdateMessage("Uploading to storage...", loading: true);
            ImageUri = await blobStorageService.UploadBadgeImageAsync(imageData);
            await UpdateMessage("");
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            await UpdateMessage("You have made too many requests. Please wait a while before trying again.");
        }
        catch (RequestFailedException ex)
        {
            await UpdateMessage($"A service error occurred (HTTP {ex.Status}): {ex.Message}");
        }
        catch (Exception ex)
        {
            await UpdateMessage($"An error occurred: {ex.Message}");
        }
    }

    public async Task ApproveImage()
    {

        if (ImageUri == null)
        {
            Message = "No Image to Approve.";
            return;
        }

        var imageUrl = ImageUri;
        await UpdateMessage("Approving badge...", loading: true);

        var messageBody = new
        {
            url = imageUrl,
            approvalTimeStamp = DateTime.UtcNow.ToString("o"),
            userPrompt = UserPrompt
        };

        string jsonMessage = JsonSerializer.Serialize(messageBody);

        await serviceBusService.SendMessageAsync(jsonMessage);

        ImageUri = null;
        await UpdateMessage("Image approved and message sent to Service Bus.");

    }
}
