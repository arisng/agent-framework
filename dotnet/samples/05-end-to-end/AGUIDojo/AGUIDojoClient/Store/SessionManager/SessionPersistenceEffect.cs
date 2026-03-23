using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using Fluxor;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Fluxor effects that auto-persist session state to browser storage.
/// Listens to state-changing actions and writes metadata (localStorage)
/// and conversation trees (IndexedDB) via <see cref="ISessionPersistenceService"/>.
/// </summary>
public sealed class SessionPersistenceEffect : IDisposable
{
    private readonly IState<SessionManagerState> _sessionStore;
    private readonly ISessionPersistenceService _persistence;
    private readonly ISessionApiService _sessionApiService;
    private readonly ILogger<SessionPersistenceEffect> _logger;

    /// <summary>Debounce handle for conversation saves.</summary>
    private CancellationTokenSource? _debounceCts;

    /// <summary>Debounce interval for IndexedDB writes.</summary>
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(500);

    public SessionPersistenceEffect(
        IState<SessionManagerState> sessionStore,
        ISessionPersistenceService persistence,
        ISessionApiService sessionApiService,
        ILogger<SessionPersistenceEffect> logger)
    {
        _sessionStore = sessionStore;
        _persistence = persistence;
        _sessionApiService = sessionApiService;
        _logger = logger;
    }

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }

    // =========================================================================
    // Conversation tree persistence (IndexedDB) — debounced
    // =========================================================================

    [EffectMethod]
    public Task OnAddMessage(SessionActions.AddMessageAction action, IDispatcher _)
        => DebouncedSaveConversationAsync(action.SessionId);

    [EffectMethod]
    public Task OnEditAndRegenerate(SessionActions.EditAndRegenerateAction action, IDispatcher _)
        => DebouncedSaveConversationAsync(action.SessionId);

    [EffectMethod]
    public Task OnSwitchBranch(SessionActions.SwitchBranchAction action, IDispatcher _)
        => DebouncedSaveConversationAsync(action.SessionId);

    [EffectMethod]
    public Task OnClearMessages(SessionActions.ClearMessagesAction action, IDispatcher _)
        => DebouncedSaveConversationAsync(action.SessionId);

    [EffectMethod]
    public Task OnTrimMessages(SessionActions.TrimMessagesAction action, IDispatcher _)
        => DebouncedSaveConversationAsync(action.SessionId);

    // =========================================================================
    // Metadata persistence (localStorage) — immediate
    // =========================================================================

    [EffectMethod]
    public Task OnCreateSession(SessionActions.CreateSessionAction action, IDispatcher _)
        => SaveAllMetadataAsync();

    [EffectMethod]
    public Task OnSetSessionTitle(SessionActions.SetSessionTitleAction action, IDispatcher _)
        => SaveAllMetadataAsync();

    [EffectMethod]
    public Task OnSetActiveSession(SessionActions.SetActiveSessionAction action, IDispatcher _)
        => SaveActiveSessionAndMetadataAsync(action.SessionId);

    [EffectMethod]
    public async Task OnArchiveSession(SessionActions.ArchiveSessionAction action, IDispatcher dispatcher)
    {
        await _persistence.DeleteConversationAsync(action.SessionId);
        await SaveAllMetadataAsync();
        // Fire-and-forget: server sync is best-effort. If the browser tab closes before
        // this completes, the server session stays active until a future reconciliation.
        _ = ArchiveSessionOnServerAsync(action.ServerSessionId, action.AguiThreadId);
    }

    [EffectMethod]
    public async Task OnSetRunning(SessionActions.SetRunningAction action, IDispatcher dispatcher)
    {
        // When streaming ends, do a final save of conversation + metadata
        if (!action.IsRunning)
        {
            await Task.WhenAll(
                SaveConversationImmediateAsync(action.SessionId),
                SaveAllMetadataAsync());
            await ReconcileServerSessionAsync(action.SessionId, dispatcher);
            return;
        }

        await SaveAllMetadataAsync();
    }

    [EffectMethod]
    public Task OnSetSessionCorrelation(SessionActions.SetSessionCorrelationAction action, IDispatcher _)
        => SaveAllMetadataAsync();

    [EffectMethod]
    public Task OnHydrateSessions(SessionActions.HydrateSessionsAction action, IDispatcher _)
        => SaveAllMetadataAsync();

    // =========================================================================
    // Private helpers
    // =========================================================================

    private async Task DebouncedSaveConversationAsync(string sessionId)
    {
        // Cancel any pending debounce
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(DebounceInterval, token);
            if (token.IsCancellationRequested) return;
            await SaveConversationImmediateAsync(sessionId);
        }
        catch (TaskCanceledException) { }
    }

    private async Task SaveConversationImmediateAsync(string sessionId)
    {
        if (SessionSelectors.TryGetSession(_sessionStore.Value, sessionId, out SessionEntry entry))
        {
            // Skip persisting trees that only contain a system prompt
            if (entry.State.Tree.Nodes.Count > 1 ||
                (entry.State.Tree.Nodes.Count == 1 &&
                 entry.State.Tree.Nodes.Values.First().Message.Role != ChatRole.System))
            {
                await _persistence.SaveConversationAsync(sessionId, entry.State.Tree);
            }
        }
    }

    private async Task SaveAllMetadataAsync()
    {
        var state = _sessionStore.Value;
        var dtos = state.Sessions.Values
            .Where(e => e.Metadata.Status != SessionStatus.Archived)
            .Select(e => SessionMetadataDto.FromMetadata(e.Metadata))
            .ToList();

        await _persistence.SaveMetadataAsync(dtos);

        if (state.ActiveSessionId is not null)
        {
            await _persistence.SaveActiveSessionIdAsync(state.ActiveSessionId);
        }
    }

    private async Task SaveActiveSessionAndMetadataAsync(string sessionId)
    {
        await _persistence.SaveActiveSessionIdAsync(sessionId);
        await SaveAllMetadataAsync();
    }

    private async Task ArchiveSessionOnServerAsync(string? serverSessionId, string? aguiThreadId)
    {
        try
        {
            string? resolvedServerSessionId = serverSessionId;
            if (string.IsNullOrWhiteSpace(resolvedServerSessionId) && !string.IsNullOrWhiteSpace(aguiThreadId))
            {
                resolvedServerSessionId = await ResolveServerSessionIdAsync(aguiThreadId);
            }

            if (string.IsNullOrWhiteSpace(resolvedServerSessionId))
            {
                return;
            }

            bool archived = await _sessionApiService.ArchiveSessionAsync(resolvedServerSessionId);
            if (!archived)
            {
                _logger.LogWarning("Server archive sync did not succeed for session {ServerSessionId}.", resolvedServerSessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Server archive sync failed for correlated session {ServerSessionId}.", serverSessionId);
        }
    }

    private async Task ReconcileServerSessionAsync(string sessionId, IDispatcher dispatcher)
    {
        if (!SessionSelectors.TryGetSession(_sessionStore.Value, sessionId, out SessionEntry entry))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(entry.Metadata.ServerSessionId) || string.IsNullOrWhiteSpace(entry.Metadata.AguiThreadId))
        {
            return;
        }

        string? resolvedServerSessionId = await ResolveServerSessionIdAsync(entry.Metadata.AguiThreadId);
        if (string.IsNullOrWhiteSpace(resolvedServerSessionId) ||
            string.Equals(resolvedServerSessionId, entry.Metadata.ServerSessionId, StringComparison.Ordinal))
        {
            return;
        }

        dispatcher.Dispatch(new SessionActions.SetSessionCorrelationAction(
            sessionId,
            entry.Metadata.AguiThreadId,
            resolvedServerSessionId));
    }

    private async Task<string?> ResolveServerSessionIdAsync(string aguiThreadId)
    {
        List<ServerSessionSummary>? serverSessions = await _sessionApiService.ListSessionsAsync();
        return serverSessions?
            .FirstOrDefault(session => string.Equals(session.AguiThreadId, aguiThreadId, StringComparison.Ordinal))
            ?.Id;
    }
}
