// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Factory for creating AGUIChatClient instances for different AG-UI endpoints.
/// Supports all 7 AG-UI protocol features demonstrated by AGUIDojoServer.
/// </summary>
public sealed class AGUIChatClientFactory : IAGUIChatClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IServiceProvider _serviceProvider;

    private static readonly List<EndpointInfo> s_endpoints =
    [
        new EndpointInfo("agentic_chat", "Agentic Chat", "Basic streaming chat with conversation context"),
        new EndpointInfo("backend_tool_rendering", "Backend Tool Rendering", "Tool calls with structured result display (WeatherInfo)"),
        new EndpointInfo("human_in_the_loop", "Human-in-the-Loop", "Approval workflows for sensitive tool calls"),
        new EndpointInfo("agentic_generative_ui", "Agentic Generative UI", "Plan/Step progress with JSON Patch updates"),
        new EndpointInfo("tool_based_generative_ui", "Tool-Based UI Rendering", "Dynamic Blazor components based on tool definitions"),
        new EndpointInfo("shared_state", "Shared State", "Bidirectional state sync (Recipe/Ingredient)"),
        new EndpointInfo("predictive_state_updates", "Predictive State Updates", "Streaming tool arguments as optimistic document updates"),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="AGUIChatClientFactory"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating named HTTP clients.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="loggerFactory">Optional logger factory for AGUIChatClient logging.</param>
    public AGUIChatClientFactory(
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILoggerFactory? loggerFactory = null)
    {
        this._httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this._serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this._loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IReadOnlyList<EndpointInfo> AvailableEndpoints => s_endpoints;

    /// <inheritdoc />
    public IChatClient CreateClient(string endpointPath)
    {
        if (string.IsNullOrWhiteSpace(endpointPath))
        {
            throw new ArgumentException("Endpoint path cannot be null or empty.", nameof(endpointPath));
        }

        // Validate endpoint path exists in available endpoints
        if (!s_endpoints.Any(e => e.Path.Equals(endpointPath, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"Unknown endpoint path: '{endpointPath}'. Available endpoints: {string.Join(", ", s_endpoints.Select(e => e.Path))}",
                nameof(endpointPath));
        }

        HttpClient httpClient = this._httpClientFactory.CreateClient("aguiserver");

        return new AGUIChatClient(
            httpClient,
            endpointPath,
            this._loggerFactory,
            jsonSerializerOptions: null,
            this._serviceProvider);
    }
}
