// Copyright (c) Microsoft. All rights reserved.

using Fluxor;

namespace AGUIDojoClient.Store.PlanState;

/// <summary>
/// Fluxor feature definition for <see cref="PlanState"/>.
/// Provides the initial state and feature name for Fluxor's assembly scanning registration.
/// </summary>
/// <remarks>
/// Discovered automatically by Fluxor via
/// <c>builder.Services.AddFluxor(o =&gt; o.ScanAssemblies(typeof(Program).Assembly))</c>
/// registered in <c>Program.cs</c>.
/// </remarks>
public sealed class PlanFeature : Feature<PlanState>
{
    /// <summary>
    /// Gets the display name for this feature in Fluxor DevTools.
    /// </summary>
    public override string GetName() => "Plan";

    /// <summary>
    /// Gets the initial state with no active plan and no diff preview.
    /// </summary>
    /// <returns>A default <see cref="PlanState"/> with all properties set to <c>null</c>.</returns>
    protected override PlanState GetInitialState() => new()
    {
        Plan = null,
        Diff = null
    };
}
