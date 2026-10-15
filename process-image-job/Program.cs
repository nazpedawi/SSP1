using System.Text.Json;
using Azure.Storage.Queues;
using ProcessImageJob;
using Shared.Models;

var imageQueue = new QueueClient("UseDevelopmentStorage=true", "process-image");
await imageQueue.CreateIfNotExistsAsync();

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
    ($"{station.Temperature}°C - {station.WeatherDescription}", (20f, 90f), 36, "#FFFFFF"));

// Save it as a file (temporary, just to check)
await using var file = File.Create($"{station.StationId}.png");
await edited.CopyToAsync(file);
Console.WriteLine($"Saved {station.StationId}.png");

await imageQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt);
Console.WriteLine("Message deleted.");