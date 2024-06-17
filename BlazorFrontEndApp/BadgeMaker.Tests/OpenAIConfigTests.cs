using BadgeMaker.Components.Models;
using Xunit;

namespace BadgeMaker.Tests;
public class OpenAIConfigTests
{
    [Fact]
    public void IsValid_ReturnsFalse_WhenApiKeyAndEndpointAreNotSet()
    {
        // Arrange
        var config = new OpenAIConfig();

        // Act
        bool isValid = config.IsConfigured;

        // Assert
        Assert.False(isValid, "Config should be invalid when both ApiKey and Endpoint are not set.");
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenOnlyApiKeyIsSet()
    {
        // Arrange
        var config = new OpenAIConfig
        {
            apiKey = "some-api-key"
        };

        // Act
        bool isValid = config.IsConfigured;

        // Assert
        Assert.False(isValid, "Config should be invalid when only ApiKey is set.");
    }

    [Fact]
    public void IsValid_ReturnsFalse_WhenOnlyEndpointIsSet()
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
    public void IsValid_ReturnsTrue_WhenApiKeyEndpointAndDeploymentAreSet()
    {
        // Arrange
        var config = new OpenAIConfig
        {
            apiKey = "some-api-key",
            endpoint = "https://api.example.com",
            deployment = "some-deployment"
        };

        // Act
        bool isValid = config.IsConfigured;

        // Assert
        Assert.True(isValid, "Config should be valid when ApiKey, Endpoint, and Deployment are all set.");
    }
}
