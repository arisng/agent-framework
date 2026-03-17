// Copyright (c) Microsoft. All rights reserved.

using Fluxor;

namespace AGUIDojoClient.Store.AgentState;

/// <summary>
/// Fluxor feature definition for <see cref="AgentState"/>.
/// Provides the initial state and feature name for Fluxor's assembly scanning registration.
/// </summary>
/// <remarks>
/// Discovered automatically by Fluxor via
/// <c>builder.Services.AddFluxor(o =&gt; o.ScanAssemblies(typeof(Program).Assembly))</c>
/// registered in <c>Program.cs</c>.
/// </remarks>
public sealed class AgentFeature : Feature<AgentState>
{
    /// <summary>
    /// Gets the display name for this feature in Fluxor DevTools.
    /// </summary>
    public override string GetName() => "Agent";

    /// <summary>
    /// Gets the initial state with the default endpoint and no active streaming.
    /// </summary>
    /// <returns>A default <see cref="AgentState"/> with "agentic_chat" endpoint, not running, no author.</returns>
    protected override AgentState GetInitialState() => new()
    {
        SelectedEndpointPath = AgentState.DefaultEndpointPath,
        IsRunning = false,
        CurrentAuthorName = null
    };
}
