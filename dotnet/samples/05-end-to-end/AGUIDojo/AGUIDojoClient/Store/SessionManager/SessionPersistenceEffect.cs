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

    /// <summary>Debounce handle for conversation saves.</summary>
    private CancellationTokenSource? _debounceCts;

    /// <summary>Debounce interval for IndexedDB writes.</summary>
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(500);

    public SessionPersistenceEffect(IState<SessionManagerState> sessionStore, ISessionPersistenceService persistence)
    {
        _sessionStore = sessionStore;
        _persistence = persistence;
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
    public async Task OnArchiveSession(SessionActions.ArchiveSessionAction action, IDispatcher _)
    {
        await _persistence.DeleteConversationAsync(action.SessionId);
        await SaveAllMetadataAsync();
    }

    [EffectMethod]
    public Task OnSetRunning(SessionActions.SetRunningAction action, IDispatcher _)
    {
        // When streaming ends, do a final save of conversation + metadata
        if (!action.IsRunning)
        {
            return Task.WhenAll(
                SaveConversationImmediateAsync(action.SessionId),
                SaveAllMetadataAsync());
        }

        return SaveAllMetadataAsync();
    }

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
}
