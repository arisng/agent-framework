// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Models;

/// <summary>
/// Snapshot of SSE stream performance metrics for the current or most recent stream.
/// Exposed via <see cref="Services.IAgentStreamingService"/> for UI rendering
/// and recorded as OpenTelemetry metrics for production observability.
/// </summary>
public sealed record SseStreamSnapshot
{
    /// <summary>Time from request start to first content event in milliseconds, or null if not yet received.</summary>
    public double? FirstTokenLatencyMs { get; init; }

    /// <summary>Total events received in the current stream.</summary>
    public int EventCount { get; init; }

    /// <summary>Current stream duration in milliseconds.</summary>
    public double DurationMs { get; init; }

    /// <summary>Number of retry attempts made for this stream.</summary>
    public int RetryCount { get; init; }

    /// <summary>Current state of the SSE connection.</summary>
    public SseConnectionState ConnectionState { get; init; }
}

/// <summary>
/// Represents the current state of the SSE connection lifecycle.
/// </summary>
public enum SseConnectionState
{
    /// <summary>No active stream.</summary>
    Idle,

    /// <summary>Establishing connection to the server.</summary>
    Connecting,

    /// <summary>Actively receiving SSE events.</summary>
    Streaming,

    /// <summary>Retrying after a connection failure.</summary>
    Retrying,

    /// <summary>Stream completed successfully.</summary>
    Completed,

    /// <summary>Stream ended with an error.</summary>
    Error,
}
