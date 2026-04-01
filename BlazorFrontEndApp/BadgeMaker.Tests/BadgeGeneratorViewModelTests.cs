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
    private readonly Mock<IBlobStorageService> mockBlobStorageService;
    private readonly BadgeGeneratorViewModel viewModel;

    public BadgeGeneratorViewModelTests()
    {
        mockOpenAIService = new Mock<IOpenAIService>();
        mockServiceBusService = new Mock<IServiceBusService>();
        mockBlobStorageService = new Mock<IBlobStorageService>();
        viewModel = new BadgeGeneratorViewModel(mockOpenAIService.Object, mockServiceBusService.Object, mockBlobStorageService.Object);
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
        var fakeImageData = BinaryData.FromBytes(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        mockOpenAIService.Setup(s => s.GenerateImageAsync(It.IsAny<string>(), It.IsAny<ImageGenerationOptions>()))
                         .ReturnsAsync(fakeImageData);
        mockBlobStorageService.Setup(s => s.UploadBadgeImageAsync(It.IsAny<BinaryData>()))
                              .ReturnsAsync("https://storage.blob.core.windows.net/badges-staging/badge-123.png?sv=2024&sig=sas");

        // Act
        await viewModel.GenerateBadge();

        // Assert
        Assert.NotNull(viewModel.ImageUri);
        Assert.Equal("", viewModel.Message);
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
        var expectedUrl = "http://example.com/image.jpg";
        viewModel.ImageUri = expectedUrl;
        viewModel.UserPrompt = "Valid prompt";
        string? capturedMessage = null;
        mockServiceBusService.Setup(s => s.SendMessageAsync(It.IsAny<string>()))
                             .Callback<string>(msg => capturedMessage = msg)
                             .Returns(Task.CompletedTask);

        // Act
        await viewModel.ApproveImage();

        // Assert
        mockServiceBusService.Verify(s => s.SendMessageAsync(It.IsAny<string>()), Times.Once);
        Assert.NotNull(capturedMessage);
        Assert.Contains(expectedUrl, capturedMessage);
        Assert.Contains("Valid prompt", capturedMessage);
        Assert.Null(viewModel.ImageUri);
        Assert.Equal("Image approved and message sent to Service Bus.", viewModel.Message);
    }
}
