namespace BadgeMaker.Components.Models;

public class OpenApiConfig
{
    public string apiKey { get; set; }
    public string deployment { get; set; }
    public string endpoint { get; set; }
    public bool IsConfigured => !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(deployment) && !string.IsNullOrEmpty(endpoint);

}
