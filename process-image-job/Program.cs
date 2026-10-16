using System.Text.Json;
using ProcessImageJob;
using Shared.Models;
using Azure.Storage.Blobs.Models;
using Azure.Data.Tables;
using Shared;

var imageQueue = Storage.Queue("process-image");
await imageQueue.CreateIfNotExistsAsync();

var jobsTable = Storage.Table("jobs");
await jobsTable.CreateIfNotExistsAsync();

var response = await imageQueue.ReceiveMessageAsync();
var message = response.Value;

if (message == null)
{
    Console.WriteLine("No images to make. Nothing to do.");
    return;
}

var imageJob = JsonSerializer.Deserialize<ImageJobMessage>(message.MessageText)!;
var station = imageJob.Station;

Console.WriteLine($"Job {imageJob.JobId}: {station.StationName}, {station.Temperature}°C, {station.WeatherDescription}");

var unsplashKey = Environment.GetEnvironmentVariable("UNSPLASH_ACCESS_KEY")
    ?? throw new Exception("UNSPLASH_ACCESS_KEY is not set");

using var http = new HttpClient();
http.DefaultRequestHeaders.Add("Authorization", $"Client-ID {unsplashKey}");

var city = station.Regio ?? "Netherlands";
string photoJson;
try
{
    var query = Uri.EscapeDataString($"{city} Netherlands");
    photoJson = await http.GetStringAsync(
        $"https://api.unsplash.com/photos/random?query={query}&orientation=landscape");
    Console.WriteLine($"Found a photo for {city}");
}
catch (HttpRequestException)
{
    // Small places or stations at sea may have no photos: use a general one instead
    Console.WriteLine($"No photo found for {city}, using a general one.");
    photoJson = await http.GetStringAsync(
        "https://api.unsplash.com/photos/random?query=netherlands%20landscape&orientation=landscape");
}

using var doc = JsonDocument.Parse(photoJson);
var imageUrl = doc.RootElement.GetProperty("urls").GetProperty("regular").GetString()!;
Console.WriteLine($"Image: {imageUrl}");

var imageBytes = await http.GetByteArrayAsync(imageUrl);

// Write the weather on the photo
using var original = new MemoryStream(imageBytes);
using var edited = ImageHelper.AddTextToImage(original,
    (station.StationName, (20f, 20f), 48, "#FFFFFF"),
    (station.Regio ?? "Onbekende regio", (20f, 100f), 32, "#FFFFFF"),
    ($"{station.Temperature}°C - {station.WeatherDescription}", (20f, 165f), 36, "#FFFFFF"));

// Upload the image to Blob storage
var container = Storage.Container("images");
await container.CreateIfNotExistsAsync();

var blobName = $"{imageJob.JobId}/{station.StationId}.png";
var blob = container.GetBlobClient(blobName);

await blob.UploadAsync(edited, new BlobUploadOptions
{
    HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" }
});
Console.WriteLine($"Uploaded {blobName}");

// Record that this image is finished: one row per image
await jobsTable.UpsertEntityAsync(new TableEntity(imageJob.JobId, $"image-{station.StationId}")
{
    ["StationName"] = station.StationName,
    ["BlobName"] = blobName,
    ["FinishedAt"] = DateTimeOffset.UtcNow
});
Console.WriteLine($"Recorded image-{station.StationId} in the jobs table");

await imageQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt);
Console.WriteLine("Message deleted.");