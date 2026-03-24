namespace AGUIDojoServer.Data;

/// <summary>
/// Represents one durable message node in the server-owned branching conversation graph.
/// </summary>
public sealed class ChatConversationNode
{
    /// <summary>Owning chat session ID.</summary>
    public required string SessionId { get; set; }

    /// <summary>Server-issued canonical node ID.</summary>
    public required string NodeId { get; set; }

    /// <summary>Parent node ID, or null when this node is the root.</summary>
    public string? ParentNodeId { get; set; }

    /// <summary>Stable sibling order among nodes with the same parent.</summary>
    public int SiblingOrder { get; set; }

    /// <summary>Runtime message identifier retained as metadata only.</summary>
    public string? RuntimeMessageId { get; set; }

    /// <summary>Message role such as user, assistant, tool, or system.</summary>
    public required string Role { get; set; }

    /// <summary>Optional author label displayed in the UI.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Primary text payload for the message.</summary>
    public string? Text { get; set; }

    /// <summary>Serialized content payload required for richer rehydration.</summary>
    public string? ContentJson { get; set; }

    /// <summary>Serialized additional properties required for rich rehydration.</summary>
    public string? AdditionalPropertiesJson { get; set; }

    /// <summary>When the node was originally created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
