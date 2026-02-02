// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Factory interface for creating AGUIChatClient instances for different AG-UI endpoints.
/// </summary>
public interface IAGUIChatClientFactory
{
    /// <summary>
    /// Gets the list of available AG-UI endpoint paths.
    /// </summary>
    IReadOnlyList<EndpointInfo> AvailableEndpoints { get; }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> instance configured for the specified endpoint path.
    /// </summary>
    /// <param name="endpointPath">The AG-UI endpoint path (e.g., "agentic_chat", "backend_tool_rendering").</param>
    /// <returns>An <see cref="IChatClient"/> configured to communicate with the specified endpoint.</returns>
    /// <exception cref="ArgumentException">Thrown when the endpoint path is null, empty, or invalid.</exception>
    IChatClient CreateClient(string endpointPath);
}
