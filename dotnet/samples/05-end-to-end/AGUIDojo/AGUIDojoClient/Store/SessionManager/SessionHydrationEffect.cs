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

    public SessionHydrationEffect(ISessionPersistenceService persistence)
    {
        _persistence = persistence;
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
        if (metadataDtos is null || metadataDtos.Count == 0)
        {
            return; // Nothing persisted — keep default state
        }

        // Step 2: Load active session ID from localStorage
        string? activeSessionId = await _persistence.LoadActiveSessionIdAsync();

        // Step 3: Reconstruct SessionEntry objects
        var sessions = new Dictionary<string, SessionEntry>();

        foreach (var dto in metadataDtos)
        {
            SessionMetadata metadata = dto.ToMetadata();

            // Reset transient statuses to Completed on reload
            metadata = metadata with
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

            // Load conversation tree from IndexedDB
            ConversationTree? tree = await _persistence.LoadConversationAsync(dto.Id);
            var sessionState = new SessionState
            {
                Tree = tree ?? new ConversationTree(),
            };

            sessions[dto.Id] = new SessionEntry(metadata, sessionState);
        }

        if (sessions.Count > 0)
        {
            dispatcher.Dispatch(new SessionActions.HydrateSessionsAction(sessions, activeSessionId));
        }
    }
}
