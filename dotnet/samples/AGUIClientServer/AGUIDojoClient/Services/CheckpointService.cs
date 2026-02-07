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

    private readonly List<Checkpoint> _checkpoints = new();

    /// <inheritdoc />
    public void CreateCheckpoint(string label, object? planState, object? recipeState, object? documentState, int messageCount)
    {
        // Enforce FIFO eviction when at capacity
        while (_checkpoints.Count >= MaxCheckpoints)
        {
            _checkpoints.RemoveAt(0);
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

        _checkpoints.Add(checkpoint);
    }

    /// <inheritdoc />
    public Checkpoint? GetLatestCheckpoint()
    {
        return _checkpoints.Count > 0 ? _checkpoints[^1] : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<Checkpoint> GetAllCheckpoints()
    {
        return _checkpoints.AsReadOnly();
    }

    /// <inheritdoc />
    public Checkpoint? RevertToCheckpoint(string checkpointId)
    {
        var index = _checkpoints.FindIndex(c => c.Id == checkpointId);
        if (index < 0)
        {
            return null;
        }

        var target = _checkpoints[index];

        // Remove all checkpoints newer than the target
        if (index + 1 < _checkpoints.Count)
        {
            _checkpoints.RemoveRange(index + 1, _checkpoints.Count - index - 1);
        }

        return target;
    }

    /// <inheritdoc />
    public void Clear()
    {
        _checkpoints.Clear();
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
}
