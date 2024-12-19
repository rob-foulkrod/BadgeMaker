
using OpenAI.Images;

namespace BadgeMaker.Components.Interfaces;

public interface IOpenAIService
{
    Task<string> GenerateImageUriAsync(string prompt, ImageGenerationOptions options);
}
