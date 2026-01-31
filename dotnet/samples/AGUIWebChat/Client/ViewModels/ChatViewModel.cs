// Copyright (c) Microsoft. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using AGUIWebChat.Client.Services;
using AGUIWebChatClient.Components.Pages.Chat;
using Microsoft.Extensions.AI;

namespace AGUIWebChat.Client.ViewModels;

public sealed class ChatViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AGUIProtocolService _protocolService;
    private readonly ILogger<ChatViewModel> _logger;
    private CancellationTokenSource? _currentResponseCancellation;
    private readonly ChatOptions _chatOptions = new();
    private int _statefulMessageCount;
    private readonly HashSet<string> _toolCallIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _toolResultIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ChatMessage> _toolCallMessages = new(StringComparer.Ordinal);
    private ChatMessage? _currentPlanMessage;
    private readonly string _systemPrompt = @"
        You are a helpful assistant.
        ";

    public ChatViewModel(AGUIProtocolService protocolService, ILogger<ChatViewModel> logger)
    {
        this._protocolService = protocolService;
        this._logger = logger;
        this.ResetConversation(); // Initialize
    }

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<AgenticPlanPanel.AgenticPlanStep> PlanSteps { get; } = new();

    public bool IsThinking
    {
        get; private set
        {
            if (field != value)
            {
                field = value;
                this.OnPropertyChanged();
            }
        }
    }

    public ChatMessage? CurrentResponseMessage
    {
        get; private set
        {
            if (field != value)
            {
                field = value;
                this.OnPropertyChanged();
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
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        this.CancelAnyCurrentResponse();

        var userMessage = new ChatMessage(ChatRole.User, input);
        this.Messages.Add(userMessage);

        this.IsThinking = true;
        this.NotifyMessagesChanged();

        try
        {
            TextContent responseText = new("");
            this.CurrentResponseMessage = new ChatMessage(ChatRole.Assistant, [responseText]);
            this._currentResponseCancellation = new();

            var requestMessages = this.Messages.Skip(this._statefulMessageCount).ToList();

            // We need to notify UI that CurrentResponseMessage has started, so it can display the "in progress" bubble
            this.NotifyMessagesChanged();

            await foreach (var update in this._protocolService.StreamResponseAsync(requestMessages, this._chatOptions, this._currentResponseCancellation.Token))
            {
                this.ProcessUpdate(update, responseText);

                // For streaming text, we notify the specific message item to re-render efficiently
                if (this.CurrentResponseMessage != null)
                {
                    ChatMessageItem.NotifyChanged(this.CurrentResponseMessage);
                }
            }

            // Finalize
            if (this.CurrentResponseMessage != null)
            {
                this.Messages.Add(this.CurrentResponseMessage);
                this._statefulMessageCount = this._chatOptions.ConversationId is not null ? this.Messages.Count : 0;
                this.CurrentResponseMessage = null;
            }

            this._toolCallIds.Clear();
            this._toolResultIds.Clear();
            this.NotifyMessagesChanged();
        }
        catch (OperationCanceledException)
        {
            // If cancelled, ensure we keep what we have
            if (this.CurrentResponseMessage is not null)
            {
                this.Messages.Add(this.CurrentResponseMessage);
                this.NotifyMessagesChanged();
            }
        }
        finally
        {
            this.IsThinking = false;
            this.CurrentResponseMessage = null;
            this._currentResponseCancellation = null;
        }
    }

    private void ProcessUpdate(AGUIUpdate update, TextContent responseText)
    {
        switch (update)
        {
            case AGUIConversationIdUpdate idUpdate:
                this._chatOptions.ConversationId = idUpdate.ConversationId;
                break;
            case AGUITextPart textUpdate:
                responseText.Text += textUpdate.Text;
                break;
            case AGUIToolCall toolCall:
                if (this._toolCallIds.Add(toolCall.Content.CallId))
                {
                    this._logger.LogInformation("[ChatVM] Processing tool call: {Name} (ID: {CallId})", toolCall.Content.Name, toolCall.Content.CallId);
                    ChatMessage? targetMessage = this.CurrentResponseMessage;
                    if (targetMessage is null)
                    {
                        targetMessage = new ChatMessage(ChatRole.Assistant, []);
                        this.Messages.Add(targetMessage);
                        this.NotifyMessagesChanged();
                    }

                    targetMessage.Contents.Add(toolCall.Content);
                    this._toolCallMessages[toolCall.Content.CallId] = targetMessage;
                    this._logger.LogInformation("[ChatVM] Tool call added to message (Role={Role}, Contents={Count})", targetMessage.Role, targetMessage.Contents.Count);
                    ChatMessageItem.NotifyChanged(targetMessage);
                }
                break;
            case AGUIToolResult toolResult:
                if (this._toolResultIds.Add(toolResult.Content.CallId))
                {
                    this._logger.LogInformation("[ChatVM] Processing tool result for CallId={CallId}, Result={Result}",
                        toolResult.Content.CallId,
                        toolResult.Content.Result?.ToString()?.Substring(0, Math.Min(100, toolResult.Content.Result?.ToString()?.Length ?? 0)));

                    // CRITICAL FIX: Tool results must ALWAYS be in separate Tool role messages
                    // OpenAI requires: [Assistant with tool_calls] -> [Tool with results]
                    // NOT: [Assistant with both tool_calls AND results]
                    this._logger.LogInformation("[ChatVM] Creating NEW Tool message for tool result (Role=Tool)");
                    ChatMessage toolResultMessage = new(ChatRole.Tool, [toolResult.Content]);
                    this.Messages.Add(toolResultMessage);
                    this._logger.LogInformation("[ChatVM] Tool result message added: MessageCount={Count}, MessageRole={Role}, ContentCount={ContentCount}",
                        this.Messages.Count, toolResultMessage.Role, toolResultMessage.Contents.Count);
                    this.NotifyMessagesChanged();
                    ChatMessageItem.NotifyChanged(toolResultMessage);

                    // NEW: Check if this is a plan tool result and update PlanSteps accordingly
                    if (this.IsPlanToolResult(toolResult))
                    {
                        this.ProcessPlanToolResult(toolResult);
                    }
                }
                else
                {
                    this._logger.LogWarning("[ChatVM] Duplicate tool result ignored for CallId={CallId}", toolResult.Content.CallId);
                }
                break;
            case AGUIDataSnapshot snapshot:
                this.ApplyPlanSnapshot(snapshot.JsonData);
                break;
            case AGUIDataDelta delta:
                this.ApplyPlanDelta(delta.JsonData);
                break;
            case AGUIRawUpdate:
                break;
        }
    }

    public void CancelAnyCurrentResponse()
    {
        if (this.CurrentResponseMessage is not null)
        {
            this.Messages.Add(this.CurrentResponseMessage);
            this.NotifyMessagesChanged();
        }

        this._currentResponseCancellation?.Cancel();
        this.CurrentResponseMessage = null;
        this._toolCallIds.Clear();
        this._toolResultIds.Clear();
        this._toolCallMessages.Clear();
        this.IsThinking = false;
        this.NotifyMessagesChanged();
    }

    public void ResetConversation()
    {
        this.CancelAnyCurrentResponse();
        this.Messages.Clear();
        this.Messages.Add(new ChatMessage(ChatRole.System, this._systemPrompt));
        this._chatOptions.ConversationId = null;
        this._statefulMessageCount = 0;
        this.PlanSteps.Clear();
        this._toolCallIds.Clear();
        this._toolResultIds.Clear();
        this._toolCallMessages.Clear();
        this._currentPlanMessage = null;
        this.NotifyMessagesChanged();
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._currentResponseCancellation?.Dispose();
        }
    }

    // --- Plan Tool Result Processing ---

    private bool IsPlanToolResult(AGUIToolResult result)
    {
        // Check if the matching tool call was create_plan or update_plan_step
        if (!this._toolCallMessages.TryGetValue(result.Content.CallId, out ChatMessage? callMessage))
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
            this._logger.LogInformation("[ChatVM] Detected plan tool result: {FunctionName} (CallId={CallId})",
                functionCall.Name, result.Content.CallId);
        }

        return isPlanTool;
    }

    private void ProcessPlanToolResult(AGUIToolResult result)
    {
        try
        {
            // Get the corresponding tool call to determine the function name
            if (!this._toolCallMessages.TryGetValue(result.Content.CallId, out ChatMessage? callMessage))
            {
                this._logger.LogWarning("[ChatVM] Cannot process plan tool result - call message not found for CallId={CallId}",
                    result.Content.CallId);
                return;
            }

            FunctionCallContent? functionCall = callMessage.Contents.OfType<FunctionCallContent>()
                .FirstOrDefault(c => c.CallId == result.Content.CallId);

            if (functionCall is null)
            {
                this._logger.LogWarning("[ChatVM] Cannot process plan tool result - function call not found for CallId={CallId}",
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
                this._logger.LogWarning("[ChatVM] Plan tool result has empty data for {FunctionName} (CallId={CallId})",
                    functionCall.Name, result.Content.CallId);
                return;
            }

            this._logger.LogInformation("[ChatVM] Processing plan tool result: {FunctionName}, Data={Data}",
                functionCall.Name, resultJson.Substring(0, Math.Min(200, resultJson.Length)));

            // Apply the appropriate update based on function name
            switch (functionCall.Name)
            {
                case "create_plan":
                    // create_plan returns a full plan snapshot
                    this.ApplyPlanSnapshot(resultJson);
                    this._logger.LogInformation("[ChatVM] Applied plan snapshot from create_plan result");
                    break;

                case "update_plan_step":
                    // update_plan_step returns a JSON patch
                    this.ApplyPlanDelta(resultJson);
                    this._logger.LogInformation("[ChatVM] Applied plan delta from update_plan_step result");
                    break;

                default:
                    this._logger.LogWarning("[ChatVM] Unknown plan function: {FunctionName}", functionCall.Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Don't crash the UI if plan processing fails - just log it
            this._logger.LogError(ex, "[ChatVM] Error processing plan tool result");
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

        this.PlanSteps.Clear();

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
            this.PlanSteps.Add(new AgenticPlanPanel.AgenticPlanStep(description, status.ToLowerInvariant(), detail));
#pragma warning restore CA1308
        }

        // FEATURE: Render plan inline with messages
        // Create or update a plan message in the message list
        this.UpdatePlanMessage();

        // Notify UI of plan changes
        this.NotifyMessagesChanged();
        this.OnPropertyChanged(nameof(this.PlanSteps));
    }

    private void ApplyPlanDelta(string raw)
    {
        try
        {
            string normalizedPatch = NormalizeJsonPatch(raw);
            // Snapshot current state
            var currentState = new AgentState([.. this.PlanSteps]);

            // Apply patch
            var newState = JsonPatchHelper.ApplyPatch(currentState, normalizedPatch);

            if (newState?.Steps is not null)
            {
                this.SyncPlanSteps(newState.Steps);
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
        if (newSteps.Count != this.PlanSteps.Count)
        {
            this.PlanSteps.Clear();
            foreach (var step in newSteps)
            {
                this.PlanSteps.Add(step);
            }
        }
        else
        {
            // Update in-place to minimize UI flickering
            for (int i = 0; i < newSteps.Count; i++)
            {
                var current = this.PlanSteps[i];
                var next = newSteps[i];

                bool changed = !string.Equals(current.Description, next.Description, StringComparison.Ordinal) ||
                               !string.Equals(current.Status, next.Status, StringComparison.Ordinal) ||
                               !string.Equals(current.Detail, next.Detail, StringComparison.Ordinal);

                if (changed)
                {
                    this.PlanSteps[i] = next;
                }
            }
        }

        // FEATURE: Update plan message after syncing steps
        this.UpdatePlanMessage();

        this.OnPropertyChanged(nameof(this.PlanSteps));
        this.NotifyMessagesChanged();
    }

    private sealed record AgentState(List<AgenticPlanPanel.AgenticPlanStep> Steps);

    private void UpdatePlanMessage()
    {
        // Create PlanContent from current PlanSteps
        if (this.PlanSteps.Count == 0)
        {
            return;
        }

        var planContent = new PlanContent(
            Steps: this.PlanSteps.ToList(),
            Title: "Plan",
            Subtitle: null
        );

        // Serialize PlanContent to JSON bytes for DataContent
        byte[] planBytes = JsonSerializer.SerializeToUtf8Bytes(planContent, s_jsonOptions);

        var dataContent = new DataContent(planBytes, "application/vnd.microsoft.agui.plan+json");

        if (this._currentPlanMessage is not null)
        {
            // Update existing plan message
            this._logger.LogInformation("[ChatVM] Updating existing plan message");

            // Clear existing contents and add updated plan
            this._currentPlanMessage.Contents.Clear();
            this._currentPlanMessage.Contents.Add(dataContent);

            // Notify that this specific message changed
            ChatMessageItem.NotifyChanged(this._currentPlanMessage);
        }
        else
        {
            // Create new plan message
            this._logger.LogInformation("[ChatVM] Creating new plan message");
            this._currentPlanMessage = new ChatMessage(ChatRole.Assistant, [dataContent]);
            this.Messages.Add(this._currentPlanMessage);
        }
    }

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
