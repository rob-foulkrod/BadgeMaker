using Azure;
using Azure.AI.OpenAI;
using BadgeMaker.Components.Interfaces;
using BadgeMaker.Components.Models;

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
        options.DeploymentName = openAIConfig.deployment;

        OpenAIClient client = new OpenAIClient(new Uri(openAIConfig.endpoint), new AzureKeyCredential(openAIConfig.apiKey));
        var imageGenerations = await client.GetImageGenerationsAsync(options);
        return imageGenerations.Value.Data.Select(data => data.Url.ToString()).First();
    }
}
