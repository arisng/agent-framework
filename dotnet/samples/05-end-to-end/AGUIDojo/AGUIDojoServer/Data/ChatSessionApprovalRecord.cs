namespace AGUIDojoServer.Data;

/// <summary>
/// Durable approval record reconstructed from canonical conversation state or best-effort browser import.
/// </summary>
public sealed class ChatSessionApprovalRecord
{
    public required string SessionId { get; set; }

    public required string ApprovalId { get; set; }

    public required string FunctionName { get; set; }

    public string? Message { get; set; }

    public string? FunctionArgumentsJson { get; set; }

    public string? OriginalCallId { get; set; }

    public ChatApprovalStatus Status { get; set; } = ChatApprovalStatus.Pending;

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? RequestNodeId { get; set; }

    public string? ResponseNodeId { get; set; }

    public string? ResolutionSource { get; set; }

    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
}

public enum ChatApprovalStatus
{
    Pending,
    Approved,
    Rejected,
}
