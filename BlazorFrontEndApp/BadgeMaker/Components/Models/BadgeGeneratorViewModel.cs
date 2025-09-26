using Azure;
using Azure.AI.OpenAI;
using Azure.Messaging.ServiceBus;
using BadgeMaker.Components.Interfaces;
using OpenAI.Images;
using System.Diagnostics;

namespace BadgeMaker.Components.Models;

public class BadgeGeneratorViewModel
{

    private readonly IOpenAIService openAIService;
    private readonly IServiceBusService serviceBusService;
    private static readonly ActivitySource ActivitySource = new("BadgeMaker.UI");

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
        using var activity = ActivitySource.StartActivity("GenerateBadge");
        activity?.SetTag("badge.prompt.length", UserPrompt.Length);

        ImageUri = null;

        if (string.IsNullOrWhiteSpace(UserPrompt))
        {
            Message = "Please enter a prompt";
            activity?.SetTag("badge.prompt.valid", false);
            activity?.SetStatus(ActivityStatusCode.Error, "Prompt was empty");
            return;
        }
        else
        {
            Message = loadingMessage;
            activity?.SetTag("badge.prompt.valid", true);
        }

        try
        {

            ImageGenerationOptions options = new ImageGenerationOptions() { 
                Size = GeneratedImageSize.W1024xH1024,
                Quality = GeneratedImageQuality.High,
                ResponseFormat = GeneratedImageFormat.Uri
            };


            ImageUri = await openAIService.GenerateImageUriAsync(UserPrompt, options);
            Message = "";
            activity?.SetTag("badge.generation.success", true);
            activity?.AddEvent(new ActivityEvent("BadgeGenerated"));
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            Message = "You have made too many requests. Please wait a while before trying again.";
            activity?.SetTag("badge.generation.success", false);
            activity?.SetTag("exception.type", ex.GetType().Name);
            activity?.SetTag("exception.status", ex.Status);
            activity?.SetStatus(ActivityStatusCode.Error, "Rate limited");
            activity?.AddEvent(new ActivityEvent("BadgeGenerationThrottled"));
        }
        catch (Exception ex)
        {
            Message = $"An error occurred: {ex.Message}";
            activity?.SetTag("badge.generation.success", false);
            activity?.SetTag("exception.type", ex.GetType().Name);
            activity?.SetTag("exception.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("BadgeGenerationFailed"));
        }
    }

    public async Task ApproveImage()
    {

        using var activity = ActivitySource.StartActivity("ApproveBadge");
        activity?.SetTag("badge.prompt.length", UserPrompt.Length);

        if (ImageUri == null)
        {
            Message = "No Image to Approve.";
            activity?.SetStatus(ActivityStatusCode.Error, "No image available");
            activity?.AddEvent(new ActivityEvent("ApproveWithoutImage"));
            return;
        }

        Message = loadingMessage;

        var approvalTimestamp = DateTime.UtcNow;
        activity?.SetTag("badge.approval.timestamp", approvalTimestamp.ToString("o"));

        await serviceBusService.SendMessageAsync(ImageUri, UserPrompt, approvalTimestamp);

        Message = "Image approved and message sent to Service Bus.";
        ImageUri = null;
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.AddEvent(new ActivityEvent("BadgeApprovalRequested"));

    }
}
