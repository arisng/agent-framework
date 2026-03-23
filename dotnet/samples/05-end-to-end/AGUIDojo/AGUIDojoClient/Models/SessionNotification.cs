namespace AGUIDojoClient.Models;

/// <summary>
/// Types of session notifications surfaced by the in-app toast layer.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// A background session completed its latest response.
    /// </summary>
    SessionCompleted,

    /// <summary>
    /// A background session is blocked on human approval.
    /// </summary>
    ApprovalRequired,

    /// <summary>
    /// A background session encountered a streaming error.
    /// </summary>
    SessionError,
}

/// <summary>
/// Represents a session-scoped notification rendered by the toast layer.
/// </summary>
public sealed record SessionNotification
{
    /// <summary>
    /// Gets the notification identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the notification type.
    /// </summary>
    public required NotificationType Type { get; init; }

    /// <summary>
    /// Gets the related session identifier.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the toast title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the optional toast detail message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets when the notification was created.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether the notification is urgent.
    /// </summary>
    public bool IsUrgent { get; init; }

    /// <summary>
    /// Gets a value indicating whether the toast stays until explicitly dismissed.
    /// </summary>
    public bool IsPersistent { get; init; }

    /// <summary>
    /// Gets the auto-dismiss duration in milliseconds. Use <c>0</c> for persistent notifications.
    /// </summary>
    public int DurationMilliseconds { get; init; }
}
