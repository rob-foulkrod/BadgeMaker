using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using BadgeMaker.Components.Interfaces;

namespace BadgeMaker.Components.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private const string StagingContainer = "badges-staging";

    public BlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task<string> UploadBadgeImageAsync(BinaryData imageData)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(StagingContainer);
        var blobName = $"badge-{Guid.NewGuid()}.png";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(imageData.ToStream(), new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" }
        });

        // Generate a user-delegation SAS token (read-only, 1 hour expiry)
        var delegationKey = await _blobServiceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1));

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = StagingContainer,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = new BlobUriBuilder(blobClient.Uri)
        {
            Sas = sasBuilder.ToSasQueryParameters(delegationKey.Value, _blobServiceClient.AccountName)
        };

        return sasUri.ToUri().ToString();
    }
}
