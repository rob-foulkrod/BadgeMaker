using Moq;
using BadgeMaker.Components.Interfaces;
using Azure.AI.OpenAI;
using BadgeMaker.Components.Models;
using OpenAI.Images;

namespace BadgeMaker.Tests;

public class BadgeGeneratorViewModelTests
{
    private readonly Mock<IOpenAIService> mockOpenAIService;
    private readonly Mock<IServiceBusService> mockServiceBusService;
    private readonly BadgeGeneratorViewModel viewModel;

    public BadgeGeneratorViewModelTests()
    {
        mockOpenAIService = new Mock<IOpenAIService>();
        mockServiceBusService = new Mock<IServiceBusService>();
        viewModel = new BadgeGeneratorViewModel(mockOpenAIService.Object, mockServiceBusService.Object);
    }

    [Fact]
    public async Task GenerateBadge_WithEmptyUserPrompt_SetsMessageToEnterPrompt()
    {
        // Arrange
        viewModel.UserPrompt = "";

        // Act
        await viewModel.GenerateBadge();

        // Assert
        Assert.Equal("Please enter a prompt", viewModel.Message);
    }

    [Fact]
    public async Task GenerateBadge_WithValidPrompt_GeneratesImageUri()
    {
        // Arrange
        viewModel.UserPrompt = "Valid prompt";
        mockOpenAIService.Setup(s => s.GenerateImageUriAsync(It.IsAny<string>(), It.IsAny<ImageGenerationOptions>()))
                         .ReturnsAsync("http://example.com/image.jpg");

        // Act
        await viewModel.GenerateBadge();

        // Assert
        Assert.NotNull(viewModel.ImageUri);
        Assert.Equal("", viewModel.Message); // Assuming successful generation clears the message
    }

    [Fact]
    public async Task ApproveImage_WithNoImageUri_SetsMessageToNoImageToApprove()
    {
        // Arrange
        viewModel.ImageUri = null;

        // Act
        await viewModel.ApproveImage();

        // Assert
        Assert.Equal("No Image to Approve.", viewModel.Message);
    }

    [Fact]
    public async Task ApproveImage_WithImageUri_SendsMessageToServiceBus()
    {
        // Arrange
        viewModel.ImageUri = "http://example.com/image.jpg";
        viewModel.UserPrompt = "Valid prompt";
        mockServiceBusService.Setup(s => s.SendMessageAsync(It.IsAny<string>()))
                             .Returns(Task.CompletedTask);

        // Act
        await viewModel.ApproveImage();

        // Assert
        mockServiceBusService.Verify(s => s.SendMessageAsync(It.IsAny<string>()), Times.Once);
        Assert.Equal("Image approved and message sent to Service Bus.", viewModel.Message);
    }
}
