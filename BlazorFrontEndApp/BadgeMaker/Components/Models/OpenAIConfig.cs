namespace BadgeMaker.Components.Models;

public class OpenAIConfig
{
    public virtual string apiKey { get; set; } = string.Empty;
    public virtual string deployment { get; set; } = string.Empty;
    public virtual string endpoint { get; set; } = string.Empty;
    public virtual bool IsConfigured => !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(deployment) && !string.IsNullOrEmpty(endpoint);

}
