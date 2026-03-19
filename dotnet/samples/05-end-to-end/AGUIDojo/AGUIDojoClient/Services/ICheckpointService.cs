// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

namespace AGUIDojoClient.Services;

/// <summary>
/// Interface for in-memory state checkpointing, enabling time-travel / undo
/// functionality in the governance workflow.
/// </summary>
/// <remarks>
/// This service is designed to be used within a single Blazor circuit (Scoped lifetime).
/// It is NOT thread-safe — all calls should occur on the Blazor circuit's synchronization context,
/// which is guaranteed by Blazor Server's single-threaded rendering model.
/// </remarks>
public interface ICheckpointService
{
    /// <summary>
    /// Creates a new checkpoint by serializing the current agent state snapshots to JSON.
    /// Enforces a maximum of 20 checkpoints using FIFO eviction (oldest removed first).
    /// </summary>
    /// <param name="sessionId">The session that owns the checkpoint.</param>
    /// <param name="label">Human-readable label describing the checkpoint (e.g., "Before plan update").</param>
    /// <param name="planState">The current plan state object to serialize, or null if no plan is active.</param>
    /// <param name="recipeState">The current recipe state object to serialize, or null if no recipe is active.</param>
    /// <param name="documentState">The current document state object to serialize, or null if no document is active.</param>
    /// <param name="messageCount">The number of messages in the conversation at this point.</param>
    /// <returns>The created <see cref="Checkpoint"/> so callers can immediately surface undo affordances.</returns>
    Checkpoint CreateCheckpoint(string sessionId, string label, object? planState, object? recipeState, object? documentState, int messageCount);

    /// <summary>
    /// Gets the most recently created checkpoint.
    /// </summary>
    /// <param name="sessionId">The session whose checkpoint history should be queried.</param>
    /// <returns>The latest <see cref="Checkpoint"/>, or null if no checkpoints exist.</returns>
    Checkpoint? GetLatestCheckpoint(string sessionId);

    /// <summary>
    /// Gets all checkpoints ordered by timestamp (oldest first).
    /// </summary>
    /// <param name="sessionId">The session whose checkpoints should be returned.</param>
    /// <returns>A read-only list of all checkpoints.</returns>
    IReadOnlyList<Checkpoint> GetAllCheckpoints(string sessionId);

    /// <summary>
    /// Reverts to a specific checkpoint by ID. Returns the checkpoint for state restoration
    /// and removes all checkpoints that were created after it.
    /// </summary>
    /// <param name="sessionId">The session that owns the checkpoint.</param>
    /// <param name="checkpointId">The unique identifier of the checkpoint to revert to.</param>
    /// <returns>The target <see cref="Checkpoint"/> for state restoration, or null if the ID was not found.</returns>
    Checkpoint? RevertToCheckpoint(string sessionId, string checkpointId);

    /// <summary>
    /// Removes all checkpoints.
    /// </summary>
    /// <param name="sessionId">The session whose checkpoints should be removed.</param>
    void Clear(string sessionId);
}
