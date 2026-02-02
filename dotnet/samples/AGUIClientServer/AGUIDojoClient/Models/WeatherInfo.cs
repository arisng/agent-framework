// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Weather information returned by the get_weather tool.
/// </summary>
public sealed class WeatherInfo
{
    /// <summary>
    /// Temperature in degrees (Fahrenheit).
    /// </summary>
    [JsonPropertyName("temperature")]
    public int Temperature { get; init; }

    /// <summary>
    /// Weather conditions (e.g., "Sunny", "Cloudy", "Rainy").
    /// </summary>
    [JsonPropertyName("conditions")]
    public string Conditions { get; init; } = string.Empty;

    /// <summary>
    /// Humidity percentage.
    /// </summary>
    [JsonPropertyName("humidity")]
    public int Humidity { get; init; }

    /// <summary>
    /// Wind speed in mph.
    /// </summary>
    [JsonPropertyName("wind_speed")]
    public int WindSpeed { get; init; }

    /// <summary>
    /// Feels-like temperature in degrees (Fahrenheit).
    /// </summary>
    [JsonPropertyName("feelsLike")]
    public int FeelsLike { get; init; }
}
