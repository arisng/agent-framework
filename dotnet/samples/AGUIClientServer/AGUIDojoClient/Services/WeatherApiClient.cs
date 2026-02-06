// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Services;

/// <summary>
/// Typed HTTP client for consuming the backend Weather API through the YARP reverse proxy.
/// </summary>
/// <remarks>
/// <para>
/// This client uses relative /api/ URLs which are proxied via YARP to the AGUIDojoServer backend.
/// The YARP configuration (see appsettings.json ReverseProxy section) routes /api/* requests
/// to the backend cluster at http://localhost:5100.
/// </para>
/// <para>
/// Usage pattern:
/// <list type="bullet">
/// <item>Blazor component injects IWeatherApiClient</item>
/// <item>Component calls GetWeatherAsync("Seattle")</item>
/// <item>Request goes to /api/weather/Seattle on this server (AGUIDojoClient)</item>
/// <item>YARP proxies to http://localhost:5100/api/weather/Seattle</item>
/// <item>AGUIDojoServer returns JSON WeatherApiResponse</item>
/// <item>Response is deserialized and returned to component</item>
/// </list>
/// </para>
/// </remarks>
public sealed class WeatherApiClient : IWeatherApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The typed HTTP client configured for API requests.</param>
    /// <remarks>
    /// The HttpClient is configured via IHttpClientFactory with a base address
    /// pointing to the BFF's own origin, allowing YARP to handle the proxying.
    /// </remarks>
    public WeatherApiClient(HttpClient httpClient)
    {
        this._httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<WeatherApiResponse?> GetWeatherAsync(string location, CancellationToken cancellationToken = default)
    {
        // URL-encode the location to handle special characters and spaces
        string encodedLocation = Uri.EscapeDataString(location);

        // Use relative URL - YARP will proxy this to the backend
        // GET /api/weather/{location} -> http://localhost:5100/api/weather/{location}
        return await this._httpClient.GetFromJsonAsync<WeatherApiResponse>(
            $"/api/weather/{encodedLocation}",
            cancellationToken);
    }
}
