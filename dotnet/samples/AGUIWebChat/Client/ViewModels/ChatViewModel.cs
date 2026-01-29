// Copyright (c) Microsoft. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
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

                    // NEW: Check if this is a plan tool result and update PlanSteps accordingly
                    if (IsPlanToolResult(toolResult))
                    {
                        ProcessPlanToolResult(toolResult);
                    }
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

    // --- Plan Tool Result Processing ---

    private bool IsPlanToolResult(AGUIToolResult result)
    {
        // Check if the matching tool call was create_plan or update_plan_step
        if (!_toolCallMessages.TryGetValue(result.Content.CallId, out ChatMessage? callMessage))
        {
            return false;
        }

        FunctionCallContent? functionCall = callMessage.Contents.OfType<FunctionCallContent>()
            .FirstOrDefault(c => c.CallId == result.Content.CallId);

        if (functionCall is null)
        {
            return false;
        }

        bool isPlanTool = functionCall.Name is "create_plan" or "update_plan_step";
        if (isPlanTool)
        {
            _logger.LogInformation("[ChatVM] Detected plan tool result: {FunctionName} (CallId={CallId})",
                functionCall.Name, result.Content.CallId);
        }

        return isPlanTool;
    }

    private void ProcessPlanToolResult(AGUIToolResult result)
    {
        try
        {
            // Get the corresponding tool call to determine the function name
            if (!_toolCallMessages.TryGetValue(result.Content.CallId, out ChatMessage? callMessage))
            {
                _logger.LogWarning("[ChatVM] Cannot process plan tool result - call message not found for CallId={CallId}",
                    result.Content.CallId);
                return;
            }

            FunctionCallContent? functionCall = callMessage.Contents.OfType<FunctionCallContent>()
                .FirstOrDefault(c => c.CallId == result.Content.CallId);

            if (functionCall is null)
            {
                _logger.LogWarning("[ChatVM] Cannot process plan tool result - function call not found for CallId={CallId}",
                    result.Content.CallId);
                return;
            }

            // Extract result data - it could be string or object
            string? resultJson = result.Content.Result switch
            {
                string strResult => strResult,
                JsonElement jsonElement => jsonElement.GetRawText(),
                object obj => JsonSerializer.Serialize(obj),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(resultJson))
            {
                _logger.LogWarning("[ChatVM] Plan tool result has empty data for {FunctionName} (CallId={CallId})",
                    functionCall.Name, result.Content.CallId);
                return;
            }

            _logger.LogInformation("[ChatVM] Processing plan tool result: {FunctionName}, Data={Data}",
                functionCall.Name, resultJson.Substring(0, Math.Min(200, resultJson.Length)));

            // Apply the appropriate update based on function name
            switch (functionCall.Name)
            {
                case "create_plan":
                    // create_plan returns a full plan snapshot
                    ApplyPlanSnapshot(resultJson);
                    _logger.LogInformation("[ChatVM] Applied plan snapshot from create_plan result");
                    break;

                case "update_plan_step":
                    // update_plan_step returns a JSON patch
                    ApplyPlanDelta(resultJson);
                    _logger.LogInformation("[ChatVM] Applied plan delta from update_plan_step result");
                    break;

                default:
                    _logger.LogWarning("[ChatVM] Unknown plan function: {FunctionName}", functionCall.Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Don't crash the UI if plan processing fails - just log it
            _logger.LogError(ex, "[ChatVM] Error processing plan tool result");
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
            string normalizedPatch = NormalizeJsonPatch(raw);
            // Snapshot current state
            var currentState = new AgentState([.. _planSteps]);

            // Apply patch
            var newState = JsonPatchHelper.ApplyPatch(currentState, normalizedPatch);

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

    private static string NormalizeJsonPatch(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(raw);
            if (node is not JsonArray array)
            {
                return raw;
            }

            JsonArray normalizedArray = new();
            foreach (JsonNode? item in array)
            {
                if (item is not JsonObject obj)
                {
                    normalizedArray.Add(item?.DeepClone());
                    continue;
                }

                JsonObject normalizedObj = new();
                foreach ((string key, JsonNode? value) in obj)
                {
                    string normalizedKey = key;
                    if (string.Equals(key, "op", StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedKey = "op";
                    }
                    else if (string.Equals(key, "path", StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedKey = "path";
                    }
                    else if (string.Equals(key, "value", StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedKey = "value";
                    }
                    else if (string.Equals(key, "from", StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedKey = "from";
                    }

                    normalizedObj[normalizedKey] = value?.DeepClone();
                }

                normalizedArray.Add(normalizedObj);
            }

            return normalizedArray.ToJsonString();
        }
        catch
        {
            return raw;
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
