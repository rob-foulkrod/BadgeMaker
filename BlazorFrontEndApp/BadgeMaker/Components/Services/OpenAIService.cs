using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using BadgeMaker.Components.Interfaces;
using BadgeMaker.Components.Models;
using OpenAI;
using OpenAI.Images;
using System.Diagnostics;

namespace BadgeMaker.Components.Services;

public class OpenAIService : IOpenAIService
{
    private readonly OpenAIConfig openAIConfig;
    private static readonly ActivitySource ActivitySource = new("BadgeMaker.OpenAIService");

    public OpenAIService(OpenAIConfig openAIConfig)
    {
        this.openAIConfig = openAIConfig;
    }

    public async Task<string> GenerateImageUriAsync(string prompt, ImageGenerationOptions options)
    {
        using var activity = ActivitySource.StartActivity("GenerateBadgeImage");
        activity?.SetTag("badge.prompt.length", prompt.Length);
        activity?.SetTag("openai.deployment", openAIConfig.deployment);

        var basePrompt = """
                Create a circular badge celebrating the completion of a specific learning module. 
                The badge should include a central image that reflects the particular topic of the 
                module. The design should feature a vibrant color scheme with a primary color that 
                stands out (e.g., a rich blue, energetic red, or lively green) and complementary 
                accent colors. The outer edge of the badge should have a decorative border, such 
                as a subtle pattern or elegant trim, to give it a polished look. Ensure the overall 
                style is visually appealing and cohesive, suitable for a collection of badges. The 
                central image should clearly represent the unique topic of this individual module.

                This badge will be awarded to learners who successfully complete the module on 
                
        """;


        try
        {
            var credential = new DefaultAzureCredential();

            AzureOpenAIClient client = new AzureOpenAIClient(new Uri(openAIConfig.endpoint), credential);

            var stopwatch = Stopwatch.StartNew();
            var imageGenerations = await client.GetImageClient(openAIConfig.deployment)
                .GenerateImageAsync(basePrompt + prompt, options);
            stopwatch.Stop();

            var imageUri = imageGenerations.Value.ImageUri.ToString();

            activity?.SetTag("badge.generation.success", true);
            activity?.SetTag("badge.generation.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("badge.image.uri_length", imageUri.Length);
            activity?.AddEvent(new ActivityEvent("BadgeGenerated"));

            return imageUri;
        }
        catch (Exception ex)
        {
            activity?.SetTag("badge.generation.success", false);
            activity?.SetTag("otel.status_code", "ERROR");
            activity?.SetTag("exception.type", ex.GetType().Name);
            activity?.SetTag("exception.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("BadgeGenerationFailed"));
            throw;
        }

    }
}
