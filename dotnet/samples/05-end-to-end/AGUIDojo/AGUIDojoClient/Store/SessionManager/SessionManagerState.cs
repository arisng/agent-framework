// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Immutable;
using AGUIDojoClient.Models;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Root Fluxor state for multi-session chat management.
/// </summary>
public sealed record SessionManagerState
{
    /// <summary>The maximum number of active sessions retained in memory.</summary>
    public const int MaxActiveSessions = 20;

    /// <summary>The default session identifier used for the initial session.</summary>
    public const string DefaultSessionId = "default-session";

    /// <summary>Gets the session dictionary keyed by session identifier.</summary>
    public ImmutableDictionary<string, SessionEntry> Sessions { get; init; } = ImmutableDictionary<string, SessionEntry>.Empty;

    /// <summary>Gets the currently active session identifier.</summary>
    public string? ActiveSessionId { get; init; }

    /// <summary>Gets the global autonomy level preference (applies to all sessions).</summary>
    public AutonomyLevel AutonomyLevel { get; init; } = AutonomyLevel.Suggest;

    /// <summary>Creates the initial store state with a single default session.</summary>
    public static SessionManagerState CreateInitial()
    {
        SessionEntry entry = CreateSessionEntry(DefaultSessionId);
        return new SessionManagerState
        {
            ActiveSessionId = entry.Metadata.Id,
            Sessions = ImmutableDictionary<string, SessionEntry>.Empty.Add(entry.Metadata.Id, entry),
        };
    }

    /// <summary>Creates a new session entry.</summary>
    public static SessionEntry CreateSessionEntry(
        string sessionId,
        string? title = null,
        string endpointPath = SessionMetadata.DefaultEndpointPath,
        DateTimeOffset? timestamp = null)
    {
        DateTimeOffset instant = timestamp ?? DateTimeOffset.UtcNow;
        return new SessionEntry(
            new SessionMetadata
            {
                Id = sessionId,
                Title = title ?? SessionMetadata.DefaultTitle,
                EndpointPath = endpointPath,
                Status = SessionStatus.Created,
                CreatedAt = instant,
                LastActivityAt = instant,
                UnreadCount = 0,
                HasPendingApproval = false,
            },
            new SessionState());
    }
}
