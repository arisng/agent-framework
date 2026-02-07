// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents a point-in-time snapshot of agent state for reversibility.
/// Used by the CheckpointManager to enable time-travel / undo functionality.
/// </summary>
public sealed class Checkpoint
{
    /// <summary>
    /// Unique identifier for the checkpoint.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable label describing the checkpoint (e.g., "Before plan update").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the checkpoint was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Serialized JSON snapshot of the plan state at this checkpoint, or null if no plan was active.
    /// </summary>
    public string? PlanSnapshot { get; set; }

    /// <summary>
    /// Serialized JSON snapshot of the recipe state at this checkpoint, or null if no recipe was active.
    /// </summary>
    public string? RecipeSnapshot { get; set; }

    /// <summary>
    /// Serialized JSON snapshot of the document state at this checkpoint, or null if no document was active.
    /// </summary>
    public string? DocumentSnapshot { get; set; }

    /// <summary>
    /// Number of messages in the conversation at this checkpoint, used for context trimming on revert.
    /// </summary>
    public int MessageCount { get; set; }
}
