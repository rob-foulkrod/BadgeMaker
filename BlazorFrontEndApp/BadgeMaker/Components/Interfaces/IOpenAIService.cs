
using OpenAI.Images;

namespace BadgeMaker.Components.Interfaces;

public interface IOpenAIService
{
    Task<BinaryData> GenerateImageAsync(string prompt, ImageGenerationOptions options);
}
