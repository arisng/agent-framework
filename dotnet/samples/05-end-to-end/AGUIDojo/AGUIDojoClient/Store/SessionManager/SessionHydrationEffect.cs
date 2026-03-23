using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using Fluxor;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Fluxor effect that hydrates session state from browser persistence on app start.
/// Triggered once from <c>Chat.razor.OnAfterRenderAsync(firstRender: true)</c>
/// via <see cref="SessionActions.HydrateFromStorageAction"/>.
/// </summary>
public sealed class SessionHydrationEffect
{
    private readonly ISessionPersistenceService _persistence;
    private readonly ISessionApiService _sessionApiService;
    private readonly ILogger<SessionHydrationEffect> _logger;

    public SessionHydrationEffect(
        ISessionPersistenceService persistence,
        ISessionApiService sessionApiService,
        ILogger<SessionHydrationEffect> logger)
    {
        _persistence = persistence;
        _sessionApiService = sessionApiService;
        _logger = logger;
    }

    /// <summary>
    /// Loads metadata from localStorage and conversation trees from IndexedDB,
    /// then dispatches <see cref="SessionActions.HydrateSessionsAction"/> to restore state.
    /// </summary>
    [EffectMethod]
    public async Task HandleHydrateFromStorage(SessionActions.HydrateFromStorageAction _, IDispatcher dispatcher)
    {
        // Step 1: Load metadata from localStorage
        List<SessionMetadataDto>? metadataDtos = await _persistence.LoadMetadataAsync();

        // Step 2: Load active session ID from localStorage
        string? activeSessionId = await _persistence.LoadActiveSessionIdAsync();

        // Step 3: Reconstruct SessionEntry objects
        Dictionary<string, SessionEntry> sessions = new(StringComparer.Ordinal);

        if (metadataDtos is not null)
        {
            foreach (SessionMetadataDto dto in metadataDtos)
            {
                SessionMetadata metadata = ResetTransientMetadata(dto.ToMetadata());

                // Load conversation tree from IndexedDB
                ConversationTree? tree = await _persistence.LoadConversationAsync(dto.Id);
                SessionState sessionState = new()
                {
                    Tree = tree ?? new ConversationTree(),
                };

                sessions[dto.Id] = new SessionEntry(metadata, sessionState);
            }
        }

        // Step 4: Merge in server-owned sessions that do not exist in browser storage
        try
        {
            List<ServerSessionSummary>? serverSessions = await _sessionApiService.ListSessionsAsync();
            if (serverSessions is not null)
            {
                Dictionary<string, string> localSessionIdsByServerSessionId = BuildLocalSessionIdsByServerSessionId(sessions);
                Dictionary<string, string> localSessionIdsByThreadId = BuildLocalSessionIdsByThreadId(sessions);

                foreach (ServerSessionSummary serverSession in serverSessions)
                {
                    string? localSessionId = FindCorrelatedLocalSessionId(
                        sessions,
                        localSessionIdsByServerSessionId,
                        localSessionIdsByThreadId,
                        serverSession);

                    if (localSessionId is not null)
                    {
                        SessionEntry mergedEntry = MergeCorrelatedSession(sessions[localSessionId], serverSession);
                        sessions[localSessionId] = mergedEntry;
                        UpdateCorrelationIndexes(localSessionIdsByServerSessionId, localSessionIdsByThreadId, localSessionId, mergedEntry.Metadata);
                        continue;
                    }

                    SessionEntry serverEntry = CreateServerSessionEntry(serverSession);
                    sessions[serverSession.Id] = serverEntry;
                    UpdateCorrelationIndexes(localSessionIdsByServerSessionId, localSessionIdsByThreadId, serverSession.Id, serverEntry.Metadata);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load server session list during hydration. Falling back to browser storage only.");
        }

        if (sessions.Count > 0)
        {
            dispatcher.Dispatch(new SessionActions.HydrateSessionsAction(sessions, activeSessionId));
        }
    }

    private static SessionMetadata ResetTransientMetadata(SessionMetadata metadata) => metadata with
    {
        Status = metadata.Status switch
        {
            SessionStatus.Streaming => SessionStatus.Completed,
            SessionStatus.Background => SessionStatus.Completed,
            SessionStatus.Active => SessionStatus.Completed,
            _ => metadata.Status,
        },
        UnreadCount = 0,
        HasPendingApproval = false,
    };

    private static SessionEntry CreateServerSessionEntry(ServerSessionSummary serverSession)
    {
        DateTimeOffset createdAt = serverSession.CreatedAt == default ? DateTimeOffset.UtcNow : serverSession.CreatedAt;
        DateTimeOffset lastActivityAt = serverSession.LastActivityAt == default ? createdAt : serverSession.LastActivityAt;

        return new SessionEntry(
            new SessionMetadata
            {
                Id = serverSession.Id,
                Title = string.IsNullOrWhiteSpace(serverSession.Title) ? SessionMetadata.DefaultTitle : serverSession.Title,
                EndpointPath = SessionMetadata.DefaultEndpointPath,
                Status = MapServerStatus(serverSession.Status),
                CreatedAt = createdAt,
                LastActivityAt = lastActivityAt,
                AguiThreadId = serverSession.AguiThreadId,
                ServerSessionId = serverSession.Id,
                UnreadCount = 0,
                HasPendingApproval = false,
            },
            new SessionState());
    }

    private static SessionEntry MergeCorrelatedSession(SessionEntry localEntry, ServerSessionSummary serverSession)
    {
        SessionEntry serverEntry = CreateServerSessionEntry(serverSession);
        DateTimeOffset mergedCreatedAt = GetEarlierTimestamp(localEntry.Metadata.CreatedAt, serverEntry.Metadata.CreatedAt);
        DateTimeOffset mergedLastActivityAt = GetLaterTimestamp(localEntry.Metadata.LastActivityAt, serverEntry.Metadata.LastActivityAt);

        return localEntry with
        {
            Metadata = localEntry.Metadata with
            {
                Title = ResolveMergedTitle(localEntry.Metadata.Title, serverEntry.Metadata.Title),
                Status = serverEntry.Metadata.Status,
                CreatedAt = mergedCreatedAt,
                LastActivityAt = mergedLastActivityAt,
                AguiThreadId = !string.IsNullOrWhiteSpace(localEntry.Metadata.AguiThreadId)
                    ? localEntry.Metadata.AguiThreadId
                    : serverEntry.Metadata.AguiThreadId,
                ServerSessionId = serverSession.Id,
                UnreadCount = 0,
                HasPendingApproval = false,
            },
        };
    }

    private static Dictionary<string, string> BuildLocalSessionIdsByServerSessionId(IReadOnlyDictionary<string, SessionEntry> sessions)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach ((string sessionId, SessionEntry entry) in sessions)
        {
            if (!string.IsNullOrWhiteSpace(entry.Metadata.ServerSessionId))
            {
                map[entry.Metadata.ServerSessionId] = sessionId;
            }
        }

        return map;
    }

    private static Dictionary<string, string> BuildLocalSessionIdsByThreadId(IReadOnlyDictionary<string, SessionEntry> sessions)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach ((string sessionId, SessionEntry entry) in sessions)
        {
            if (!string.IsNullOrWhiteSpace(entry.Metadata.AguiThreadId))
            {
                map[entry.Metadata.AguiThreadId] = sessionId;
            }
        }

        return map;
    }

