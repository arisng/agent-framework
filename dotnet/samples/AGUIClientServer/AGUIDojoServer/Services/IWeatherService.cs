// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoServer.Services;

/// <summary>
/// Represents weather information for a location.
/// </summary>
/// <param name="Temperature">The temperature in degrees Celsius.</param>
/// <param name="Conditions">The weather conditions (e.g., sunny, cloudy, rainy).</param>
/// <param name="Humidity">The humidity percentage.</param>
/// <param name="WindSpeed">The wind speed in km/h.</param>
/// <param name="FeelsLike">The feels-like temperature in degrees Celsius.</param>
public sealed record WeatherInfo(
    int Temperature,
    string Conditions,
    int Humidity,
    int WindSpeed,
    int FeelsLike);

/// <summary>
/// Provides weather information for locations.
/// </summary>
/// <remarks>
/// This service is shared between Minimal API endpoints and AI Tools,
/// enabling consistent weather data access across the application.
/// </remarks>
public interface IWeatherService
{
    /// <summary>
    /// Gets the current weather for a specified location.
    /// </summary>
    /// <param name="location">The location to get the weather for (e.g., "Seattle", "London").</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="WeatherInfo"/> containing the current weather conditions.</returns>
    Task<WeatherInfo> GetWeatherAsync(string location, CancellationToken cancellationToken = default);
}
