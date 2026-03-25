using System.Text.Json.Serialization;
using AGUIDojoServer.AgenticUI;
using AGUIDojoServer.PredictiveStateUpdates;
using AGUIDojoServer.SharedState;
using AGUIDojoServer.Tools;

namespace AGUIDojoServer.ChatSessions;

internal sealed record ChatSessionWorkspaceDto
{
    public ChatSessionWorkspaceSnapshotDto? Snapshot { get; init; }

    public List<ChatSessionApprovalRecordDto> Approvals { get; init; } = [];

    public List<ChatSessionAuditEventDto> AuditEvents { get; init; } = [];
}

internal sealed record ChatSessionWorkspaceSnapshotDto
{
    public Plan? CurrentPlan { get; init; }

    public Recipe? CurrentRecipe { get; init; }

    public DocumentState? CurrentDocument { get; init; }

    public string? PreviousDocumentText { get; init; }

    public bool IsDocumentPreview { get; init; }

    public DataGridResult? CurrentDataGrid { get; init; }

    public List<ChatSessionFileReferenceDto> FileReferences { get; init; } = [];

    public DateTimeOffset UpdatedAt { get; init; }
}

internal sealed record ChatSessionFileReferenceDto
{
    public required string AttachmentId { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required string MessageNodeId { get; init; }
}

internal sealed record ChatSessionApprovalRecordDto
{
    public required string ApprovalId { get; init; }

    public required string FunctionName { get; init; }

    public string? Message { get; init; }

    public string? FunctionArgumentsJson { get; init; }

    public string? OriginalCallId { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public string? RequestNodeId { get; init; }

    public string? ResponseNodeId { get; init; }

    public string? ResolutionSource { get; init; }
}

internal sealed record ChatSessionAuditEventDto
{
    public required string Id { get; init; }

    public required string EventType { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string? ApprovalId { get; init; }

    public string? FunctionName { get; init; }

    public string? RiskLevel { get; init; }

    public string? AutonomyLevel { get; init; }

    public bool? WasApproved { get; init; }

    public bool? WasAutoDecided { get; init; }

    public string? PreferredModelId { get; init; }

    public string? EffectiveModelId { get; init; }

    public string? RoutingReason { get; init; }

    public int? InputMessageCount { get; init; }

    public int? OutputMessageCount { get; init; }

    public bool? WasCompacted { get; init; }
}

internal sealed record ChatSessionWorkspaceImportRequest
{
    public ChatSessionWorkspaceImportSnapshotDto? Snapshot { get; init; }

    public ChatSessionPendingApprovalImportDto? PendingApproval { get; init; }

    public List<ChatSessionAuditImportEntryDto> AuditEntries { get; init; } = [];
}

internal sealed record ChatSessionWorkspaceImportSnapshotDto
{
    public Plan? CurrentPlan { get; init; }

    public Recipe? CurrentRecipe { get; init; }

    public DocumentState? CurrentDocument { get; init; }

    public string? PreviousDocumentText { get; init; }

    public bool IsDocumentPreview { get; init; }

    public DataGridResult? CurrentDataGrid { get; init; }

    public List<ChatSessionFileReferenceDto> FileReferences { get; init; } = [];
}

internal sealed record ChatSessionPendingApprovalImportDto
{
    public required string ApprovalId { get; init; }

    public required string FunctionName { get; init; }

    public string? Message { get; init; }

    public string? FunctionArgumentsJson { get; init; }

    public string? OriginalCallId { get; init; }

    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record ChatSessionAuditImportEntryDto
{
    public required string Id { get; init; }

    public required string EventType { get; init; }

    public string? Title { get; init; }

    public string? Summary { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string? ApprovalId { get; init; }

    public string? FunctionName { get; init; }

    public string? RiskLevel { get; init; }

    public string? AutonomyLevel { get; init; }

    public bool? WasApproved { get; init; }

    public bool? WasAutoDecided { get; init; }
}

internal sealed record ChatSessionWorkspaceSummary(
    int ApprovalCount,
    int PendingApprovalCount,
    int AuditEventCount,
    int ArtifactCount,
    int FileReferenceCount,
    string? LatestEffectiveModelId,
    DateTimeOffset? LatestCompactionAt);
