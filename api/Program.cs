using Azure.Storage.Queues;  

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var queue = new QueueClient("UseDevelopmentStorage=true", "start-jobs");
await queue.CreateIfNotExistsAsync();

app.MapPost("/jobs", async () =>
{
    var jobId = Guid.NewGuid().ToString();

    await queue.SendMessageAsync(jobId);  

    return Results.Accepted($"/jobs/{jobId}", new { jobId });
});

app.Run();