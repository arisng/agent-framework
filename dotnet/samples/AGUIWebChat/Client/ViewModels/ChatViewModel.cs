// Copyright (c) Microsoft. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AGUIWebChat.Client.Services;
using AGUIWebChatClient.Components.Pages.Chat;

namespace AGUIWebChat.Client.ViewModels;

public sealed class ChatViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AGUIProtocolService _protocolService;
    private readonly ILogger<ChatViewModel> _logger;
    private readonly ObservableCollection<AgenticPlanPanel.AgenticPlanStep> _planSteps = new();
    private ChatMessage? _currentResponseMessage;
    private CancellationTokenSource? _currentResponseCancellation;
    private ChatOptions _chatOptions = new();
    private int _statefulMessageCount;
    private bool _isThinking;
    private readonly HashSet<string> _toolCallIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _toolResultIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ChatMessage> _toolCallMessages = new(StringComparer.Ordinal);
    private string _systemPrompt = @"
        You are a helpful assistant.
        ";

    public ChatViewModel(AGUIProtocolService protocolService, ILogger<ChatViewModel> logger)
    {
        _protocolService = protocolService;
        _logger = logger;
        ResetConversation(); // Initialize
    }

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<AgenticPlanPanel.AgenticPlanStep> PlanSteps => _planSteps;

    public bool IsThinking
    {
        get => _isThinking;
        private set
        {
            if (_isThinking != value)
            {
                _isThinking = value;
                OnPropertyChanged();
            }
        }
    }

    public ChatMessage? CurrentResponseMessage
    {
        get => _currentResponseMessage;
        private set
        {
            if (_currentResponseMessage != value)
            {
                _currentResponseMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? MessagesChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void NotifyMessagesChanged() => MessagesChanged?.Invoke(this, EventArgs.Empty);

    public async Task SendMessageAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        CancelAnyCurrentResponse();

        var userMessage = new ChatMessage(ChatRole.User, input);
        Messages.Add(userMessage);

        IsThinking = true;
        NotifyMessagesChanged();

        try
        {
            TextContent responseText = new("");
            CurrentResponseMessage = new ChatMessage(ChatRole.Assistant, [responseText]);
            _currentResponseCancellation = new();

            var requestMessages = Messages.Skip(_statefulMessageCount).ToList();

            // We need to notify UI that CurrentResponseMessage has started, so it can display the "in progress" bubble
            NotifyMessagesChanged();

            await foreach (var update in _protocolService.StreamResponseAsync(requestMessages, _chatOptions, _currentResponseCancellation.Token))
            {
                ProcessUpdate(update, responseText);

                // For streaming text, we notify the specific message item to re-render efficiently
                if (CurrentResponseMessage != null)
                {
                   ChatMessageItem.NotifyChanged(CurrentResponseMessage);
                }
            }

            // Finalize
            if (CurrentResponseMessage != null)
            {
                Messages.Add(CurrentResponseMessage);
                _statefulMessageCount = _chatOptions.ConversationId is not null ? Messages.Count : 0;
                CurrentResponseMessage = null;
            }

            _toolCallIds.Clear();
            _toolResultIds.Clear();
            NotifyMessagesChanged();
        }
        catch (OperationCanceledException)
        {
            // If cancelled, ensure we keep what we have
             if (CurrentResponseMessage is not null)
            {
                Messages.Add(CurrentResponseMessage);
                NotifyMessagesChanged();
            }
        }
        finally
        {
            IsThinking = false;
            CurrentResponseMessage = null;
            _currentResponseCancellation = null;
        }
    }

    private void ProcessUpdate(AGUIUpdate update, TextContent responseText)
    {
        switch (update)
        {
            case AGUIConversationIdUpdate idUpdate:
                _chatOptions.ConversationId = idUpdate.ConversationId;
                break;
            case AGUITextPart textUpdate:
                responseText.Text += textUpdate.Text;
                break;
            case AGUIToolCall toolCall:
                 if (_toolCallIds.Add(toolCall.Content.CallId))
                {
                    _logger.LogInformation("[ChatVM] Processing tool call: {Name} (ID: {CallId})", toolCall.Content.Name, toolCall.Content.CallId);
                    ChatMessage? targetMessage = CurrentResponseMessage;
                    if (targetMessage is null)
                    {
                        targetMessage = new ChatMessage(ChatRole.Assistant, []);
                        Messages.Add(targetMessage);
                        NotifyMessagesChanged();
                    }

                    targetMessage.Contents.Add(toolCall.Content);
                    _toolCallMessages[toolCall.Content.CallId] = targetMessage;
                    _logger.LogInformation("[ChatVM] Tool call added to message (Role={Role}, Contents={Count})", targetMessage.Role, targetMessage.Contents.Count);
                    ChatMessageItem.NotifyChanged(targetMessage);
                }
                break;
            case AGUIToolResult toolResult:
                if (_toolResultIds.Add(toolResult.Content.CallId))
                {
                    _logger.LogInformation("[ChatVM] Processing tool result for CallId={CallId}, Result={Result}",
                        toolResult.Content.CallId,
                        toolResult.Content.Result?.ToString()?.Substring(0, Math.Min(100, toolResult.Content.Result?.ToString()?.Length ?? 0)));

                    // CRITICAL FIX: Tool results must ALWAYS be in separate Tool role messages
                    // OpenAI requires: [Assistant with tool_calls] -> [Tool with results]
                    // NOT: [Assistant with both tool_calls AND results]
                    _logger.LogInformation("[ChatVM] Creating NEW Tool message for tool result (Role=Tool)");
                    ChatMessage toolResultMessage = new ChatMessage(ChatRole.Tool, [toolResult.Content]);
                    Messages.Add(toolResultMessage);
                    _logger.LogInformation("[ChatVM] Tool result message added: MessageCount={Count}, MessageRole={Role}, ContentCount={ContentCount}",
                        Messages.Count, toolResultMessage.Role, toolResultMessage.Contents.Count);
                    NotifyMessagesChanged();
                    ChatMessageItem.NotifyChanged(toolResultMessage);
                }
                else
                {
                    _logger.LogWarning("[ChatVM] Duplicate tool result ignored for CallId={CallId}", toolResult.Content.CallId);
                }
                break;
            case AGUIDataSnapshot snapshot:
                ApplyPlanSnapshot(snapshot.JsonData);
                break;
            case AGUIDataDelta delta:
                ApplyPlanDelta(delta.JsonData);
                break;
            case AGUIRawUpdate raw:
                break;
        }
    }

    public void CancelAnyCurrentResponse()
    {
        if (CurrentResponseMessage is not null)
        {
            Messages.Add(CurrentResponseMessage);
            NotifyMessagesChanged();
        }

        _currentResponseCancellation?.Cancel();
        CurrentResponseMessage = null;
        _toolCallIds.Clear();
        _toolResultIds.Clear();
        _toolCallMessages.Clear();
        IsThinking = false;
        NotifyMessagesChanged();
    }

    public void ResetConversation()
    {
        CancelAnyCurrentResponse();
        Messages.Clear();
        Messages.Add(new ChatMessage(ChatRole.System, _systemPrompt));
        _chatOptions.ConversationId = null;
        _statefulMessageCount = 0;
        _planSteps.Clear();
        _toolCallIds.Clear();
        _toolResultIds.Clear();
        _toolCallMessages.Clear();
        NotifyMessagesChanged();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
             _currentResponseCancellation?.Dispose();
        }
    }

    // --- JSON Patching Logic (Temporary Copy) ---

    private void ApplyPlanSnapshot(string raw)
    {
        if (!TryReadJsonElement(raw, out JsonElement snapshotElement))
        {
            return;
        }

        if (!snapshotElement.TryGetProperty("steps", out JsonElement stepsElement) || stepsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        _planSteps.Clear();

        foreach (JsonElement stepElement in stepsElement.EnumerateArray())
        {
            string description = stepElement.TryGetProperty("description", out JsonElement descriptionElement)
                ? (descriptionElement.GetString() ?? string.Empty)
                : string.Empty;
            string status = stepElement.TryGetProperty("status", out JsonElement statusElement)
                ? (statusElement.GetString() ?? "pending")
                : "pending";
            string? detail = stepElement.TryGetProperty("detail", out JsonElement detailElement)
                ? detailElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(description))
            {
                description = "Untitled step";
            }

#pragma warning disable CA1308 // Normalize strings to uppercase
            _planSteps.Add(new AgenticPlanPanel.AgenticPlanStep(description, status.ToLowerInvariant(), detail));
#pragma warning restore CA1308
        }

        // Notify UI of plan changes
        NotifyMessagesChanged();
        OnPropertyChanged(nameof(PlanSteps));
    }

    private void ApplyPlanDelta(string raw)
    {
        try
        {
            // Snapshot current state
            var currentState = new AgentState([.. _planSteps]);

            // Apply patch
            var newState = JsonPatchHelper.ApplyPatch(currentState, raw);

            if (newState?.Steps is not null)
            {
                SyncPlanSteps(newState.Steps);
            }
        }
        catch
        {
            // Ignore malformed patches to prevent UI crashes
        }
    }

    private void SyncPlanSteps(List<AgenticPlanPanel.AgenticPlanStep> newSteps)
    {
        // If the structure changed significantly (count diff), reset
        if (newSteps.Count != _planSteps.Count)
        {
            _planSteps.Clear();
            foreach (var step in newSteps)
            {
                _planSteps.Add(step);
            }
        }
        else
        {
            // Update in-place to minimize UI flickering
            for (int i = 0; i < newSteps.Count; i++)
            {
                var current = _planSteps[i];
                var next = newSteps[i];

                bool changed = !string.Equals(current.Description, next.Description, StringComparison.Ordinal) ||
                               !string.Equals(current.Status, next.Status, StringComparison.Ordinal) ||
                               !string.Equals(current.Detail, next.Detail, StringComparison.Ordinal);

                if (changed)
                {
                    _planSteps[i] = next;
                }
            }
        }

        OnPropertyChanged(nameof(PlanSteps));
        NotifyMessagesChanged();
    }

    private sealed record AgentState(List<AgenticPlanPanel.AgenticPlanStep> Steps);

    private static bool TryReadJsonElement(string raw, out JsonElement element)
    {
        element = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            element = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
