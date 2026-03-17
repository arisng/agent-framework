// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;
using Fluxor;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Sets a session title from the first user message when the title is still the default placeholder.
/// </summary>
public sealed class SessionTitleEffect
{
    private readonly IState<SessionManagerState> _sessionStore;

    public SessionTitleEffect(IState<SessionManagerState> sessionStore)
    {
        _sessionStore = sessionStore;
    }

    [EffectMethod]
    public Task HandleAddMessage(SessionActions.AddMessageAction action, IDispatcher dispatcher)
    {
        if (action.Message.Role != ChatRole.User)
        {
            return Task.CompletedTask;
        }

        if (!SessionSelectors.TryGetSession(_sessionStore.Value, action.SessionId, out SessionEntry entry))
        {
            return Task.CompletedTask;
        }

        if (!string.Equals(entry.Metadata.Title, SessionMetadata.DefaultTitle, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        string? candidate = action.Message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return Task.CompletedTask;
        }

        if (candidate.Length > 50)
        {
            candidate = candidate[..47] + "...";
        }

        dispatcher.Dispatch(new SessionActions.SetSessionTitleAction(action.SessionId, candidate));
        return Task.CompletedTask;
    }
}
