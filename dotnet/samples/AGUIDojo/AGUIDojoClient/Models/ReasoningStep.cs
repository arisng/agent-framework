// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Models;

/// <summary>
/// Status of a reasoning step during agent execution.
/// </summary>
public enum ReasoningStepStatus
{
    /// <summary>
    /// Step is pending execution.
    /// </summary>
    Pending,

    /// <summary>
    /// Step is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Step has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Step has failed.
    /// </summary>
    Failed
}

/// <summary>
/// Represents a single reasoning step in the agent's chain of thought.
/// Tracks tool call timing, arguments, results, and execution status
/// for observability and transparency in the agent's decision-making process.
/// </summary>
public sealed class ReasoningStep
{
    /// <summary>
    /// Unique identifier for this reasoning step, typically the tool call ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// The sequential number of this step in the reasoning chain.
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// The name of the tool being invoked in this step.
    /// </summary>
    public required string ToolName { get; set; }

    /// <summary>
    /// The arguments passed to the tool, if any.
    /// </summary>
    public IDictionary<string, object?>? Arguments { get; set; }

    /// <summary>
    /// The result returned by the tool after execution.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// The time when this step started execution.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// The time when this step completed execution, if finished.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// The duration of this step's execution.
    /// Computed from <see cref="EndTime"/> minus <see cref="StartTime"/> when both are available.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// The current execution status of this reasoning step.
    /// </summary>
    public ReasoningStepStatus Status { get; set; } = ReasoningStepStatus.Pending;
}
