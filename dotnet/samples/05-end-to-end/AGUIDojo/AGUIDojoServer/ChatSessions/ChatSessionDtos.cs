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
    public string? SubjectEntityId { get; init; }
    public string? PreferredModelId { get; init; }
    public string? AguiThreadId { get; init; }
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
    public string? SubjectEntityId { get; init; }
    public string? AguiThreadId { get; init; }
    public string? PreferredModelId { get; init; }
}
