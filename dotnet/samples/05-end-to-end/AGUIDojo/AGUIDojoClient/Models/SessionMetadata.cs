namespace AGUIDojoClient.Models;

/// <summary>
/// Lightweight metadata describing a chat session.
/// </summary>
public sealed record SessionMetadata
{
    /// <summary>The default title assigned to new sessions.</summary>
    public const string DefaultTitle = "New Chat";

    /// <summary>The default AG-UI route used by newly created sessions.</summary>
    public const string DefaultEndpointPath = "chat";

    /// <summary>Creates a durable AG-UI thread identifier for a client-owned session.</summary>
    public static string CreateAguiThreadId() => $"thread_{Guid.NewGuid():N}";

    /// <summary>Gets the unique identifier for the session.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display title for the session.</summary>
    public string Title { get; init; } = DefaultTitle;

    /// <summary>Gets the AG-UI route hint associated with the session.</summary>
    public string EndpointPath { get; init; } = DefaultEndpointPath;

    /// <summary>Gets the preferred model identifier for the session.</summary>
    public string? PreferredModelId { get; init; }

    /// <summary>Gets the current lifecycle status of the session.</summary>
    public SessionStatus Status { get; init; } = SessionStatus.Created;

    /// <summary>Gets when the session was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the session last had activity.</summary>
    public DateTimeOffset LastActivityAt { get; init; }

    /// <summary>Gets the durable AG-UI thread identifier used for /chat continuity.</summary>
    public string? AguiThreadId { get; init; }

    /// <summary>Gets the correlated server-owned chat session identifier, when known.</summary>
    public string? ServerSessionId { get; init; }

    /// <summary>Gets the linked business subject module, when known.</summary>
    public string? SubjectModule { get; init; }

    /// <summary>Gets the linked business subject type, when known.</summary>
    public string? SubjectEntityType { get; init; }

    /// <summary>Gets the linked business subject identifier, when known.</summary>
    public string? SubjectEntityId { get; init; }

    /// <summary>Gets the simulated owner identifier for the session.</summary>
    public string? OwnerId { get; init; }

    /// <summary>Gets the simulated tenant identifier for the session.</summary>
    public string? TenantId { get; init; }

    /// <summary>Gets the linked workflow instance identifier, when known.</summary>
    public string? WorkflowInstanceId { get; init; }

    /// <summary>Gets the linked runtime instance identifier, when known.</summary>
    public string? RuntimeInstanceId { get; init; }

    /// <summary>Gets the number of unread updates for the session.</summary>
    public int UnreadCount { get; init; }

    /// <summary>Gets a value indicating whether the session has a pending approval.</summary>
    public bool HasPendingApproval { get; init; }
}
