using Azure.Data.Tables;          
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

var jobsTable = new TableClient("UseDevelopmentStorage=true", "jobs");
await jobsTable.CreateIfNotExistsAsync();

app.MapPost("/jobs", async () =>
{
    var jobId = Guid.NewGuid().ToString();

    await jobsTable.AddEntityAsync(new TableEntity(jobId, "job")
    {
        ["Status"] = "Queued",
        ["CreatedAt"] = DateTimeOffset.UtcNow
    });

    await queue.SendMessageAsync(jobId);

    return Results.Accepted($"/jobs/{jobId}", new { jobId });
});

// Return the status and progress of one job
app.MapGet("/jobs/{jobId}", async (string jobId) =>
{
    var result = await jobsTable.GetEntityIfExistsAsync<TableEntity>(jobId, "job");
    if (!result.HasValue)
    {
        return Results.NotFound(new { error = $"Job {jobId} not found" });
    }

    var job = result.Value!;
    var status = job.GetString("Status");
    var totalImages = job.GetInt32("TotalImages") ?? 0;

    // Count the "image-..." rows of this job (every row in the partition except the "job" row)
    var jobRowKey = "job";
    var filter = TableClient.CreateQueryFilter($"PartitionKey eq {jobId} and RowKey ne {jobRowKey}");

    var finishedImages = 0;
    await foreach (var row in jobsTable.QueryAsync<TableEntity>(filter))
    {
        finishedImages++;
    }

    // "Completed" is calculated, not stored: done when every image has been recorded
    if (status == "Processing" && totalImages > 0 && finishedImages >= totalImages)
    {
        status = "Completed";
    }

    var progressPercent = totalImages == 0 ? 0 : finishedImages * 100 / totalImages;

    return Results.Ok(new
    {
        jobId,
        status,
        totalImages,
        finishedImages,
        progressPercent,
        createdAt = job.GetDateTimeOffset("CreatedAt")
    });
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