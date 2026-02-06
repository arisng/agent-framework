// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Services;

/// <summary>
/// Weather information returned from the backend Weather API.
/// </summary>
/// <param name="Temperature">The temperature in degrees Celsius.</param>
/// <param name="Conditions">The weather conditions (e.g., sunny, cloudy, rainy).</param>
/// <param name="Humidity">The humidity percentage.</param>
/// <param name="WindSpeed">The wind speed in km/h.</param>
/// <param name="FeelsLike">The feels-like temperature in degrees Celsius.</param>
/// <remarks>
/// <para>
/// This record is the response type from the AGUIDojoServer's /api/weather endpoint.
/// It uses camelCase JSON property names matching ASP.NET Core's default serialization.
/// </para>
/// <para>
/// Note: This is distinct from <see cref="Models.WeatherInfo"/> which is used for
/// AG-UI tool rendering with different JSON property naming conventions.
/// </para>
/// </remarks>
public sealed record WeatherApiResponse(
    int Temperature,
    string Conditions,
    int Humidity,
    int WindSpeed,
    int FeelsLike);

/// <summary>
/// Client service for consuming the backend Weather API through the YARP reverse proxy.
/// </summary>
/// <remarks>
/// This typed HTTP client uses relative /api/ URLs which are proxied via YARP
/// to the AGUIDojoServer backend. This follows the BFF (Backend-for-Frontend) pattern
/// where the Blazor Server acts as a proxy for API requests.
/// </remarks>
public interface IWeatherApiClient
{
    /// <summary>
    /// Gets the current weather for a specified location.
    /// </summary>
    /// <param name="location">The location to get weather for (e.g., "Seattle", "London").</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="WeatherApiResponse"/> containing the current weather conditions.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    Task<WeatherApiResponse?> GetWeatherAsync(string location, CancellationToken cancellationToken = default);
}
