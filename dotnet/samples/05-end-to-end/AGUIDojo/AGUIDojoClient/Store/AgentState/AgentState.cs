// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace AGUIDojoClient.Store.AgentState;

/// <summary>
/// Immutable Fluxor state record for agent management.
/// Tracks the currently selected AG-UI endpoint, whether the agent is actively streaming
/// a response, and the current author name from SSE events.
/// </summary>
/// <remarks>
/// This state is managed via Fluxor actions and reducers. Components read from
/// <c>IState&lt;AgentState&gt;</c> instead of local fields. The streaming loop in
/// <c>Chat.razor</c> dispatches <see cref="AgentActions.SetEndpointAction"/> on endpoint change,
/// <see cref="AgentActions.SetRunningAction"/> on stream start/stop, and
/// <see cref="AgentActions.SetAuthorNameAction"/> from SSE <c>AuthorName</c> events.
/// The feature is registered via <see cref="AgentFeature"/> which provides the initial state.
/// </remarks>
[SuppressMessage("Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Justification = "Fluxor convention: state record matches folder/namespace name.")]
public sealed record AgentState
{
    /// <summary>
    /// The default AG-UI endpoint path used when no explicit endpoint is selected.
    /// </summary>
    public const string DefaultEndpointPath = "agentic_chat";

    /// <summary>
    /// The currently selected AG-UI endpoint path (e.g., "agentic_chat", "shared_state",
    /// "predictive_state_updates", "agentic_generative_ui").
    /// </summary>
    public string SelectedEndpointPath { get; init; } = DefaultEndpointPath;

    /// <summary>
    /// Whether the agent is currently streaming a response via SSE.
    /// Used by <c>PanicButton</c> and other governance components to show/hide controls.
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// The current author name from the most recent SSE event, or <c>null</c> if not set.
    /// Used for multi-agent visualization (different agent identities produce different
    /// <c>AuthorName</c> values in SSE response events).
    /// </summary>
    public string? CurrentAuthorName { get; init; }
}
