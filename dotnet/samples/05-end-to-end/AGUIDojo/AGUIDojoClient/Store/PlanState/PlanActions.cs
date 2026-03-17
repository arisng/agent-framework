// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

namespace AGUIDojoClient.Store.PlanState;

/// <summary>
/// Fluxor actions for plan state management.
/// Dispatched from the SSE streaming loop in <c>Chat.razor</c> when plan-related
/// data content events are received from the AG-UI SSE stream.
/// </summary>
public static class PlanActions
{
    /// <summary>
    /// Sets the current plan to a new full snapshot.
    /// Dispatched when a <c>STATE_SNAPSHOT</c> event containing a plan is received.
    /// </summary>
    /// <param name="Plan">The new plan snapshot to set as current.</param>
    public sealed record SetPlanAction(Plan Plan);

    /// <summary>
    /// Applies a JSON Patch delta to the current plan.
    /// Dispatched when a <c>STATE_DELTA</c> event with patch operations is received.
    /// </summary>
    /// <param name="Operations">The RFC 6902 JSON Patch operations to apply.</param>
    public sealed record ApplyPlanDeltaAction(IEnumerable<JsonPatchOperation> Operations);

    /// <summary>
    /// Clears the current plan state.
    /// Dispatched when the conversation is reset or a new session starts.
    /// </summary>
    public sealed record ClearPlanAction;

    /// <summary>
    /// Sets the diff preview state for before/after comparison.
    /// Dispatched before a plan change to capture the before state for
    /// the <see cref="Components.Governance.DiffPreview"/> component.
    /// </summary>
    /// <param name="Diff">The diff state containing before/after snapshots and title.</param>
    public sealed record SetPlanDiffAction(DiffState? Diff);
}
