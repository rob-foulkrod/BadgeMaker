using Azure.AI.OpenAI;

namespace BadgeMaker.Components.Interfaces;

public interface IOpenAIService
{
    Task<string> GenerateImageUriAsync(string prompt, ImageGenerationOptions options);
}
