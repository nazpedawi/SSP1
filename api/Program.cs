using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Sas;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var queue = new QueueClient("UseDevelopmentStorage=true", "start-jobs");
await queue.CreateIfNotExistsAsync();

var images = new BlobContainerClient("UseDevelopmentStorage=true", "images");
await images.CreateIfNotExistsAsync();

app.MapPost("/jobs", async () =>
{
    var jobId = Guid.NewGuid().ToString();

    await queue.SendMessageAsync(jobId);

    return Results.Accepted($"/jobs/{jobId}", new { jobId });
});

// Return a temporary link (SAS) for every image of this job
app.MapGet("/jobs/{jobId}/images", async (string jobId, CancellationToken cancellationToken) =>
{
    var expiresOn = DateTimeOffset.UtcNow.AddHours(1);
    var urls = new List<string>();

    // Only the blobs whose name starts with "{jobId}/" belong to this job
    await foreach (var blob in images.GetBlobsAsync(BlobTraits.None, BlobStates.None, $"{jobId}/", cancellationToken))
    {
        var blobClient = images.GetBlobClient(blob.Name);
        var sasUrl = blobClient.GenerateSasUri(BlobSasPermissions.Read, expiresOn);
        urls.Add(sasUrl.ToString());
    }

    return Results.Ok(new { jobId, count = urls.Count, images = urls });
});

app.Run();