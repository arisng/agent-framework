// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Status of a step in an execution plan.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StepStatus>))]
public enum StepStatus
{
    /// <summary>
    /// Step is pending execution.
    /// </summary>
    Pending,

    /// <summary>
    /// Step has been completed.
    /// </summary>
    Completed
}
