using BadgeMaker.Components.Models;
using Xunit;

namespace BadgeMaker.Tests;
public class OpenAIConfigTests
{
    [Fact]
    public void IsConfigured_ReturnsFalse_WhenNothingIsSet()
    {
        // Arrange
        var config = new OpenAIConfig();

        // Act
        bool isValid = config.IsConfigured;

        // Assert
        Assert.False(isValid, "Config should be invalid when Endpoint and Deployment are not set.");
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenOnlyEndpointIsSet()
    {
        // Arrange
        var config = new OpenAIConfig
        {
            endpoint = "https://api.example.com"
        };

        // Act
        bool isValid = config.IsConfigured;

        // Assert
        Assert.False(isValid, "Config should be invalid when only Endpoint is set.");
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenOnlyDeploymentIsSet()
    {
        // Arrange
        var config = new OpenAIConfig
        {
            deployment = "some-deployment"
        };

        // Act
        bool isValid = config.IsConfigured;

        // Assert
        Assert.False(isValid, "Config should be invalid when only Deployment is set.");
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_WhenEndpointAndDeploymentAreSet()
    {
        // Arrange
        var config = new OpenAIConfig
        {
            endpoint = "https://api.example.com",
            deployment = "some-deployment"
        };

        // Act
        bool isValid = config.IsConfigured;

        // Assert
        Assert.True(isValid, "Config should be valid when Endpoint and Deployment are both set.");
    }

    [Fact]
    public void BasePrompt_DefaultsToDefaultBasePrompt()
    {
        // Arrange
        var config = new OpenAIConfig();

        // Act & Assert
        Assert.Equal(OpenAIConfig.DefaultBasePrompt, config.basePrompt);
    }

    [Fact]
    public void BasePrompt_CanBeOverridden()
    {
        // Arrange
        var config = new OpenAIConfig
        {
            basePrompt = "Custom prompt: "
        };

        // Act & Assert
        Assert.Equal("Custom prompt: ", config.basePrompt);
    }
}
