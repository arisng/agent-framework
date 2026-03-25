namespace AGUIDojoServer.ChatSessions;

/// <summary>Summary DTO for session list views.</summary>
public sealed class ChatSessionSummary
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
    public string? SubjectModule { get; init; }
    public string? SubjectEntityType { get; init; }
    public string? SubjectEntityId { get; init; }
    public string? OwnerId { get; init; }
    public string? TenantId { get; init; }
    public string? WorkflowInstanceId { get; init; }
    public string? RuntimeInstanceId { get; init; }
    public string? PreferredModelId { get; init; }
    public string? AguiThreadId { get; init; }
    public required string ServerProtocolVersion { get; init; }

    public int ApprovalCount { get; init; }

    public int PendingApprovalCount { get; init; }

    public int AuditEventCount { get; init; }

    public int ArtifactCount { get; init; }

    public int FileReferenceCount { get; init; }

    public string? LatestEffectiveModelId { get; init; }

    public DateTimeOffset? LatestCompactionAt { get; init; }
}

/// <summary>Detail DTO with full session metadata.</summary>
public sealed class ChatSessionDetail
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
    public string? SubjectModule { get; init; }
    public string? SubjectEntityType { get; init; }
    public string? SubjectEntityId { get; init; }
    public string? OwnerId { get; init; }
    public string? TenantId { get; init; }
    public string? WorkflowInstanceId { get; init; }
    public string? RuntimeInstanceId { get; init; }
    public string? AguiThreadId { get; init; }
    public string? PreferredModelId { get; init; }
    public string? RootMessageId { get; init; }
    public string? ActiveLeafMessageId { get; init; }
    public required string ServerProtocolVersion { get; init; }

    public int ApprovalCount { get; init; }

    public int PendingApprovalCount { get; init; }

    public int AuditEventCount { get; init; }

    public int ArtifactCount { get; init; }

    public int FileReferenceCount { get; init; }

    public string? LatestEffectiveModelId { get; init; }

    public DateTimeOffset? LatestCompactionAt { get; init; }
}
