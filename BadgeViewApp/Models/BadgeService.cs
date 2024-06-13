using Azure.Storage.Blobs;

public class BadgeService(IConfiguration _configuration) {
    private BlobServiceClient _blobClient = new BlobServiceClient(_configuration["BadgesStorageAccount"]);


    public List<string> GetBadges() {
        var containerClient = _blobClient.GetBlobContainerClient("badges");

        var badges = containerClient.GetBlobs().Select(b => b.Name).ToList();

        return badges;
    }

    public Stream? GetBadge(string badgeId) {
        var containerClient = _blobClient.GetBlobContainerClient("badges");

        var blobClient = containerClient.GetBlobClient(badgeId);

        if (blobClient.Exists()) {
            return blobClient.OpenRead();
        }   

        return null;
    }
}