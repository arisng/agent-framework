// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents a single step in an execution plan.
/// </summary>
public sealed class Step
{
    /// <summary>
    /// Description of what this step does.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// Current status of this step.
    /// </summary>
    [JsonPropertyName("status")]
    public StepStatus Status { get; set; } = StepStatus.Pending;
}
