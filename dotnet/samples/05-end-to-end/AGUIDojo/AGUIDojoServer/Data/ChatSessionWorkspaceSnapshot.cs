namespace AGUIDojoServer.Data;

/// <summary>
/// Stores the current durable workspace projection for a chat session.
/// </summary>
public sealed class ChatSessionWorkspaceSnapshot
{
    public required string SessionId { get; set; }

    public required string SnapshotJson { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Source { get; set; } = WorkspaceSnapshotSources.Derived;

    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
}

public static class WorkspaceSnapshotSources
{
    public const string Derived = "derived";
    public const string BrowserImport = "browser-import";
}
