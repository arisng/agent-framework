// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

namespace AGUIDojoClient.Services;

/// <summary>
/// Interface for tracking agent tool call execution for observability.
/// Provides timing, status, and result tracking for each tool invocation
/// in the agent's reasoning chain.
/// </summary>
/// <remarks>
/// This service is designed to be used within a single Blazor circuit (Scoped lifetime).
/// It is NOT thread-safe — all calls should occur on the Blazor circuit's synchronization context,
/// which is guaranteed by Blazor Server's single-threaded rendering model.
/// </remarks>
public interface IObservabilityService
{
    /// <summary>
    /// Starts tracking a new tool call. Creates a <see cref="ReasoningStep"/> with
    /// <see cref="ReasoningStepStatus.Running"/> status and records the start time.
    /// </summary>
    /// <param name="callId">The unique identifier for this tool call (typically from the AG-UI protocol).</param>
    /// <param name="toolName">The name of the tool being invoked.</param>
    /// <param name="arguments">The arguments passed to the tool, if any.</param>
    /// <returns>The newly created <see cref="ReasoningStep"/> with Running status.</returns>
    ReasoningStep StartToolCall(string callId, string toolName, IDictionary<string, object?>? arguments);

    /// <summary>
    /// Marks a tracked tool call as completed. Sets the end time, computes duration,
    /// stores the result, and transitions status to <see cref="ReasoningStepStatus.Completed"/>.
    /// </summary>
    /// <param name="callId">The unique identifier of the tool call to complete.</param>
    /// <param name="result">The result returned by the tool.</param>
    void CompleteToolCall(string callId, object? result);

    /// <summary>
    /// Marks a tracked tool call as failed. Sets the end time, stores the error message,
    /// and transitions status to <see cref="ReasoningStepStatus.Failed"/>.
    /// </summary>
    /// <param name="callId">The unique identifier of the tool call that failed.</param>
    /// <param name="error">The error message describing the failure.</param>
    void FailToolCall(string callId, string error);

    /// <summary>
    /// Gets all tracked reasoning steps in the order they were started.
    /// </summary>
    /// <returns>A read-only list of all reasoning steps.</returns>
    IReadOnlyList<ReasoningStep> GetSteps();

    /// <summary>
    /// Gets the currently running reasoning step, if any.
    /// </summary>
    /// <returns>The step with <see cref="ReasoningStepStatus.Running"/> status, or null if no step is active.</returns>
    ReasoningStep? GetActiveStep();
}
