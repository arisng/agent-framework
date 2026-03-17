// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using AGUIDojoServer.Services;

namespace AGUIDojoServer.Tools;

/// <summary>
/// DI-compatible AI Tool wrapper for weather operations.
/// </summary>
/// <remarks>
/// <para>
/// This class is designed to be registered as a Singleton in the DI container,
/// as AI Tools are registered as KeyedSingleton by the Agent Framework.
/// </para>
/// <para>
/// To access scoped services (like <see cref="IWeatherService"/>), the tool
/// uses <see cref="IHttpContextAccessor"/> to resolve the service from
/// <see cref="HttpContext.RequestServices"/> at execution time.
/// </para>
/// </remarks>
public sealed class WeatherTool
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherTool"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor for resolving scoped services.</param>
    public WeatherTool(IHttpContextAccessor httpContextAccessor)
    {
        this._httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Gets the weather for a given location.
    /// </summary>
    /// <param name="location">The location to get the weather for.</param>
    /// <returns>A <see cref="WeatherInfo"/> containing the current weather conditions.</returns>
    /// <exception cref="InvalidOperationException">Thrown when HttpContext is not available or service cannot be resolved.</exception>
    [Description("Get the weather for a given location.")]
    public async Task<WeatherInfo> GetWeatherAsync(
        [Description("The location to get the weather for.")] string location)
    {
        var httpContext = this._httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available. This tool must be called within an HTTP request context.");

        var weatherService = httpContext.RequestServices.GetRequiredService<IWeatherService>();
        return await weatherService.GetWeatherAsync(location, httpContext.RequestAborted);
    }
}
