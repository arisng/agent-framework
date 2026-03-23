using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents document state for predictive updates streamed through the unified chat route.
/// </summary>
public sealed class DocumentState
{
    /// <summary>
    /// The markdown document content being streamed.
    /// </summary>
    [JsonPropertyName("document")]
    public string Document { get; set; } = string.Empty;
}
