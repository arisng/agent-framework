// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

namespace AGUIDojoClient.Services;

/// <summary>
/// Tracks agent tool call execution for observability and transparency.
/// Maintains an ordered collection of reasoning steps with timing, status, and results.
/// </summary>
/// <remarks>
/// <para>
/// This service operates within a single Blazor circuit (registered as Scoped).
/// It is NOT thread-safe — all calls are expected to occur on the Blazor circuit's
/// synchronization context, which is guaranteed by Blazor Server's single-threaded
/// rendering model.
/// </para>
/// <para>
/// Tool calls are tracked by their <c>callId</c> (typically the tool call ID from
/// the AG-UI protocol's <c>FunctionCallContent</c>). The service automatically
/// assigns sequential step numbers and computes durations.
/// </para>
/// </remarks>
public sealed class ObservabilityService : IObservabilityService
{
    private readonly Dictionary<string, ReasoningStep> _steps = new();
    private readonly List<ReasoningStep> _orderedSteps = [];
    private int _stepCounter;

    /// <inheritdoc />
    public ReasoningStep StartToolCall(string callId, string toolName, IDictionary<string, object?>? arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var step = new ReasoningStep
        {
            Id = callId,
            StepNumber = ++_stepCounter,
            ToolName = toolName,
            Arguments = arguments,
            StartTime = DateTime.UtcNow,
            Status = ReasoningStepStatus.Running
        };

        _steps[callId] = step;
        _orderedSteps.Add(step);

        return step;
    }

    /// <inheritdoc />
    public void CompleteToolCall(string callId, object? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);

        if (_steps.TryGetValue(callId, out var step))
        {
            step.EndTime = DateTime.UtcNow;
            step.Result = result;
            step.Status = ReasoningStepStatus.Completed;
        }
    }

    /// <inheritdoc />
    public void FailToolCall(string callId, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);

        if (_steps.TryGetValue(callId, out var step))
        {
            step.EndTime = DateTime.UtcNow;
            step.Result = error;
            step.Status = ReasoningStepStatus.Failed;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ReasoningStep> GetSteps() => _orderedSteps.AsReadOnly();

    /// <inheritdoc />
    public ReasoningStep? GetActiveStep() => _orderedSteps.FindLast(s => s.Status == ReasoningStepStatus.Running);
}
