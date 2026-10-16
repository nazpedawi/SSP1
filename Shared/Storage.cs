using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;

namespace Shared;

// One place that decides HOW we connect to storage:
// - In Azure: STORAGE_ACCOUNT_NAME is set (by Bicep) -> connect with the app's managed identity (no keys)
// - Locally:  STORAGE_ACCOUNT_NAME is not set        -> connect to Azurite
public static class Storage
{
    private const string Azurite = "UseDevelopmentStorage=true";

    private static readonly string? AccountName =
        Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME");

    public static bool IsAzure => !string.IsNullOrWhiteSpace(AccountName);

    public static QueueClient Queue(string queueName) => IsAzure
        ? new QueueClient(new Uri($"https://{AccountName}.queue.core.windows.net/{queueName}"), new DefaultAzureCredential())
        : new QueueClient(Azurite, queueName);

    public static BlobServiceClient BlobService() => IsAzure
        ? new BlobServiceClient(new Uri($"https://{AccountName}.blob.core.windows.net"), new DefaultAzureCredential())
        : new BlobServiceClient(Azurite);

    public static BlobContainerClient Container(string containerName) =>
        BlobService().GetBlobContainerClient(containerName);

    public static TableClient Table(string tableName) => IsAzure
        ? new TableClient(new Uri($"https://{AccountName}.table.core.windows.net"), tableName, new DefaultAzureCredential())
        : new TableClient(Azurite, tableName);
}