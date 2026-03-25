namespace AGUIDojoServer.Data;

/// <summary>
/// Structured session audit fact persisted for support, routing diagnostics, and governance inspection.
/// </summary>
public sealed class ChatSessionAuditEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public required string SessionId { get; set; }

    public ChatAuditEventType EventType { get; set; }

    public required string Title { get; set; }

    public string? Summary { get; set; }

    public string? DataJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public string? RelatedNodeId { get; set; }

    public string? CorrelationId { get; set; }
}

public enum ChatAuditEventType
{
    ApprovalRequested,
    ApprovalResolved,
    ToolCall,
    ToolResult,
    ModelRouting,
    CompactionCheckpoint,
    WorkspaceImport,
}
