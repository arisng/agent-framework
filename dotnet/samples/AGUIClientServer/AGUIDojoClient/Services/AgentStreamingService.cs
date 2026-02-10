// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUIDojoClient.Helpers;
using AGUIDojoClient.Models;
using AGUIDojoClient.Shared;
using AGUIDojoClient.Store.AgentState;
using AGUIDojoClient.Store.ArtifactState;
using AGUIDojoClient.Store.ChatState;
using AGUIDojoClient.Store.PlanState;
using Fluxor;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Coordinates the AG-UI SSE streaming loop, governance actions, and conversation lifecycle.
/// Owns all streaming-related state: cancellation tokens, deduplication sets,
/// approval <see cref="TaskCompletionSource{TResult}"/>, diff preview state, and
/// <see cref="ChatOptions"/>.
/// </summary>
/// <remarks>
/// Registered as <c>Scoped</c> (per Blazor circuit). Dispatches Fluxor actions for
/// all state mutations. Uses UI callbacks (<c>throttledStateChanged</c>,
/// <c>stateChanged</c>) to trigger Blazor re-renders without coupling to
/// <c>ComponentBase</c>.
/// <para>
/// Extracted from <c>Chat.razor</c> (task-27) to reduce component complexity from
/// ~720 lines to ≤300 lines while preserving all 7 AG-UI protocol features.
/// </para>
/// </remarks>
public sealed class AgentStreamingService : IAgentStreamingService
{
    private readonly IDispatcher _dispatcher;
    private readonly IState<PlanState> _planStore;
    private readonly IState<AgentState> _agentStore;
    private readonly IState<ArtifactState> _artifactStore;
    private readonly IState<ChatState> _chatStore;
    private readonly IApprovalHandler _approvalHandler;
    private readonly IJsonPatchApplier _jsonPatchApplier;
    private readonly IStateManager _stateManager;
    private readonly IObservabilityService _observabilityService;
    private readonly ICheckpointService _checkpointService;

    private readonly ChatOptions _chatOptions = new();
    private readonly HashSet<string> _seenFunctionCallIds = new();
    private readonly HashSet<string> _seenFunctionResultCallIds = new();

    private CancellationTokenSource? _currentResponseCancellation;
    private ChatMessage? _streamingMessage;
    private TaskCompletionSource<bool>? _approvalTaskSource;

    private object? _lastDiffBefore;
    private object? _lastDiffAfter;
    private string _lastDiffTitle = "State Diff";

    private Action? _throttledStateChanged;
    private Action? _stateChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStreamingService"/> class.
    /// </summary>
    public AgentStreamingService(
        IDispatcher dispatcher,
        IState<PlanState> planStore,
        IState<AgentState> agentStore,
        IState<ArtifactState> artifactStore,
        IState<ChatState> chatStore,
        IApprovalHandler approvalHandler,
        IJsonPatchApplier jsonPatchApplier,
        IStateManager stateManager,
        IObservabilityService observabilityService,
        ICheckpointService checkpointService)
    {
        _dispatcher = dispatcher;
        _planStore = planStore;
        _agentStore = agentStore;
        _artifactStore = artifactStore;
        _chatStore = chatStore;
        _approvalHandler = approvalHandler;
        _jsonPatchApplier = jsonPatchApplier;
        _stateManager = stateManager;
        _observabilityService = observabilityService;
        _checkpointService = checkpointService;
    }

    /// <inheritdoc />
    public object? LastDiffBefore => _lastDiffBefore;

    /// <inheritdoc />
    public object? LastDiffAfter => _lastDiffAfter;

    /// <inheritdoc />
    public string LastDiffTitle => _lastDiffTitle;

    /// <inheritdoc />
    public ChatOptions ChatOptions => _chatOptions;

