// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents a JSON Patch operation as defined in RFC 6902.
/// Used for incremental updates to Plan state from the /agentic_generative_ui endpoint.
/// </summary>
public sealed class JsonPatchOperation
{
    /// <summary>
    /// The operation type: "add", "remove", "replace", "move", "copy", or "test".
    /// </summary>
    [JsonPropertyName("op")]
    public required string Op { get; set; }

    /// <summary>
    /// The JSON Pointer path to the target location.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; set; }

    /// <summary>
    /// The value to use for add/replace/test operations.
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    /// <summary>
    /// The source path for move/copy operations.
    /// </summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }
}
