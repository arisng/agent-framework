// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoServer.Services;

/// <summary>
/// Default implementation of <see cref="IWeatherService"/> that provides simulated weather data.
/// </summary>
/// <remarks>
/// This implementation includes an artificial delay to demonstrate tool call progress
/// in the AG-UI protocol, allowing users to see the tool call in progress before
/// the result is returned.
/// </remarks>
public sealed class WeatherService : IWeatherService
{
    /// <summary>
    /// The simulated delay in milliseconds to demonstrate tool call progress in the UI.
    /// </summary>
    private const int SimulatedDelayMs = 1500;

    /// <inheritdoc/>
    public async Task<WeatherInfo> GetWeatherAsync(string location, CancellationToken cancellationToken = default)
    {
        // Add artificial delay to demonstrate tool call appearing before result in UI
        // This allows users to see the tool call in progress before the LLM processes the result
        await Task.Delay(SimulatedDelayMs, cancellationToken);

        // Return simulated weather data
        // In a production scenario, this would call a real weather API
        return new WeatherInfo(
            Temperature: 20,
            Conditions: "sunny",
            Humidity: 50,
            WindSpeed: 10,
            FeelsLike: 25);
    }
}
