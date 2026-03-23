using AGUIDojoClient.Models;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Convenience helpers for selecting active and per-session values from the session manager store.
/// </summary>
public static class SessionSelectors
{
    private static SessionEntry CreateFallbackEntry() => new(
        new SessionMetadata
        {
            Id = string.Empty,
            Title = SessionMetadata.DefaultTitle,
            EndpointPath = SessionMetadata.DefaultEndpointPath,
            CreatedAt = DateTimeOffset.MinValue,
            LastActivityAt = DateTimeOffset.MinValue,
        },
        new SessionState());

    public static string GetActiveSessionId(SessionManagerState state)
    {
        if (state.ActiveSessionId is not null)
        {
            return state.ActiveSessionId;
        }

        IReadOnlyList<SessionEntry> orderedSessions = GetOrderedSessions(state);
        return orderedSessions.Count > 0 ? orderedSessions[0].Metadata.Id : string.Empty;
    }

    public static bool TryGetSession(SessionManagerState state, string sessionId, out SessionEntry entry)
    {
        if (state.Sessions.TryGetValue(sessionId, out SessionEntry? resolved))
        {
            entry = resolved;
            return true;
        }

        entry = CreateFallbackEntry();
        return false;
    }

    public static SessionEntry GetActiveSession(SessionManagerState state)
    {
        string activeSessionId = GetActiveSessionId(state);
        return TryGetSession(state, activeSessionId, out SessionEntry entry)
            ? entry
            : CreateFallbackEntry();
    }

    public static IReadOnlyList<SessionEntry> GetOrderedSessions(SessionManagerState state) =>
        state.Sessions.Values
            .OrderByDescending(entry => entry.Metadata.LastActivityAt)
            .ThenByDescending(entry => entry.Metadata.CreatedAt)
            .ToList();

    public static SessionMetadata GetActiveMetadata(SessionManagerState state) => GetActiveSession(state).Metadata;

    public static SessionState GetActiveState(SessionManagerState state) => GetActiveSession(state).State;

    public static SessionState GetSessionStateOrDefault(SessionManagerState state, string sessionId) =>
        TryGetSession(state, sessionId, out SessionEntry entry) ? entry.State : new SessionState();

    public static IReadOnlyList<ChatMessage> ActiveMessages(SessionManagerState state) => GetActiveState(state).Messages;
}
