using System.Text.Json;
using FetchWeatherJob;
using Shared.Models;

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
foreach (var station in stations)
{
    Console.WriteLine($"{station.StationName}: {station.Temperature}°C, {station.WeatherDescription}");
}