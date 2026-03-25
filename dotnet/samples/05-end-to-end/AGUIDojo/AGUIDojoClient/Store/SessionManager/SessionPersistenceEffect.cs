using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using Fluxor;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Fluxor effects that keep a browser-local cache of session state.
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
    private CancellationTokenSource? _workspaceDebounceCts;

    /// <summary>Debounce interval for cached IndexedDB writes.</summary>
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
        _workspaceDebounceCts?.Cancel();
        _workspaceDebounceCts?.Dispose();
    }

    // =========================================================================
    // Conversation tree cache (IndexedDB) — debounced
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
    // Metadata cache (localStorage) — immediate
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
            await SyncWorkspaceImmediateAsync(action.SessionId, dispatcher);
            return;
        }

        await SaveAllMetadataAsync();
    }

    [EffectMethod]
    public Task OnSetSessionCorrelation(SessionActions.SetSessionCorrelationAction action, IDispatcher _)
        => SaveAllMetadataAsync();

    [EffectMethod]
    public Task OnSetPreferredModel(SessionActions.SetPreferredModelAction action, IDispatcher _)
        => SaveAllMetadataAsync();

    [EffectMethod]
    public Task OnHydrateSessions(SessionActions.HydrateSessionsAction action, IDispatcher _)
        => SaveAllMetadataAsync();

    [EffectMethod]
    public Task OnSetPendingApproval(SessionActions.SetPendingApprovalAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

    [EffectMethod]
    public Task OnSetPlan(SessionActions.SetPlanAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

    [EffectMethod]
    public Task OnApplyPlanDelta(SessionActions.ApplyPlanDeltaAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

    [EffectMethod]
    public Task OnClearPlan(SessionActions.ClearPlanAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

    [EffectMethod]
    public Task OnSetRecipe(SessionActions.SetRecipeAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

    [EffectMethod]
    public Task OnSetDocument(SessionActions.SetDocumentAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

    [EffectMethod]
    public Task OnSetDocumentPreview(SessionActions.SetDocumentPreviewAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

    [EffectMethod]
    public Task OnClearArtifacts(SessionActions.ClearArtifactsAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

    [EffectMethod]
    public Task OnSetDataGridArtifact(SessionActions.SetDataGridArtifactAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

    [EffectMethod]
    public Task OnAddAuditEntry(SessionActions.AddAuditEntryAction action, IDispatcher dispatcher)
        => DebouncedSyncWorkspaceAsync(action.SessionId, dispatcher);

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

    private async Task DebouncedSyncWorkspaceAsync(string sessionId, IDispatcher dispatcher)
    {
        _workspaceDebounceCts?.Cancel();
        _workspaceDebounceCts = new CancellationTokenSource();
        CancellationToken token = _workspaceDebounceCts.Token;

        try
        {
            await Task.Delay(DebounceInterval, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            await SyncWorkspaceImmediateAsync(sessionId, dispatcher);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task SaveConversationImmediateAsync(string sessionId)
    {
        if (SessionSelectors.TryGetSession(_sessionStore.Value, sessionId, out SessionEntry entry))
        {
            bool hasMeaningfulTree = HasMeaningfulConversation(entry.State.Tree);
            if (hasMeaningfulTree)
            {
                await _persistence.SaveConversationAsync(sessionId, entry.State.Tree);
            }
            else
            {
                await _persistence.DeleteConversationAsync(sessionId);
            }

            string? resolvedServerSessionId = entry.Metadata.ServerSessionId;
            if (string.IsNullOrWhiteSpace(resolvedServerSessionId) && !string.IsNullOrWhiteSpace(entry.Metadata.AguiThreadId))
            {
                resolvedServerSessionId = await ResolveServerSessionIdAsync(entry.Metadata.AguiThreadId);
            }

            if (string.IsNullOrWhiteSpace(resolvedServerSessionId))
            {
                return;
            }

            if (!hasMeaningfulTree)
            {
                await _sessionApiService.ClearConversationAsync(resolvedServerSessionId);
                return;
            }

            if (!entry.State.IsRunning && LooksLikeServerConversationNodeId(entry.State.Tree.ActiveLeafId))
            {
                await _sessionApiService.SetActiveLeafAsync(resolvedServerSessionId, entry.State.Tree.ActiveLeafId!);
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

    private async Task ImportWorkspaceToServerAsync(string sessionId)
    {
        if (!SessionSelectors.TryGetSession(_sessionStore.Value, sessionId, out SessionEntry entry))
        {
            return;
        }

        ServerSessionWorkspaceImportRequest? request = SessionWorkspaceProjection.CreateImportRequest(entry.State);
        if (request is null)
        {
            return;
        }

        string? resolvedServerSessionId = entry.Metadata.ServerSessionId;
        if (string.IsNullOrWhiteSpace(resolvedServerSessionId) && !string.IsNullOrWhiteSpace(entry.Metadata.AguiThreadId))
        {
            resolvedServerSessionId = await ResolveServerSessionIdAsync(entry.Metadata.AguiThreadId);
        }

        if (string.IsNullOrWhiteSpace(resolvedServerSessionId))
        {
            return;
        }

        try
        {
            bool imported = await _sessionApiService.ImportWorkspaceAsync(resolvedServerSessionId, request);
            if (!imported)
            {
                _logger.LogWarning("Workspace import sync did not succeed for session {ServerSessionId}.", resolvedServerSessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Workspace import sync failed for session {ServerSessionId}.", resolvedServerSessionId);
        }
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

    private async Task SyncWorkspaceImmediateAsync(string sessionId, IDispatcher dispatcher)
    {
        if (!SessionSelectors.TryGetSession(_sessionStore.Value, sessionId, out SessionEntry entry))
        {
            return;
        }

        ServerSessionWorkspaceImportRequest? request = SessionWorkspaceProjection.CreateImportRequest(entry.State);
        if (request is null)
        {
            return;
        }

        string? resolvedServerSessionId = entry.Metadata.ServerSessionId;
        if (string.IsNullOrWhiteSpace(resolvedServerSessionId) && !string.IsNullOrWhiteSpace(entry.Metadata.AguiThreadId))
        {
            resolvedServerSessionId = await ResolveServerSessionIdAsync(entry.Metadata.AguiThreadId);
            if (!string.IsNullOrWhiteSpace(resolvedServerSessionId))
            {
                dispatcher.Dispatch(new SessionActions.SetSessionCorrelationAction(
                    sessionId,
                    entry.Metadata.AguiThreadId,
                    resolvedServerSessionId));
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedServerSessionId))
        {
            return;
        }

        bool imported = await _sessionApiService.ImportWorkspaceAsync(resolvedServerSessionId, request);
        if (!imported)
        {
            _logger.LogWarning("Workspace import did not succeed for server session {ServerSessionId}.", resolvedServerSessionId);
        }
    }

    private async Task<string?> ResolveServerSessionIdAsync(string aguiThreadId)
    {
        List<ServerSessionSummary>? serverSessions = await _sessionApiService.ListSessionsAsync();
        return serverSessions?
            .FirstOrDefault(session => string.Equals(session.AguiThreadId, aguiThreadId, StringComparison.Ordinal))
            ?.Id;
    }

    private static bool HasMeaningfulConversation(ConversationTree tree) =>
        tree.Nodes.Count > 1 ||
        (tree.Nodes.Count == 1 && tree.Nodes.Values.First().Message.Role != ChatRole.System);

    private static bool LooksLikeServerConversationNodeId(string? nodeId) =>
        !string.IsNullOrWhiteSpace(nodeId) && nodeId.Length == 32;
}
