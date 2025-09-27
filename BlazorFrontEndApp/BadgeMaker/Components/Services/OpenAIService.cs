using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using BadgeMaker.Components.Interfaces;
using BadgeMaker.Components.Models;
using OpenAI;
using OpenAI.Images;

namespace BadgeMaker.Components.Services;

public class OpenAIService : IOpenAIService
{
    private readonly OpenAIConfig openAIConfig;

    public OpenAIService(OpenAIConfig openAIConfig)
    {
        this.openAIConfig = openAIConfig;
    }

    public async Task<string> GenerateImageUriAsync(string prompt, ImageGenerationOptions options)
    {
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


        var credential = new DefaultAzureCredential();

        var endpoint = openAIConfig.endpoint;
        var deployment = openAIConfig.deployment;

        if (string.IsNullOrEmpty(endpoint))
        {
            throw new InvalidOperationException("OpenAI endpoint is not configured.");
        }
        if (string.IsNullOrEmpty(deployment))
        {
            throw new InvalidOperationException("OpenAI deployment name is not configured.");
        }

        AzureOpenAIClient client = new AzureOpenAIClient(new Uri(endpoint), credential);

        var imageGenerations = await client.GetImageClient(deployment).GenerateImageAsync(basePrompt + prompt, options);

        return imageGenerations.Value.ImageUri?.ToString() ?? string.Empty;

    }
}
