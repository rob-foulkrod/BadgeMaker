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
        AzureCliCredentialOptions azcliOptions = new AzureCliCredentialOptions();
        azcliOptions.TenantId = "f33ccec0-9dd1-498d-a573-a859c975456e";
        
        var cred = new AzureCliCredential(azcliOptions);

        var credential = new DefaultAzureCredential();
       

        AzureOpenAIClient client = new AzureOpenAIClient(new Uri(openAIConfig.endpoint), credential);
       
        var imageGenerations = await client.GetImageClient(openAIConfig.deployment).GenerateImageAsync(prompt, options);

        return imageGenerations.Value.ImageUri.ToString();

    }
}
