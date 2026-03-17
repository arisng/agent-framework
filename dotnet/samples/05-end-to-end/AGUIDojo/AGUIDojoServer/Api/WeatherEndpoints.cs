// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoServer.Services;

namespace AGUIDojoServer.Api;

/// <summary>
/// Minimal API endpoints for weather operations.
/// </summary>
/// <remarks>
/// These endpoints use the same <see cref="IWeatherService"/> as AI Tools,
/// demonstrating shared business services between REST API and agentic workflows.
/// </remarks>
internal static class WeatherEndpoints
{
    /// <summary>
    /// Maps weather API endpoints to the application.
    /// </summary>
    /// <param name="group">The route group builder for the /api prefix.</param>
    /// <returns>The route group builder with weather endpoints mapped.</returns>
    public static RouteGroupBuilder MapWeatherEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/weather/{location}", GetWeatherAsync)
            .WithName("GetWeather")
            .WithSummary("Gets the current weather for a location")
            .WithDescription("Returns weather information including temperature, conditions, humidity, wind speed, and feels-like temperature.")
            .Produces<WeatherInfo>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
            .RequireAuthorization();

        return group;
    }

    /// <summary>
    /// Gets the current weather for a specified location.
    /// </summary>
    /// <param name="location">The location to get the weather for (e.g., "Seattle", "London").</param>
    /// <param name="weatherService">The weather service injected via DI.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="WeatherInfo"/> containing the current weather conditions.</returns>
    private static async Task<IResult> GetWeatherAsync(
        string location,
        IWeatherService weatherService,
        CancellationToken cancellationToken)
    {
        WeatherInfo weather = await weatherService.GetWeatherAsync(location, cancellationToken);
        return TypedResults.Ok(weather);
    }
}
