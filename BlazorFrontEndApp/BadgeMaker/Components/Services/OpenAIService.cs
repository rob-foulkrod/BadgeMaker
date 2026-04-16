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

    public async Task<BinaryData> GenerateImageAsync(string prompt, ImageGenerationOptions options)
    {
        var basePrompt = openAIConfig.basePrompt;

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

        return imageGenerations.Value.ImageBytes;

    }
}
