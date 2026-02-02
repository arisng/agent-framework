// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents an execution plan with a collection of steps.
/// Used by the /agentic_generative_ui endpoint to display plan progress.
/// </summary>
public sealed class Plan
{
    /// <summary>
    /// The steps in the plan.
    /// </summary>
    [JsonPropertyName("steps")]
    public List<Step> Steps { get; set; } = [];
}
