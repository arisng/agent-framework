// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;

namespace AGUIDojoClient.Store.PlanState;

/// <summary>
/// Represents the before/after state for diff preview of plan changes.
/// Used by the <see cref="Components.Governance.DiffPreview"/> component to visualize state transitions.
/// </summary>
/// <param name="Before">The plan state before the change.</param>
/// <param name="After">The plan state after the change.</param>
/// <param name="Title">A descriptive title for the diff (e.g., "Plan State Change").</param>
public sealed record DiffState(object? Before, object? After, string Title = "State Diff");

/// <summary>
/// Immutable Fluxor state record for plan management.
/// Holds the current execution plan and optional diff preview state.
/// </summary>
/// <remarks>
/// This state is managed via Fluxor actions and reducers. Components read from
/// <c>IState&lt;PlanState&gt;</c> instead of local fields. The streaming loop in
/// <c>Chat.razor</c> dispatches <see cref="PlanActions.SetPlanAction"/> and
/// <see cref="PlanActions.ApplyPlanDeltaAction"/> as SSE events arrive.
/// The feature is registered via <see cref="PlanFeature"/> which provides the initial state.
/// </remarks>
[SuppressMessage("Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Justification = "Fluxor convention: state record matches folder/namespace name.")]
public sealed record PlanState
{
    /// <summary>
    /// The current execution plan, or <c>null</c> if no plan is active.
    /// </summary>
    public Models.Plan? Plan { get; init; }

    /// <summary>
    /// The diff preview state for before/after comparison, or <c>null</c> if not active.
    /// </summary>
    public DiffState? Diff { get; init; }
}
