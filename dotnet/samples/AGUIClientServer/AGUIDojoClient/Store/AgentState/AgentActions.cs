// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Store.AgentState;

/// <summary>
/// Fluxor actions for agent state management.
/// Dispatched from the SSE streaming loop in <c>Chat.razor</c> when agent-related
/// state changes occur (endpoint selection, stream start/stop, author name changes).
/// </summary>
public static class AgentActions
{
    /// <summary>
    /// Sets the currently selected AG-UI endpoint path.
    /// Dispatched when the user selects a different demo endpoint from the header dropdown.
    /// </summary>
    /// <param name="EndpointPath">The AG-UI endpoint path to select (e.g., "agentic_chat", "shared_state").</param>
    public sealed record SetEndpointAction(string EndpointPath);

    /// <summary>
    /// Sets whether the agent is currently streaming a response.
    /// Dispatched at the start and end of the SSE response streaming loop.
    /// </summary>
    /// <param name="IsRunning">
    /// <c>true</c> when the agent begins streaming a response;
    /// <c>false</c> when streaming completes or is cancelled.
    /// </param>
    public sealed record SetRunningAction(bool IsRunning);

    /// <summary>
    /// Sets the current author name from SSE response events.
    /// Dispatched when the streaming response includes an <c>AuthorName</c> value,
    /// enabling multi-agent visualization with distinct identities.
    /// </summary>
    /// <param name="AuthorName">The author name from the SSE event, or <c>null</c> to clear.</param>
    public sealed record SetAuthorNameAction(string? AuthorName);
}
