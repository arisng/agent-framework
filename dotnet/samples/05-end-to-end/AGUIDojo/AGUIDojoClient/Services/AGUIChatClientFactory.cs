// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Factory for creating AGUIChatClient instances that always target the unified /chat endpoint.
/// </summary>
public sealed class AGUIChatClientFactory : IAGUIChatClientFactory
{
    private const string UnifiedEndpointPath = "chat";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AGUIChatClientFactory"/> class.
    /// </summary>
    public AGUIChatClientFactory(
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILoggerFactory? loggerFactory = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IChatClient CreateClient()
    {
        HttpClient httpClient = _httpClientFactory.CreateClient("aguiserver");

        return new AGUIChatClient(
            httpClient,
            UnifiedEndpointPath,
            _loggerFactory,
            jsonSerializerOptions: null,
            _serviceProvider);
    }
}
