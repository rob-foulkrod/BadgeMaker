using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System.Linq;

public class BadgeService
{
    private readonly BlobServiceClient _blobClient;

    public BadgeService(IConfiguration configuration)
    {
        var uriString = configuration["BadgesStorageAccount"];
        if (string.IsNullOrEmpty(uriString))
        {
            throw new InvalidOperationException("BadgesStorageAccount configuration is missing.");
        }

        _blobClient = new BlobServiceClient(new Uri(uriString), new DefaultAzureCredential());
    }

    public List<string> GetBadges()
    {
        var containerClient = _blobClient.GetBlobContainerClient("badges");

        var badges = containerClient.GetBlobs().Select(b => b.Name).ToList();

        return badges;
    }

    public Stream? GetBadge(string badgeId)
    {
        var containerClient = _blobClient.GetBlobContainerClient("badges");

        var blobClient = containerClient.GetBlobClient(badgeId);

        if (blobClient.Exists())
        {
            return blobClient.OpenRead();
        }

        return null;
    }
}