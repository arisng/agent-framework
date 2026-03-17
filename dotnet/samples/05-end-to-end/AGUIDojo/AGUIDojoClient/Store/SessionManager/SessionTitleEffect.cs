// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using AGUIDojoClient.Models;
using Fluxor;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Sets a session title from the first user message (instant truncation),
/// then replaces it with an LLM-generated title after the first assistant response.
/// </summary>
public sealed class SessionTitleEffect
{
    private readonly IState<SessionManagerState> _sessionStore;
    private readonly IHttpClientFactory _httpClientFactory;

    public SessionTitleEffect(IState<SessionManagerState> sessionStore, IHttpClientFactory httpClientFactory)
    {
        _sessionStore = sessionStore;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Immediately sets a truncated title from the first user message for instant feedback.
    /// </summary>
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

    /// <summary>
    /// When streaming ends after the first exchange, calls the LLM to generate a better title.
    /// </summary>
    [EffectMethod]
    public async Task HandleStreamComplete(SessionActions.SetRunningAction action, IDispatcher dispatcher)
    {
        // Only trigger when streaming ENDS (IsRunning becomes false)
        if (action.IsRunning)
        {
            return;
        }

        if (!SessionSelectors.TryGetSession(_sessionStore.Value, action.SessionId, out SessionEntry entry))
        {
            return;
        }

        // Only generate for sessions with a truncated title (not "New Chat" and not already LLM-generated).
        // Heuristic: truncated titles end with "..." and are <= 50 chars.
        if (string.Equals(entry.Metadata.Title, SessionMetadata.DefaultTitle, StringComparison.Ordinal))
        {
            return;
        }

        if (!entry.Metadata.Title.EndsWith("...", StringComparison.Ordinal))
        {
            return;
        }

        // Only on the first exchange (2-4 messages: user + assistant, possibly with tool messages)
        var messages = entry.State.Messages;
        if (messages.Count < 2 || messages.Count > 4)
        {
            return;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("aguiserver");
            var titleMessages = messages
                .Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)
                .Take(4)
                .Select(m => new { Role = m.Role == ChatRole.User ? "user" : "assistant", Content = m.Text ?? string.Empty })
                .ToList();

            var titleRequest = new { Messages = titleMessages };

            var response = await httpClient.PostAsJsonAsync("/api/title", titleRequest);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TitleResponse>();
                if (!string.IsNullOrWhiteSpace(result?.Title))
                {
                    dispatcher.Dispatch(new SessionActions.SetSessionTitleAction(action.SessionId, result.Title));
                }
            }
        }
        catch
        {
            // Silently fail — truncated title is an acceptable fallback
        }
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Deserialized by ReadFromJsonAsync.")]
    private sealed record TitleResponse(string Title);
}
