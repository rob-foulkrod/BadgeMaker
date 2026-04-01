namespace BadgeMaker.Components.Models;

public class OpenAIConfig
{
    public virtual string? deployment { get; set; }
    public virtual string? endpoint { get; set; }
    public virtual bool IsConfigured => !string.IsNullOrEmpty(deployment) && !string.IsNullOrEmpty(endpoint);

}
