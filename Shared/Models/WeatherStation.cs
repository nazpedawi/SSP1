namespace Shared.Models;

public record WeatherStation(
    int StationId,
    string StationName,
    double? Lat,
    double? Lon,
    string? Regio,
    DateTime? Timestamp,
    string? WeatherDescription,
    string? IconUrl,
    string? FullIconUrl,
    string? GraphUrl,
    string? WindDirection,
    double? AirPressure,
    double? Temperature,
    double? GroundTemperature,
    double? FeelTemperature,
    double? Visibility,
    double? WindGusts,
    double? WindSpeed,
    int? WindSpeedBft,
    double? Humidity,
    double? Precipitation,
    double? SunPower,
    double? RainFallLast24Hour,
    double? RainFallLastHour,
    int? WindDirectionDegrees
);