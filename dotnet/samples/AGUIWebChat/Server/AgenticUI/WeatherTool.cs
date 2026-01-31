// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;

namespace AGUIWebChatServer.AgenticUI;

/// <summary>
/// Provides weather-related tool functions for the agent.
/// </summary>
internal static class WeatherTool
{
    /// <summary>
    /// Gets the weather for a given location.
    /// This is a mock implementation that returns static weather data.
    /// </summary>
    /// <param name="location">The location to get the weather for.</param>
    /// <returns>Weather information for the specified location.</returns>
    [Description("Get the weather for a given location.")]
    public static WeatherInfo GetWeather(
        [Description("The location to get the weather for.")] string location)
    {
        // Mock weather data - in a real implementation, this would call a weather API
        return new WeatherInfo
        {
            Temperature = 20,
            Conditions = "sunny",
            Humidity = 50,
            WindSpeed = 10,
            FeelsLike = 25
        };
    }
}
