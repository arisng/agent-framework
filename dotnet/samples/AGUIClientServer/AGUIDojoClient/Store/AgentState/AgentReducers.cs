// Copyright (c) Microsoft. All rights reserved.

using Fluxor;

namespace AGUIDojoClient.Store.AgentState;

/// <summary>
/// Pure reducer functions for <see cref="AgentState"/>.
/// Each method takes the current state and an action, returning a new immutable state record.
/// </summary>
/// <remarks>
/// Reducers are discovered by Fluxor via assembly scanning (registered in <c>Program.cs</c>).
/// The <see cref="ReducerMethodAttribute"/> marks static methods as reducer handlers.
/// </remarks>
public static class AgentReducers
{
    /// <summary>
    /// Handles <see cref="AgentActions.SetEndpointAction"/> by updating the selected endpoint path.
    /// </summary>
    /// <param name="state">The current agent state.</param>
    /// <param name="action">The action containing the new endpoint path.</param>
    /// <returns>A new <see cref="AgentState"/> with the updated endpoint path.</returns>
    [ReducerMethod]
    public static AgentState OnSetEndpoint(AgentState state, AgentActions.SetEndpointAction action) =>
        state with { SelectedEndpointPath = action.EndpointPath };

    /// <summary>
    /// Handles <see cref="AgentActions.SetRunningAction"/> by updating the running status.
    /// </summary>
    /// <param name="state">The current agent state.</param>
    /// <param name="action">The action indicating whether the agent is streaming.</param>
    /// <returns>A new <see cref="AgentState"/> with the updated running status.</returns>
    [ReducerMethod]
    public static AgentState OnSetRunning(AgentState state, AgentActions.SetRunningAction action) =>
        state with { IsRunning = action.IsRunning };

    /// <summary>
    /// Handles <see cref="AgentActions.SetAuthorNameAction"/> by updating the current author name.
    /// </summary>
    /// <param name="state">The current agent state.</param>
    /// <param name="action">The action containing the author name from SSE events.</param>
    /// <returns>A new <see cref="AgentState"/> with the updated author name.</returns>
    [ReducerMethod]
    public static AgentState OnSetAuthorName(AgentState state, AgentActions.SetAuthorNameAction action) =>
        state with { CurrentAuthorName = action.AuthorName };
}
