using Azure.Data.Tables;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Shared;
using Shared.Models;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Storage clients: Azurite locally, managed identity in Azure
var queue = Storage.Queue("start-jobs");
await queue.CreateIfNotExistsAsync();

var blobService = Storage.BlobService();
var images = blobService.GetBlobContainerClient("images");
await images.CreateIfNotExistsAsync();

var jobsTable = Storage.Table("jobs");
await jobsTable.CreateIfNotExistsAsync();

// Root: shows that the API is running and which endpoints exist
app.MapGet("/", () => Results.Ok(new
{
    message = "Weather image API is running",
    endpoints = new[] { "POST /jobs", "GET /jobs/{jobId}", "GET /jobs/{jobId}/images" },
    documentation = "See api/api.http in the GitHub repo"
}));

// Start a new job
app.MapPost("/jobs", async () =>
{
    var jobId = Guid.NewGuid().ToString();

    // Save the job in the table BEFORE putting it on the queue
    await jobsTable.AddEntityAsync(new TableEntity(jobId, "job")
    {
        ["Status"] = JobStatus.Queued.ToString(),
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
    var status = Enum.Parse<JobStatus>(job.GetString("Status")!);
    var totalImages = job.GetInt32("TotalImages") ?? 0;

    // Count the "image-..." rows of this job (every row in the partition except the "job" row)
    var jobRowKey = "job";
    var filter = TableClient.CreateQueryFilter($"PartitionKey eq {jobId} and RowKey ne {jobRowKey}");

    var finishedImages = 0;
    await foreach (var row in jobsTable.QueryAsync<TableEntity>(filter))
    {
        finishedImages++;
    }

    // "Completed" is calculated: done when every image has been recorded.
    // The first time we see this, save it to the job row so Table storage shows it too.
    if (status == JobStatus.Processing && totalImages > 0 && finishedImages >= totalImages)
    {
        status = JobStatus.Completed;

        await jobsTable.UpsertEntityAsync(
            new TableEntity(jobId, jobRowKey) { ["Status"] = JobStatus.Completed.ToString() },
            TableUpdateMode.Merge);
    }

    var progressPercent = totalImages == 0 ? 0 : finishedImages * 100 / totalImages;

    return Results.Ok(new
    {
        jobId,
        status = status.ToString(),
        totalImages,
        finishedImages,
        progressPercent,
        createdAt = job.GetDateTimeOffset("CreatedAt")
    });
});

// Return a temporary link (SAS) for every image of this job
app.MapGet("/jobs/{jobId}/images", async (string jobId, CancellationToken cancellationToken) =>
{
    var job = await jobsTable.GetEntityIfExistsAsync<TableEntity>(jobId, "job");
    if (!job.HasValue)
    {
        return Results.NotFound(new { error = $"Job {jobId} not found" });
    }

    var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);   // a bit in the past, in case clocks differ slightly
    var expiresOn = DateTimeOffset.UtcNow.AddHours(1);

    // In Azure there is no account key: ask Azure for a user delegation key ONCE per request
    UserDelegationKey? delegationKey = null;
    if (Storage.IsAzure)
    {
        var keyResponse = await blobService.GetUserDelegationKeyAsync(startsOn, expiresOn, cancellationToken);
        delegationKey = keyResponse.Value;
    }

    var urls = new List<string>();

    // Only the blobs whose name starts with "{jobId}/" belong to this job
    await foreach (var blob in images.GetBlobsAsync(BlobTraits.None, BlobStates.None, $"{jobId}/", cancellationToken))
    {
        var blobClient = images.GetBlobClient(blob.Name);

        if (delegationKey == null)
        {
            // Locally (Azurite): sign the link with the account key from the connection string
            urls.Add(blobClient.GenerateSasUri(BlobSasPermissions.Read, expiresOn).ToString());
        }
        else
        {
            // In Azure: sign the link with the user delegation key
            var sas = new BlobSasBuilder
            {
                BlobContainerName = images.Name,
                BlobName = blob.Name,
                Resource = "b",              // "b" = one blob
                StartsOn = startsOn,
                ExpiresOn = expiresOn
            };
            sas.SetPermissions(BlobSasPermissions.Read);

            var sasToken = sas.ToSasQueryParameters(delegationKey, blobService.AccountName);
            urls.Add($"{blobClient.Uri}?{sasToken}");
        }
    }

    return Results.Ok(new { jobId, count = urls.Count, images = urls });
});

app.Run();