    /// <summary>
    /// Gets whether the currently selected endpoint is the Shared State endpoint.
    /// </summary>
    private bool IsSharedStateEndpoint =>
        _agentStore.Value.SelectedEndpointPath.Equals("shared_state", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether the currently selected endpoint is the Predictive State Updates endpoint.
    /// </summary>
    private bool IsPredictiveStateEndpoint =>
        _agentStore.Value.SelectedEndpointPath.Equals("predictive_state_updates", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void SetUiCallbacks(Action throttledStateChanged, Action stateChanged)
    {
        _throttledStateChanged = throttledStateChanged;
        _stateChanged = stateChanged;
    }

    /// <inheritdoc />
    public async Task ProcessAgentResponseAsync(IChatClient chatClient)
    {
        bool hasApprovalResponses;

        do
        {
            hasApprovalResponses = false;
            List<FunctionResultContent> approvalResponses = [];

            // Stream and display a new response from the IChatClient
            var responseText = new TextContent("");
            _streamingMessage = new ChatMessage(ChatRole.Assistant, [responseText]);
            _dispatcher.Dispatch(new ChatActions.UpdateResponseMessageAction(_streamingMessage));
            _stateChanged?.Invoke();
            _currentResponseCancellation = new();
            _dispatcher.Dispatch(new AgentActions.SetRunningAction(true));

            try
            {
                await foreach (var update in chatClient.GetStreamingResponseAsync(
                    _chatStore.Value.Messages.Skip(_chatStore.Value.StatefulMessageCount), _chatOptions, _currentResponseCancellation.Token))
                {
                    // Check for approval requests and Plan state updates in the update
                    foreach (var content in update.Contents)
                    {
                        if (content is FunctionCallContent fcc && _approvalHandler.TryExtractApprovalRequest(fcc, out var approval) && approval is not null)
                        {
                            // Show approval dialog and wait for user decision
                            _dispatcher.Dispatch(new ChatActions.SetPendingApprovalAction(approval));
                            _approvalTaskSource = new TaskCompletionSource<bool>();
                            _stateChanged?.Invoke();

                            bool approved = await _approvalTaskSource.Task;

                            // Create approval response
                            var response = _approvalHandler.CreateApprovalResponse(approval.ApprovalId, approved);
                            approvalResponses.Add(response);

                            // Add approval/rejection response to streaming message for immediate display
                            // This ensures the tool call cycle shows "Completed"/"Rejected" instead of "Running"
                            if (_streamingMessage is not null)
                            {
                                _streamingMessage.Contents.Add(response);
                            }

                            _dispatcher.Dispatch(new ChatActions.SetPendingApprovalAction(null));
                            _approvalTaskSource = null;
                            _stateChanged?.Invoke();
                        }
                        // Handle Plan state snapshots (Agentic Generative UI)
                        else if (content is DataContent dc && ChatHelpers.IsPlanStateSnapshot(dc))
                        {
                            Plan? plan = ChatHelpers.TryParsePlanSnapshot(dc);
                            if (plan is not null)
                            {
                                _checkpointService.CreateCheckpoint("Before plan update", _planStore.Value.Plan, IsSharedStateEndpoint ? _stateManager.CurrentRecipe : null, _artifactStore.Value.CurrentDocumentState, _chatStore.Value.Messages.Count);
                                _lastDiffBefore = _planStore.Value.Plan;
                                _dispatcher.Dispatch(new PlanActions.SetPlanAction(plan));
                                _lastDiffAfter = plan;
                                _lastDiffTitle = "Plan State Change";
                                _stateChanged?.Invoke();
                            }
                        }
                        // Handle Plan state deltas (JSON Patch for step updates)
                        else if (content is DataContent patchDc && ChatHelpers.IsPlanStateDelta(patchDc))
                        {
                            if (_planStore.Value.Plan is not null)
                            {
                                List<JsonPatchOperation>? operations = ChatHelpers.TryParsePatchOperations(patchDc);
                                if (operations is not null)
                                {
                                    _checkpointService.CreateCheckpoint("Before plan step update", _planStore.Value.Plan, IsSharedStateEndpoint ? _stateManager.CurrentRecipe : null, _artifactStore.Value.CurrentDocumentState, _chatStore.Value.Messages.Count);
                                    _jsonPatchApplier.ApplyPatch(_planStore.Value.Plan, operations);
                                    _dispatcher.Dispatch(new PlanActions.ApplyPlanDeltaAction(operations));
                                    _stateChanged?.Invoke();
                                }
                            }
                        }
                        // Handle Recipe state snapshots (Shared State feature)
                        else if (content is DataContent recipeDc && IsSharedStateEndpoint && _stateManager.TryExtractRecipeSnapshot(recipeDc, out Recipe? recipe) && recipe is not null)
                        {
                            _checkpointService.CreateCheckpoint("Before recipe update", _planStore.Value.Plan, _stateManager.CurrentRecipe, _artifactStore.Value.CurrentDocumentState, _chatStore.Value.Messages.Count);
                            _lastDiffBefore = _stateManager.CurrentRecipe;
                            _stateManager.UpdateFromServerSnapshot(recipe);
                            _dispatcher.Dispatch(new SetRecipeAction(recipe));
                            _lastDiffAfter = _stateManager.CurrentRecipe;
                            _lastDiffTitle = "Recipe State Change";
                            _stateChanged?.Invoke();
                        }
                        // Handle Document state snapshots (Predictive State Updates feature)
                        else if (content is DataContent docDc && IsPredictiveStateEndpoint && ChatHelpers.TryExtractDocumentSnapshot(docDc, out DocumentState? docState) && docState is not null)
                        {
                            if (_artifactStore.Value.CurrentDocumentState is null)
                            {
                                _checkpointService.CreateCheckpoint("Before document update", _planStore.Value.Plan, null, _artifactStore.Value.CurrentDocumentState, _chatStore.Value.Messages.Count);
                            }
                            _dispatcher.Dispatch(new SetDocumentAction(docState));
                            _dispatcher.Dispatch(new SetDocumentPreviewAction(true));
                            _throttledStateChanged?.Invoke();
                        }
                    }

                    // Consolidate content: only add non-duplicate FunctionCallContent and skip redundant DataContent
                    foreach (AIContent content in update.Contents)
                    {
                        if (content is TextContent)
                        {
                            continue;
                        }

                        if (content is FunctionCallContent fcc)
                        {
                            if (fcc.CallId is not null && !_seenFunctionCallIds.Contains(fcc.CallId))
                            {
                                _seenFunctionCallIds.Add(fcc.CallId);
                                _streamingMessage?.Contents.Add(content);
                                _observabilityService.StartToolCall(fcc.CallId, fcc.Name ?? "unknown", fcc.Arguments);
                            }
                        }
                        else if (content is DataContent dc)
                        {
                            ChatHelpers.ConsolidateDataContent(_streamingMessage!, dc);
                        }
                        else if (content is FunctionResultContent frc)
                        {
                            if (frc.CallId is not null && !_seenFunctionResultCallIds.Contains(frc.CallId))
                            {
                                _seenFunctionResultCallIds.Add(frc.CallId);
                                _streamingMessage?.Contents.Add(content);
                                _observabilityService.CompleteToolCall(frc.CallId, frc.Result);
                            }
                            else if (frc.CallId is null)
                            {
                                _streamingMessage?.Contents.Add(content);
                            }
                        }
                        else
                        {
                            _streamingMessage?.Contents.Add(content);
                        }
                    }

                    responseText.Text += update.Text;
                    _chatOptions.ConversationId = update.ConversationId;
                    _dispatcher.Dispatch(new ChatActions.SetConversationIdAction(update.ConversationId));

                    if (update.AuthorName is not null)
                    {
                        _dispatcher.Dispatch(new AgentActions.SetAuthorNameAction(update.AuthorName));
                        _streamingMessage!.AuthorName = update.AuthorName;
                    }

                    _throttledStateChanged?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                // User cancelled the operation
            }
            catch (HttpRequestException ex)
            {
                responseText.Text = $"Error: {ex.Message}";
                Components.Pages.Chat.ChatMessageItem.NotifyChanged(_streamingMessage);
            }
            catch (Exception ex)
            {
                responseText.Text = $"An unexpected error occurred: {ex.Message}";
                Components.Pages.Chat.ChatMessageItem.NotifyChanged(_streamingMessage);
            }

            _dispatcher.Dispatch(new AgentActions.SetRunningAction(false));

            // Store the final response in the conversation via Fluxor dispatch
            _dispatcher.Dispatch(new ChatActions.AddMessageAction(_streamingMessage!));
            _streamingMessage = null;
            _dispatcher.Dispatch(new ChatActions.UpdateResponseMessageAction(null));

            // If there were approval responses, add them as a tool message and continue
            if (approvalResponses.Count > 0)
            {
                bool hasAnyRejection = approvalResponses.Any(r => ChatHelpers.IsRejectionResponse(r));

                foreach (var response in approvalResponses)
                {
                    _dispatcher.Dispatch(new ChatActions.AddMessageAction(new ChatMessage(ChatRole.Tool, [response])));
                }

                if (hasAnyRejection)
                {
                    hasApprovalResponses = false;
                    _chatOptions.ConversationId = null;
                    _dispatcher.Dispatch(new ChatActions.SetConversationIdAction(null));
                    _dispatcher.Dispatch(new ChatActions.SetStatefulCountAction(_chatStore.Value.Messages.Count));
                }
                else
                {
                    hasApprovalResponses = true;
                }
            }
            else
            {
                // Only update statefulMessageCount when there are no pending approval workflows
                _dispatcher.Dispatch(new ChatActions.SetStatefulCountAction(
                    _chatStore.Value.ConversationId is not null ? _chatStore.Value.Messages.Count : 0));
            }

            // Mark document as finalized when streaming completes (for Predictive State Updates)
            if (IsPredictiveStateEndpoint && _artifactStore.Value.CurrentDocumentState is not null)
            {
                _dispatcher.Dispatch(new SetDocumentPreviewAction(false));
                _stateChanged?.Invoke();
            }
        } while (hasApprovalResponses);
    }

    /// <inheritdoc />
    public void ResolveApproval(bool approved)
    {
        _approvalTaskSource?.TrySetResult(approved);
    }

    /// <inheritdoc />
    public void CancelAnyCurrentResponse()
    {
        // If a response was cancelled while streaming, include it in the conversation so it's not lost
        if (_streamingMessage is not null)
        {
            _dispatcher.Dispatch(new ChatActions.AddMessageAction(_streamingMessage));
        }

        _currentResponseCancellation?.Cancel();
        _dispatcher.Dispatch(new AgentActions.SetRunningAction(false));
        _streamingMessage = null;
        _dispatcher.Dispatch(new ChatActions.UpdateResponseMessageAction(null));
        _dispatcher.Dispatch(new ChatActions.SetPendingApprovalAction(null));
        _approvalTaskSource?.TrySetCanceled();
        _approvalTaskSource = null;
        _approvalHandler.ClearPendingApprovals();
    }

    /// <inheritdoc />
    public void ResetConversation(string systemPrompt, string selectedEndpointPath)
    {
        CancelAnyCurrentResponse();
        _dispatcher.Dispatch(new ChatActions.ClearMessagesAction());
        _dispatcher.Dispatch(new ChatActions.AddMessageAction(new(ChatRole.System, systemPrompt)));
        _chatOptions.ConversationId = null;
        _dispatcher.Dispatch(new PlanActions.ClearPlanAction());
        _dispatcher.Dispatch(new AgentActions.SetAuthorNameAction(null));
        _dispatcher.Dispatch(new ClearArtifactsAction());
        _seenFunctionCallIds.Clear();
        _seenFunctionResultCallIds.Clear();
        _approvalHandler.ClearPendingApprovals();
        _checkpointService.Clear();
        _lastDiffBefore = null;
        _lastDiffAfter = null;

        InitializeEndpointState(selectedEndpointPath);
    }

    /// <inheritdoc />
    public void HandlePanic(string selectedEndpointPath)
    {
        CancelAnyCurrentResponse();
        var checkpoint = _checkpointService.GetLatestCheckpoint();
        if (checkpoint is not null)
        {
            RestoreFromCheckpoint(checkpoint, selectedEndpointPath);
        }

        ResetConversationContext();
        _stateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void HandleCheckpointRevert(string checkpointId, string selectedEndpointPath)
    {
        CancelAnyCurrentResponse();
        var checkpoint = _checkpointService.RevertToCheckpoint(checkpointId);
        if (checkpoint is null)
        {
            return;
        }

        RestoreFromCheckpoint(checkpoint, selectedEndpointPath);
        ResetConversationContext();
        _stateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void InitializeEndpointState(string endpointPath)
    {
        if (endpointPath.Equals("shared_state", StringComparison.OrdinalIgnoreCase))
        {
            _stateManager.Initialize();
        }
        else
        {
            _stateManager.Clear();
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, int>? GetToolInvocationSummary()
    {
        var steps = _observabilityService.GetSteps();
        if (steps.Count == 0)
        {
            return null;
        }

        return steps
            .GroupBy(s => s.ToolName)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Restores all Fluxor state stores from a checkpoint snapshot.
    /// Shared by <see cref="HandlePanic"/> and <see cref="HandleCheckpointRevert"/> for DRY checkpoint restoration.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to restore from.</param>
    /// <param name="selectedEndpointPath">The currently selected endpoint path.</param>
    private void RestoreFromCheckpoint(Checkpoint checkpoint, string selectedEndpointPath)
    {
        // Restore plan state
        if (checkpoint.PlanSnapshot is not null)
        {
            var restoredPlan = JsonSerializer.Deserialize<Plan>(checkpoint.PlanSnapshot, JsonDefaults.Options);
            if (restoredPlan is not null)
            {
                _dispatcher.Dispatch(new PlanActions.SetPlanAction(restoredPlan));
            }
            else
            {
                _dispatcher.Dispatch(new PlanActions.ClearPlanAction());
            }
        }
        else
        {
            _dispatcher.Dispatch(new PlanActions.ClearPlanAction());
        }

        // Restore recipe state
        bool isSharedState = selectedEndpointPath.Equals("shared_state", StringComparison.OrdinalIgnoreCase);
        if (checkpoint.RecipeSnapshot is not null && isSharedState)
        {
            var recipe = JsonSerializer.Deserialize<Recipe>(checkpoint.RecipeSnapshot, JsonDefaults.Options);
            if (recipe is not null)
            {
                _stateManager.UpdateFromServerSnapshot(recipe);
                _dispatcher.Dispatch(new SetRecipeAction(recipe));
            }
        }

        // Restore document state
        bool isPredictiveState = selectedEndpointPath.Equals("predictive_state_updates", StringComparison.OrdinalIgnoreCase);
        if (checkpoint.DocumentSnapshot is not null && isPredictiveState)
        {
            var restoredDoc = JsonSerializer.Deserialize<DocumentState>(checkpoint.DocumentSnapshot, JsonDefaults.Options);
            if (restoredDoc is not null)
            {
                _dispatcher.Dispatch(new SetDocumentAction(restoredDoc));
            }
            _dispatcher.Dispatch(new SetDocumentPreviewAction(false));
        }
        else if (isPredictiveState)
        {
            _dispatcher.Dispatch(new ClearArtifactsAction());
        }

        // Trim messages to checkpoint boundary via Fluxor dispatch
        if (checkpoint.MessageCount < _chatStore.Value.Messages.Count)
        {
            _dispatcher.Dispatch(new ChatActions.TrimMessagesAction(checkpoint.MessageCount));
        }
    }

    /// <summary>
    /// Resets conversation context after a checkpoint restore or panic action.
    /// Clears the conversation ID, syncs stateful message count, and clears diff state.
    /// </summary>
    private void ResetConversationContext()
    {
        _chatOptions.ConversationId = null;
        _dispatcher.Dispatch(new ChatActions.SetConversationIdAction(null));
        _dispatcher.Dispatch(new ChatActions.SetStatefulCountAction(_chatStore.Value.Messages.Count));
        _lastDiffBefore = null;
        _lastDiffAfter = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _currentResponseCancellation?.Cancel();
        _currentResponseCancellation?.Dispose();
        _approvalTaskSource?.TrySetCanceled();
    }
}
