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
        var metadataDtos = await _persistence.LoadMetadataAsync();

        // Step 2: Load active session ID from localStorage
        string? activeSessionId = await _persistence.LoadActiveSessionIdAsync();

        // Step 3: Reconstruct SessionEntry objects
        var sessions = new Dictionary<string, SessionEntry>(StringComparer.Ordinal);

        if (metadataDtos is not null)
        {
            foreach (var dto in metadataDtos)
            {
                SessionMetadata metadata = ResetTransientMetadata(dto.ToMetadata());

                // Load conversation tree from IndexedDB
                ConversationTree? tree = await _persistence.LoadConversationAsync(dto.Id);
                var sessionState = new SessionState
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
                foreach (ServerSessionSummary serverSession in serverSessions)
                {
                    if (sessions.ContainsKey(serverSession.Id))
                    {
                        continue;
                    }

                    sessions[serverSession.Id] = CreateServerSessionEntry(serverSession);
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
                UnreadCount = 0,
                HasPendingApproval = false,
            },
            new SessionState());
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
