// Copyright (c) Microsoft. All rights reserved.

using Fluxor;

namespace AGUIDojoClient.Store.PlanState;

/// <summary>
/// Pure reducer functions for <see cref="PlanState"/>.
/// Each method takes the current state and an action, returning a new immutable state record.
/// </summary>
/// <remarks>
/// Reducers are discovered by Fluxor via assembly scanning (registered in <c>Program.cs</c>).
/// The <see cref="ReducerMethodAttribute"/> marks static methods as reducer handlers.
/// <para>
/// Note: <see cref="PlanActions.ApplyPlanDeltaAction"/> is handled via an effect or
/// in-component logic because <see cref="Services.IJsonPatchApplier"/> mutates the
/// <see cref="Models.Plan"/> object in-place (RFC 6902 patch semantics). The reducer
/// returns the same plan reference after mutation to trigger state change notification.
/// </para>
/// </remarks>
public static class PlanReducers
{
    /// <summary>
    /// Handles <see cref="PlanActions.SetPlanAction"/> by replacing the current plan with a new snapshot.
    /// </summary>
    /// <param name="state">The current plan state.</param>
    /// <param name="action">The action containing the new plan snapshot.</param>
    /// <returns>A new <see cref="PlanState"/> with the updated plan.</returns>
    [ReducerMethod]
    public static PlanState OnSetPlan(PlanState state, PlanActions.SetPlanAction action) =>
        state with { Plan = action.Plan };

    /// <summary>
    /// Handles <see cref="PlanActions.ApplyPlanDeltaAction"/> by signaling that a delta
    /// has been applied to the current plan. The actual JSON Patch mutation is performed
    /// by <see cref="Services.IJsonPatchApplier"/> before dispatching this action.
    /// This reducer creates a new state record to trigger Fluxor change notification.
    /// </summary>
    /// <param name="state">The current plan state (plan has already been mutated in-place).</param>
    /// <param name="action">The action containing the patch operations that were applied.</param>
    /// <returns>A new <see cref="PlanState"/> with the same (mutated) plan reference to trigger re-render.</returns>
    [ReducerMethod]
    public static PlanState OnApplyPlanDelta(PlanState state, PlanActions.ApplyPlanDeltaAction action) =>
        state with { Plan = state.Plan };

    /// <summary>
    /// Handles <see cref="PlanActions.ClearPlanAction"/> by resetting the plan and diff to <c>null</c>.
    /// </summary>
    /// <param name="state">The current plan state.</param>
    /// <param name="action">The clear action.</param>
    /// <returns>A new <see cref="PlanState"/> with both plan and diff set to <c>null</c>.</returns>
    [ReducerMethod]
    public static PlanState OnClearPlan(PlanState state, PlanActions.ClearPlanAction action) =>
        new() { Plan = null, Diff = null };

    /// <summary>
    /// Handles <see cref="PlanActions.SetPlanDiffAction"/> by updating the diff preview state.
    /// </summary>
    /// <param name="state">The current plan state.</param>
    /// <param name="action">The action containing the diff state, or <c>null</c> to clear it.</param>
    /// <returns>A new <see cref="PlanState"/> with the updated diff.</returns>
    [ReducerMethod]
    public static PlanState OnSetPlanDiff(PlanState state, PlanActions.SetPlanDiffAction action) =>
        state with { Diff = action.Diff };
}
