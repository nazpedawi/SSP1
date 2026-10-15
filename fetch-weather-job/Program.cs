using System.Text.Json;
using Azure.Storage.Queues;  
using FetchWeatherJob;
using Shared.Models;

var startQueue = new QueueClient("UseDevelopmentStorage=true", "start-jobs");
var imageQueue = new QueueClient("UseDevelopmentStorage=true", "process-image");

await startQueue.CreateIfNotExistsAsync();
await imageQueue.CreateIfNotExistsAsync();

var response = await startQueue.ReceiveMessageAsync();
var message = response.Value;

if (message == null)
{
    Console.WriteLine("No jobs in the queue. Nothing to do.");
    return;
}

var jobId = message.MessageText;
Console.WriteLine($"Picked up job {jobId}");

using var client = new HttpClient();
client.DefaultRequestHeaders.UserAgent.ParseAdd("SSP1-WeatherApp/1.0");

var json = await client.GetStringAsync("https://data.buienradar.nl/2.0/feed/json");

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var raw = JsonSerializer.Deserialize<BuienradarResponseDto>(json, options);

var rawStations = raw?.Actual?.StationMeasurements ?? new List<StationMeasurementDto>();

var stations = rawStations.Select(s => new WeatherStation(
    s.StationId,
    s.StationName,
    s.Lat,
    s.Lon,
    s.Regio,
    s.Timestamp,
    s.WeatherDescription,
    s.IconUrl,
    s.FullIconUrl,
    s.GraphUrl,
    s.WindDirection,
    s.AirPressure,
    s.Temperature,
    s.GroundTemperature,
    s.FeelTemperature,
    s.Visibility,
    s.WindGusts,
    s.WindSpeed,
    s.WindSpeedBft,
    s.Humidity,
    s.Precipitation,
    s.SunPower,
    s.RainFallLast24Hour,
    s.RainFallLastHour,
    s.WindDirectionDegrees
)).ToList();

Console.WriteLine($"Fetched {stations.Count} weather stations.");

foreach (var station in stations.Take(50))
{
    var imageMessage = new ImageJobMessage(jobId, station);
    var messageJson = JsonSerializer.Serialize(imageMessage);

    await imageQueue.SendMessageAsync(messageJson);
    Console.WriteLine($"Queued image for {station.StationName}");
}

await startQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt);
Console.WriteLine($"Finished job {jobId}, message deleted.");