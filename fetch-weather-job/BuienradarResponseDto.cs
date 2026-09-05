using System.Text.Json.Serialization;

namespace FetchWeatherJob;

public class BuienradarResponseDto
{
    [JsonPropertyName("buienradar")]
    public BuienradarMetaDto? Buienradar { get; set; }

    [JsonPropertyName("actual")]
    public ActualDto? Actual { get; set; }
}

public class BuienradarMetaDto
{
    [JsonPropertyName("copyright")]
    public string? Copyright { get; set; }

    [JsonPropertyName("terms")]
    public string? Terms { get; set; }
}

public class ActualDto
{
    [JsonPropertyName("stationmeasurements")]
    public List<StationMeasurementDto> StationMeasurements { get; set; } = new();
}

public class StationMeasurementDto
{
    [JsonPropertyName("stationid")]
    public int StationId { get; set; }

    [JsonPropertyName("stationname")]
    public string StationName { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double? Lat { get; set; }

    [JsonPropertyName("lon")]
    public double? Lon { get; set; }

    [JsonPropertyName("regio")]
    public string? Regio { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("weatherdescription")]
    public string? WeatherDescription { get; set; }

    [JsonPropertyName("iconurl")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("fullIconUrl")]
    public string? FullIconUrl { get; set; }

    [JsonPropertyName("graphUrl")]
    public string? GraphUrl { get; set; }

    [JsonPropertyName("winddirection")]
    public string? WindDirection { get; set; }

    [JsonPropertyName("airpressure")]
    public double? AirPressure { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("groundtemperature")]
    public double? GroundTemperature { get; set; }

    [JsonPropertyName("feeltemperature")]
    public double? FeelTemperature { get; set; }

    [JsonPropertyName("visibility")]
    public double? Visibility { get; set; }

    [JsonPropertyName("windgusts")]
    public double? WindGusts { get; set; }

    [JsonPropertyName("windspeed")]
    public double? WindSpeed { get; set; }

    [JsonPropertyName("windspeedBft")]
    public int? WindSpeedBft { get; set; }

    [JsonPropertyName("humidity")]
    public double? Humidity { get; set; }

    [JsonPropertyName("precipitation")]
    public double? Precipitation { get; set; }

    [JsonPropertyName("sunpower")]
    public double? SunPower { get; set; }

    [JsonPropertyName("rainFallLast24Hour")]
    public double? RainFallLast24Hour { get; set; }

    [JsonPropertyName("rainFallLastHour")]
    public double? RainFallLastHour { get; set; }

    [JsonPropertyName("winddirectiondegrees")]
    public int? WindDirectionDegrees { get; set; }
}