namespace AGUIDojoClient.Models;

/// <summary>
/// Represents the lifecycle state of a chat session.
/// </summary>
public enum SessionStatus
{
    /// <summary>The session exists but no user activity has started yet.</summary>
    Created,

    /// <summary>The session is the active foreground session and is idle.</summary>
    Active,

    /// <summary>The active foreground session is currently streaming a response.</summary>
    Streaming,

    /// <summary>The session is streaming while another session is active.</summary>
    Background,

    /// <summary>The session has completed its latest response.</summary>
    Completed,

    /// <summary>The session encountered an error.</summary>
    Error,

    /// <summary>The session has been archived and is eligible for removal.</summary>
    Archived,
}
