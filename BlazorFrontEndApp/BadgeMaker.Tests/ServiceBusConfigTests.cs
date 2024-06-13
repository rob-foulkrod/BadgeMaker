using BadgeMaker.Components.Models;
using Xunit;

public class ServiceBusConfigTests
{
    [Fact]
    public void IsConfigured_ShouldReturnFalse_WhenBothPropertiesAreNotSet()
    {
        // Arrange
        var config = new ServiceBusConfig();

        // Act
        var result = config.IsConfigured;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsConfigured_ShouldReturnFalse_WhenOnlyConnectionStringIsSet()
    {
        // Arrange
        var config = new ServiceBusConfig
        {
            connectionString = "some-connection-string"
        };

        // Act
        var result = config.IsConfigured;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsConfigured_ShouldReturnFalse_WhenOnlyQueueNameIsSet()
    {
        // Arrange
        var config = new ServiceBusConfig
        {
            queueName = "some-queue-name"
        };

        // Act
        var result = config.IsConfigured;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsConfigured_ShouldReturnTrue_WhenBothPropertiesAreSet()
    {
        // Arrange
        var config = new ServiceBusConfig
        {
            connectionString = "some-connection-string",
            queueName = "some-queue-name"
        };

        // Act
        var result = config.IsConfigured;

        // Assert
        Assert.True(result);
    }
}