    private static string? FindCorrelatedLocalSessionId(
        Dictionary<string, SessionEntry> sessions,
        Dictionary<string, string> localSessionIdsByServerSessionId,
        Dictionary<string, string> localSessionIdsByThreadId,
        ServerSessionSummary serverSession)
    {
        if (sessions.ContainsKey(serverSession.Id))
        {
            return serverSession.Id;
        }

        if (localSessionIdsByServerSessionId.TryGetValue(serverSession.Id, out string? sessionIdByServerSessionId))
        {
            return sessionIdByServerSessionId;
        }

        if (!string.IsNullOrWhiteSpace(serverSession.AguiThreadId) &&
            localSessionIdsByThreadId.TryGetValue(serverSession.AguiThreadId, out string? sessionIdByThreadId))
        {
            return sessionIdByThreadId;
        }

        return null;
    }

    private static void UpdateCorrelationIndexes(
        Dictionary<string, string> localSessionIdsByServerSessionId,
        Dictionary<string, string> localSessionIdsByThreadId,
        string sessionId,
        SessionMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.ServerSessionId))
        {
            localSessionIdsByServerSessionId[metadata.ServerSessionId] = sessionId;
        }

        if (!string.IsNullOrWhiteSpace(metadata.AguiThreadId))
        {
            localSessionIdsByThreadId[metadata.AguiThreadId] = sessionId;
        }
    }

    private static DateTimeOffset GetEarlierTimestamp(DateTimeOffset left, DateTimeOffset right)
    {
        if (left == default)
        {
            return right;
        }

        if (right == default)
        {
            return left;
        }

        return left <= right ? left : right;
    }

    private static DateTimeOffset GetLaterTimestamp(DateTimeOffset left, DateTimeOffset right)
    {
        if (left == default)
        {
            return right;
        }

        if (right == default)
        {
            return left;
        }

        return left >= right ? left : right;
    }

    private static string ResolveMergedTitle(string localTitle, string serverTitle)
    {
        bool localHasMeaningfulTitle = !string.IsNullOrWhiteSpace(localTitle) &&
            !string.Equals(localTitle, SessionMetadata.DefaultTitle, StringComparison.Ordinal);
        bool serverHasMeaningfulTitle = !string.IsNullOrWhiteSpace(serverTitle) &&
            !string.Equals(serverTitle, SessionMetadata.DefaultTitle, StringComparison.Ordinal);

        if (localHasMeaningfulTitle)
        {
            return localTitle;
        }

        if (serverHasMeaningfulTitle)
        {
            return serverTitle;
        }

        if (!string.IsNullOrWhiteSpace(localTitle))
        {
            return localTitle;
        }

        return string.IsNullOrWhiteSpace(serverTitle) ? SessionMetadata.DefaultTitle : serverTitle;
    }

    private static SessionStatus MapServerStatus(string? status)
    {
        if (Enum.TryParse<SessionStatus>(status, ignoreCase: true, out SessionStatus parsedStatus))
        {
            return parsedStatus switch
            {
                SessionStatus.Archived => SessionStatus.Archived,
                SessionStatus.Error => SessionStatus.Error,
                _ => SessionStatus.Completed,
            };
        }

        return SessionStatus.Completed;
    }
}
