// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUIDojoClient.Models;

namespace AGUIDojoClient.Services;

/// <summary>
/// In-memory implementation of <see cref="ICheckpointService"/> for state checkpointing.
/// Stores up to <see cref="MaxCheckpoints"/> snapshots with FIFO eviction.
/// </summary>
/// <remarks>
/// This service is Scoped (one instance per Blazor circuit) and is NOT thread-safe.
/// All calls are expected on the Blazor circuit's synchronization context.
/// </remarks>
public sealed class CheckpointService : ICheckpointService
{
    /// <summary>
    /// Maximum number of checkpoints retained before FIFO eviction.
    /// </summary>
    private const int MaxCheckpoints = 20;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true, // Human-readable for DiffPreview / debugging
    };

    private readonly Dictionary<string, List<Checkpoint>> _checkpointsBySession = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void CreateCheckpoint(string sessionId, string label, object? planState, object? recipeState, object? documentState, int messageCount)
    {
        List<Checkpoint> checkpoints = GetOrCreateCheckpoints(sessionId);

        // Enforce FIFO eviction when at capacity
        while (checkpoints.Count >= MaxCheckpoints)
        {
            checkpoints.RemoveAt(0);
        }

        var checkpoint = new Checkpoint
        {
            Id = Guid.NewGuid().ToString("N"),
            Label = label,
            Timestamp = DateTime.UtcNow,
            PlanSnapshot = SerializeState(planState),
            RecipeSnapshot = SerializeState(recipeState),
            DocumentSnapshot = SerializeState(documentState),
            MessageCount = messageCount,
        };

        checkpoints.Add(checkpoint);
    }

    /// <inheritdoc />
    public Checkpoint? GetLatestCheckpoint(string sessionId)
    {
        List<Checkpoint> checkpoints = GetOrCreateCheckpoints(sessionId);
        return checkpoints.Count > 0 ? checkpoints[^1] : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<Checkpoint> GetAllCheckpoints(string sessionId)
    {
        return GetOrCreateCheckpoints(sessionId).AsReadOnly();
    }

    /// <inheritdoc />
    public Checkpoint? RevertToCheckpoint(string sessionId, string checkpointId)
    {
        List<Checkpoint> checkpoints = GetOrCreateCheckpoints(sessionId);
        var index = checkpoints.FindIndex(c => c.Id == checkpointId);
        if (index < 0)
        {
            return null;
        }

        var target = checkpoints[index];

        // Remove all checkpoints newer than the target
        if (index + 1 < checkpoints.Count)
        {
            checkpoints.RemoveRange(index + 1, checkpoints.Count - index - 1);
        }

        return target;
    }

    /// <inheritdoc />
    public void Clear(string sessionId)
    {
        _checkpointsBySession.Remove(sessionId);
    }

    /// <summary>
    /// Serializes an object to pretty-printed JSON, or returns null if the input is null.
    /// </summary>
    private static string? SerializeState(object? state)
    {
        if (state is null)
        {
            return null;
        }

        // If the state is already a string (e.g., raw JSON), return as-is
        if (state is string s)
        {
            return s;
        }

        return JsonSerializer.Serialize(state, s_jsonOptions);
    }

    private List<Checkpoint> GetOrCreateCheckpoints(string sessionId)
    {
        if (!_checkpointsBySession.TryGetValue(sessionId, out List<Checkpoint>? checkpoints))
        {
            checkpoints = [];
            _checkpointsBySession[sessionId] = checkpoints;
        }

        return checkpoints;
    }
}
