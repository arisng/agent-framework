// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIWebChatServer.AgenticUI;

/// <summary>
/// Represents weather information for a specific location.
/// </summary>
internal sealed class WeatherInfo
{
    /// <summary>
    /// Gets or sets the temperature in Celsius.
    /// </summary>
    [JsonPropertyName("temperature")]
    public required int Temperature { get; init; }

    /// <summary>
    /// Gets or sets the weather conditions (e.g., "sunny", "cloudy", "rainy").
    /// </summary>
    [JsonPropertyName("conditions")]
    public required string Conditions { get; init; }

    /// <summary>
    /// Gets or sets the humidity percentage.
    /// </summary>
    [JsonPropertyName("humidity")]
    public required int Humidity { get; init; }

    /// <summary>
    /// Gets or sets the wind speed in km/h.
    /// </summary>
    [JsonPropertyName("windSpeed")]
    public required int WindSpeed { get; init; }

    /// <summary>
    /// Gets or sets the "feels like" temperature in Celsius.
    /// </summary>
    [JsonPropertyName("feelsLike")]
    public required int FeelsLike { get; init; }
}
