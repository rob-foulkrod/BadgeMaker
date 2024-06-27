# BadgeProcessingFunction

## Configuration Requirements

This project requires certain environment variables to be set for local development and deployment. Below is a list of the required environment variables:

- `AzureWebJobsStorage`: The connection string to the Azure storage account used by Azure Functions for managing triggers and logging. For security reasons, the actual connection string is not provided here. Please refer to your Azure portal or contact your administrator to obtain the correct connection string.
- `BlobContainerName`: The name of the blob container where the badge images are stored. This container must be created in the Azure storage account specified in `AzureWebJobsStorage`. The name of the container is arbitrary and can be set to any valid name. The container name is case-insensitive and must be unique within the storage account. The container name is not sensitive information and can be shared publicly.`
- `badgeservicebus`: The connection string to the Azure Service Bus used by this function. Similar to `AzureWebJobsStorage`, the actual connection string is not disclosed here for security purposes. Obtain this connection string from your Azure portal or through your administrator.


## Local Development

For local development, these configurations must be added to your `local.settings.json` file under the `Values` section. Ensure not to include sensitive information in public repositories or shared documents.

Example `local.settings.json` (sensitive information omitted):

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=...",
    "BlobContainerName": "badges",
    "badgeservicebus": "Endpoint=sb://..."
  }
}
```

## Security Notice

Never commit sensitive information like connection strings or keys to version control. Always use secure methods to manage and access sensitive information, such as Azure Key Vault or environment variables in your deployment environment.


