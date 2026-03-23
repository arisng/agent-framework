using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using AGUIDojoClient.Helpers;
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

        string? candidate = BuildInstantTitleCandidate(action.Message.Text);
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

        if (!entry.Metadata.Title.EndsWith("...", StringComparison.Ordinal)
            && !IsAttachmentFallbackTitle(entry.Metadata.Title))
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
                .Select(m => new
                {
                    Role = m.Role == ChatRole.User ? "user" : "assistant",
                    Content = m.Role == ChatRole.User
                        ? BuildTitlePromptContent(m.Text)
                        : m.Text ?? string.Empty
                })
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

    private static string BuildInstantTitleCandidate(string? text)
    {
        string visibleText = MessageAttachmentMarkers.Strip(text).Trim();
        if (!string.IsNullOrWhiteSpace(visibleText))
        {
            return visibleText;
        }

        int attachmentCount = MessageAttachmentMarkers.Extract(text).Count;
        return attachmentCount switch
        {
            1 => "Image attachment",
            > 1 => $"{attachmentCount} image attachments",
            _ => string.Empty,
        };
    }

    private static string BuildTitlePromptContent(string? text)
    {
        string visibleText = MessageAttachmentMarkers.Strip(text).Trim();
        int attachmentCount = MessageAttachmentMarkers.Extract(text).Count;
        if (attachmentCount == 0)
        {
            return visibleText;
        }

        string attachmentSummary = attachmentCount == 1 ? "1 image attachment" : $"{attachmentCount} image attachments";
        return string.IsNullOrWhiteSpace(visibleText)
            ? $"User shared {attachmentSummary}."
            : $"{visibleText}\n\n[{attachmentSummary}]";
    }

    private static bool IsAttachmentFallbackTitle(string title) =>
        string.Equals(title, "Image attachment", StringComparison.Ordinal)
        || title.EndsWith(" image attachments", StringComparison.Ordinal);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Deserialized by ReadFromJsonAsync.")]
    private sealed record TitleResponse(string Title);
}
