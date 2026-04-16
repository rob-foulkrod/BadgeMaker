namespace BadgeMaker.Components.Models;

public class OpenAIConfig
{
    public const string DefaultBasePrompt = """
                Create a circular badge celebrating the completion of a specific learning module. 
                The badge should include a central image that reflects the particular topic of the 
                module. The design should feature a vibrant color scheme with a primary color that 
                stands out (e.g., a rich blue, energetic red, or lively green) and complementary 
                accent colors. The outer edge of the badge should have a decorative border, such 
                as a subtle pattern or elegant trim, to give it a polished look. Ensure the overall 
                style is visually appealing and cohesive, suitable for a collection of badges. The 
                central image should clearly represent the unique topic of this individual module.

                This badge will be awarded to learners who successfully complete the module on 
                
        """;

    public virtual string? deployment { get; set; }
    public virtual string? endpoint { get; set; }
    public virtual string basePrompt { get; set; } = DefaultBasePrompt;
    public virtual bool IsConfigured => !string.IsNullOrEmpty(deployment) && !string.IsNullOrEmpty(endpoint);

}
