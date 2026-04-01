namespace BadgeMaker.Components.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadBadgeImageAsync(BinaryData imageData);
}
