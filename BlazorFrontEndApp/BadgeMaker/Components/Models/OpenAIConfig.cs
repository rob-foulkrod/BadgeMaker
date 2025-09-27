namespace BadgeMaker.Components.Models;

public class OpenAIConfig
{
    public virtual string? apiKey { get; set; }
    public virtual string? deployment { get; set; }
    public virtual string? endpoint { get; set; }
    public virtual bool IsConfigured => !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(deployment) && !string.IsNullOrEmpty(endpoint);

}
