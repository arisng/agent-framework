// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents document state for predictive state updates.
/// Used by the /predictive_state_updates endpoint to stream progressive document content.
/// </summary>
public sealed class DocumentState
{
    /// <summary>
    /// The markdown document content being streamed.
    /// </summary>
    [JsonPropertyName("document")]
    public string Document { get; set; } = string.Empty;
}
