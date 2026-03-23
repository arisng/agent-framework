using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents Mermaid diagram content returned by a tool such as <c>show_mermaid</c>.
/// </summary>
public sealed class MermaidResult
{
    /// <summary>
    /// A human-readable title for the diagram.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = "Diagram";

    /// <summary>
    /// Optional description shown above the diagram.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// The Mermaid definition text.
    /// </summary>
    [JsonPropertyName("definition")]
    public string Definition { get; init; } = string.Empty;
}
