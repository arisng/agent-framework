namespace AGUIDojoServer.Data;

/// <summary>
/// Represents a server-owned chat session with lifecycle and subject metadata.
/// </summary>
public sealed class ChatSession
{
    /// <summary>Server-issued unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>User-facing session title (auto-generated or user-set).</summary>
    public string? Title { get; set; }

    /// <summary>Session lifecycle status.</summary>
    public ChatSessionStatus Status { get; set; } = ChatSessionStatus.Active;

    /// <summary>When the session was first created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When the session last had activity.</summary>
    public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When the session was archived, if applicable.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>Canonical root node ID for the persisted conversation graph.</summary>
    public string? RootMessageId { get; set; }

    /// <summary>Canonical active leaf node ID for the persisted conversation graph.</summary>
    public string? ActiveLeafMessageId { get; set; }

    /// <summary>The business module that owns the subject (e.g. "Todo").</summary>
    public string? SubjectModule { get; set; }

    /// <summary>The business entity ID within the subject module.</summary>
    public string? SubjectEntityId { get; set; }

    /// <summary>The business entity type within the subject module.</summary>
    public string? SubjectEntityType { get; set; }

    /// <summary>AG-UI thread correlation ID (runtime reference, not primary key).</summary>
    public string? AguiThreadId { get; set; }

    /// <summary>User's preferred model for this session (for future model picker).</summary>
    public string? PreferredModelId { get; set; }

    /// <summary>Server-owned protocol/version marker for thin recovery and support metadata.</summary>
    public string ServerProtocolVersion { get; set; } = ChatSessionProtocolVersions.Current;

    /// <summary>Optimistic concurrency token (GUID string, SQLite-compatible).</summary>
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// Well-known protocol versions for the server-owned chat-session contract.
/// </summary>
public static class ChatSessionProtocolVersions
{
    public const string Current = "agui-chat-session.v1";
}

/// <summary>
/// Session lifecycle status values.
/// </summary>
public enum ChatSessionStatus
{
    /// <summary>Session is active and accepting new turns.</summary>
    Active,

    /// <summary>Session has been archived by the user.</summary>
    Archived,
}
