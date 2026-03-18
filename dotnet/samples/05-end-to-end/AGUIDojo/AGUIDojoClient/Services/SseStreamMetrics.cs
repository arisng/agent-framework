// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.Metrics;

namespace AGUIDojoClient.Services;

/// <summary>
/// OpenTelemetry metrics for SSE streaming performance and reliability.
/// All instruments use the <c>AGUIDojoClient.SSE</c> meter name and follow
/// the OpenTelemetry semantic conventions for naming and units.
/// </summary>
/// <remarks>
/// Register the meter in DI with <c>.AddMeter(SseStreamMetrics.MeterName)</c>
/// on the OpenTelemetry metrics builder.
/// </remarks>
public static class SseStreamMetrics
{
    /// <summary>The meter name registered with OpenTelemetry.</summary>
    public const string MeterName = "AGUIDojoClient.SSE";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>Time from request start to first content event (milliseconds).</summary>
    public static readonly Histogram<double> FirstTokenLatency =
        Meter.CreateHistogram<double>(
            "agui.sse.first_token_latency",
            unit: "ms",
            description: "Time from stream request to first content event");

    /// <summary>Total stream duration (milliseconds).</summary>
    public static readonly Histogram<double> StreamDuration =
        Meter.CreateHistogram<double>(
            "agui.sse.stream_duration",
            unit: "ms",
            description: "Total SSE stream duration");

    /// <summary>Number of SSE events received per stream.</summary>
    public static readonly Histogram<int> EventsPerStream =
        Meter.CreateHistogram<int>(
            "agui.sse.events_per_stream",
            unit: "{events}",
            description: "SSE events received per stream");

    /// <summary>Total streams started.</summary>
    public static readonly Counter<long> StreamsStarted =
        Meter.CreateCounter<long>(
            "agui.sse.streams_started",
            unit: "{streams}",
            description: "Total SSE streams initiated");

    /// <summary>Total streams completed by outcome.</summary>
    public static readonly Counter<long> StreamsCompleted =
        Meter.CreateCounter<long>(
            "agui.sse.streams_completed",
            unit: "{streams}",
            description: "Total SSE streams completed by outcome");

    /// <summary>Total auto-retry attempts.</summary>
    public static readonly Counter<long> RetryAttempts =
        Meter.CreateCounter<long>(
            "agui.sse.retry_attempts",
            unit: "{retries}",
            description: "Auto-retry attempts on connection failure");
}
