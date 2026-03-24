using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AGUIDojoClient.Helpers;
using AGUIDojoClient.Models;
using AGUIDojoClient.Shared;
using AGUIDojoClient.Store.SessionManager;
using Fluxor;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Coordinates the AG-UI SSE streaming loop, governance actions, and conversation lifecycle.
/// </summary>
public sealed class AgentStreamingService : IAgentStreamingService
{
    private const int MaxConcurrentStreams = 3;
    private const int MaxQueuedStreams = 5;
    private const int MaxRetryAttempts = 3;

    private readonly IDispatcher _dispatcher;
    private readonly IState<SessionManagerState> _sessionStore;
    private readonly IApprovalHandler _approvalHandler;
    private readonly IJsonPatchApplier _jsonPatchApplier;
    private readonly IStateManager _stateManager;
    private readonly IObservabilityService _observabilityService;
    private readonly ICheckpointService _checkpointService;
    private readonly IAGUIChatClientFactory _chatClientFactory;
    private readonly IToolComponentRegistry _toolRegistry;
    private readonly ConcurrentDictionary<string, SessionStreamingContext> _sessionContexts = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _notificationDismissals = new();
    private readonly object _streamGate = new();
    private readonly object _notificationGate = new();
    private readonly Queue<QueuedStreamRequest> _queuedRequests = [];
    private readonly List<SessionNotification> _notifications = [];
    private readonly HashSet<string> _runningSessions = [];

    private Action? _throttledStateChanged;
    private Action? _stateChanged;
    private Func<Func<Task>, Task>? _invokeAsync;
    private volatile SseStreamSnapshot? _currentStreamMetrics;
    private readonly IRiskAssessmentService _riskAssessmentService;
    private readonly IAutonomyPolicyService _autonomyPolicyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStreamingService"/> class.
    /// </summary>
    public AgentStreamingService(
        IDispatcher dispatcher,
        IState<SessionManagerState> sessionStore,
        IApprovalHandler approvalHandler,
        IJsonPatchApplier jsonPatchApplier,
        IStateManager stateManager,
        IObservabilityService observabilityService,
        ICheckpointService checkpointService,
        IAGUIChatClientFactory chatClientFactory,
        IToolComponentRegistry toolRegistry,
        IRiskAssessmentService riskAssessmentService,
        IAutonomyPolicyService autonomyPolicyService)
    {
        _dispatcher = dispatcher;
        _sessionStore = sessionStore;
        _approvalHandler = approvalHandler;
        _jsonPatchApplier = jsonPatchApplier;
        _stateManager = stateManager;
        _observabilityService = observabilityService;
        _checkpointService = checkpointService;
        _chatClientFactory = chatClientFactory;
        _toolRegistry = toolRegistry;
        _riskAssessmentService = riskAssessmentService;
        _autonomyPolicyService = autonomyPolicyService;
        _sessionStore.StateChanged += OnSessionStoreChanged;
    }

    /// <inheritdoc />
    public object? LastDiffBefore => GetActiveContext().LastDiffBefore;

    /// <inheritdoc />
    public object? LastDiffAfter => GetActiveContext().LastDiffAfter;

    /// <inheritdoc />
    public string LastDiffTitle => GetActiveContext().LastDiffTitle;

    /// <inheritdoc />
    public ChatOptions ChatOptions => GetActiveContext().ChatOptions;

    /// <inheritdoc />
    public IReadOnlyList<SessionNotification> Notifications
    {
        get
        {
            lock (_notificationGate)
            {
                return _notifications.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? NotificationsChanged;

    /// <inheritdoc />
    public SseStreamSnapshot? CurrentStreamMetrics => _currentStreamMetrics;

    /// <inheritdoc />
    public void SetUiCallbacks(Action throttledStateChanged, Action stateChanged, Func<Func<Task>, Task> invokeAsync)
    {
        _throttledStateChanged = throttledStateChanged;
        _stateChanged = stateChanged;
        _invokeAsync = invokeAsync;
    }

    /// <inheritdoc />
    public bool CanQueueResponse(string sessionId)
    {
        SessionStreamingContext context = GetOrCreateContext(sessionId);
        lock (_streamGate)
        {
            return _runningSessions.Contains(sessionId)
                || context.IsQueued
                || _runningSessions.Count < MaxConcurrentStreams
                || _queuedRequests.Count < MaxQueuedStreams;
        }
    }

    /// <inheritdoc />
    public Task ProcessAgentResponseAsync(string sessionId)
    {
        if (!TryGetSession(sessionId, out SessionEntry entry))
        {
            return Task.CompletedTask;
        }

        SessionStreamingContext context = GetOrCreateContext(sessionId);
        lock (_streamGate)
        {
            if (_runningSessions.Contains(sessionId) && context.ActiveResponseTask is not null)
            {
                return context.ActiveResponseTask;
            }

            if (context.IsQueued)
            {
                return Task.CompletedTask;
            }

            if (_runningSessions.Count >= MaxConcurrentStreams)
            {
                if (_queuedRequests.Count >= MaxQueuedStreams)
                {
                    throw new InvalidOperationException("The session streaming queue is full.");
                }

                context.IsQueued = true;
                _queuedRequests.Enqueue(new QueuedStreamRequest(sessionId));
                return Task.CompletedTask;
            }

            return StartSessionStream(entry.Metadata.Id, context);
        }
    }

    /// <inheritdoc />
    public void ResolveApproval(string sessionId, bool approved)
    {
        if (_sessionContexts.TryGetValue(sessionId, out SessionStreamingContext? context))
        {
            context.ApprovalTaskSource?.TrySetResult(approved);
        }

        DismissNotifications(notification =>
            notification.Type == NotificationType.ApprovalRequired
            && string.Equals(notification.SessionId, sessionId, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public void CancelResponse(string sessionId)
    {
        DismissNotifications(notification => string.Equals(notification.SessionId, sessionId, StringComparison.Ordinal));
        _dispatcher.Dispatch(new SessionActions.SetPendingApprovalAction(sessionId, null));
        _approvalHandler.ClearPendingApprovals(sessionId);

        if (!_sessionContexts.TryGetValue(sessionId, out SessionStreamingContext? context))
        {
            return;
        }

        lock (_streamGate)
        {
            _runningSessions.Remove(sessionId);
            RemoveQueuedRequests(sessionId);
            context.IsQueued = false;
        }

        if (context.StreamingMessage is not null)
        {
            _dispatcher.Dispatch(new SessionActions.AddMessageAction(sessionId, context.StreamingMessage));
        }

        context.StreamingMessage = null;
        context.ResponseCancellation?.Cancel();
        context.ResponseCancellation?.Dispose();
        context.ResponseCancellation = null;
        context.ActiveResponseTask = null;

        _dispatcher.Dispatch(new SessionActions.SetRunningAction(sessionId, false));
        _dispatcher.Dispatch(new SessionActions.UpdateResponseMessageAction(sessionId, null));
        context.ApprovalTaskSource?.TrySetCanceled();
        context.ApprovalTaskSource = null;
    }

    /// <inheritdoc />
    public void ResetConversation(string sessionId, string systemPrompt)
    {
        CancelResponse(sessionId);
        _dispatcher.Dispatch(new SessionActions.ClearUndoGracePeriodAction(sessionId));
        _dispatcher.Dispatch(new SessionActions.ClearMessagesAction(sessionId));
        _dispatcher.Dispatch(new SessionActions.AddMessageAction(sessionId, new ChatMessage(ChatRole.System, systemPrompt)));
        _dispatcher.Dispatch(new SessionActions.ClearPlanAction(sessionId));
        _dispatcher.Dispatch(new SessionActions.SetAuthorNameAction(sessionId, null));
        _dispatcher.Dispatch(new SessionActions.ClearArtifactsAction(sessionId));
        _approvalHandler.ClearPendingApprovals(sessionId);
        _checkpointService.Clear(sessionId);

        SessionStreamingContext context = GetOrCreateContext(sessionId);
        context.ResetConversationState();

        SyncSessionState(sessionId);
    }

    /// <inheritdoc />
    public void HandlePanic(string sessionId)
    {
        CancelResponse(sessionId);
        _dispatcher.Dispatch(new SessionActions.ClearUndoGracePeriodAction(sessionId));
        Checkpoint? checkpoint = _checkpointService.GetLatestCheckpoint(sessionId);
        if (checkpoint is not null)
        {
            RestoreFromCheckpoint(sessionId, checkpoint);
        }

        ResetConversationContext(sessionId);
        _stateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void HandleCheckpointRevert(string sessionId, string checkpointId)
    {
        CancelResponse(sessionId);
        _dispatcher.Dispatch(new SessionActions.ClearUndoGracePeriodAction(sessionId));
        Checkpoint? checkpoint = _checkpointService.RevertToCheckpoint(sessionId, checkpointId);
        if (checkpoint is null)
        {
            return;
        }

        RestoreFromCheckpoint(sessionId, checkpoint);
        ResetConversationContext(sessionId);
        _stateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void SyncSessionState(string sessionId)
    {
        SessionStreamingContext context = GetOrCreateContext(sessionId);
        EnsureContextCorrelation(sessionId, context);
        ApplyModelRouting(sessionId, context);

        Recipe? recipe = GetSessionState(sessionId).CurrentRecipe;
        if (recipe is not null)
        {
            _stateManager.UpdateFromServerSnapshot(recipe);
        }
        else
        {
            _stateManager.Clear();
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, int>? GetToolInvocationSummary()
    {
        IReadOnlyList<ReasoningStep> steps = _observabilityService.GetSteps();
        if (steps.Count == 0)
        {
            return null;
        }

        return steps
            .GroupBy(s => s.ToolName)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <inheritdoc />
    public void DismissNotification(string notificationId)
    {
        if (string.IsNullOrWhiteSpace(notificationId))
        {
            return;
        }

        bool removed;
        lock (_notificationGate)
        {
            removed = _notifications.RemoveAll(notification => string.Equals(notification.Id, notificationId, StringComparison.Ordinal)) > 0;
        }

        CancelNotificationDismissal(notificationId);

        if (removed)
        {
            _ = RaiseNotificationsChangedAsync();
        }
    }

    private SessionStreamingContext GetActiveContext() => GetOrCreateContext(SessionSelectors.GetActiveSessionId(_sessionStore.Value));

    private SessionStreamingContext GetOrCreateContext(string sessionId) => _sessionContexts.GetOrAdd(sessionId, _ => new SessionStreamingContext());

    private void ApplyModelRouting(string sessionId, SessionStreamingContext context)
    {
        if (!TryGetSession(sessionId, out SessionEntry entry))
        {
            context.ChatOptions.ModelId = null;
            context.ChatOptions.AdditionalProperties?.Remove("preferredModelId");
            return;
        }

        string? preferredModelId = entry.Metadata.PreferredModelId;
        if (!string.IsNullOrWhiteSpace(preferredModelId))
        {
            context.ChatOptions.ModelId = preferredModelId;
            context.ChatOptions.AdditionalProperties ??= [];
            context.ChatOptions.AdditionalProperties["preferredModelId"] = preferredModelId;
            return;
        }

        context.ChatOptions.ModelId = null;
        context.ChatOptions.AdditionalProperties?.Remove("preferredModelId");
    }

    private void OnSessionStoreChanged(object? sender, EventArgs e)
    {
        HashSet<string> liveSessionIds = _sessionStore.Value.Sessions.Keys.ToHashSet(StringComparer.Ordinal);
        foreach ((string sessionId, _) in _sessionContexts)
        {
            if (!liveSessionIds.Contains(sessionId))
            {
                RemoveSessionContext(sessionId);
            }
        }
    }

    private void RemoveSessionContext(string sessionId)
    {
        lock (_streamGate)
        {
            _runningSessions.Remove(sessionId);
            RemoveQueuedRequests(sessionId);
        }

        DismissNotifications(notification => string.Equals(notification.SessionId, sessionId, StringComparison.Ordinal));

        if (_sessionContexts.TryRemove(sessionId, out SessionStreamingContext? context))
        {
            context.Dispose();
        }
    }

    private void RemoveQueuedRequests(string sessionId)
    {
        if (_queuedRequests.Count == 0)
        {
            return;
        }

        List<QueuedStreamRequest> remaining = [];
        while (_queuedRequests.Count > 0)
        {
            QueuedStreamRequest request = _queuedRequests.Dequeue();
            if (!string.Equals(request.SessionId, sessionId, StringComparison.Ordinal))
            {
                remaining.Add(request);
            }
        }

        foreach (QueuedStreamRequest request in remaining)
        {
            _queuedRequests.Enqueue(request);
        }
    }

    private Task StartSessionStream(string sessionId, SessionStreamingContext context)
    {
        context.IsQueued = false;
        _runningSessions.Add(sessionId);
        Task streamTask = RunSessionResponseAsync(sessionId, context);
        context.ActiveResponseTask = streamTask;
        return streamTask;
    }

    private async Task RunSessionResponseAsync(string sessionId, SessionStreamingContext context)
    {
        bool shouldPromoteQueuedStream = false;

        try
        {
            if (!TryGetSession(sessionId, out _))
            {
                return;
            }

            IChatClient chatClient = _chatClientFactory.CreateClient();
            context.ChatOptions.ConversationId = GetSessionState(sessionId).ConversationId;
            EnsureContextCorrelation(sessionId, context);
            ApplyModelRouting(sessionId, context);

            bool hasApprovalResponses;

            do
            {
                hasApprovalResponses = false;
                bool encounteredError = false;
                bool wasCancelled = false;
                string? errorNotificationMessage = null;
                List<FunctionResultContent> approvalResponses = [];
                bool sawDocumentPreview = false;

                var responseText = new TextContent(string.Empty);
                var streamingMessage = new ChatMessage(ChatRole.Assistant, [responseText]);
                var rawResponseText = new StringBuilder();
                double? responseConfidence = null;
                context.StreamingMessage = streamingMessage;
                _dispatcher.Dispatch(new SessionActions.UpdateResponseMessageAction(sessionId, streamingMessage));
                _stateChanged?.Invoke();

                var responseCancellation = new CancellationTokenSource();
                context.ResponseCancellation = responseCancellation;
                _dispatcher.Dispatch(new SessionActions.SetRunningAction(sessionId, true));

                try
                {
                    SessionState session = GetSessionState(sessionId);
                    bool receivedFirstContent = false;
                    var streamStopwatch = Stopwatch.StartNew();
                    int eventCount = 0;
                    int retryCount = 0;

                    _currentStreamMetrics = new SseStreamSnapshot { ConnectionState = SseConnectionState.Connecting };
                    SseStreamMetrics.StreamsStarted.Add(1);
                    _throttledStateChanged?.Invoke();

                    // Retry loop: retries connection failures that occur before first content arrives.
                    // Once content has been received, failures are not retried to avoid duplicate data.
                    while (true)
                    {
                        try
                        {
                            await foreach (var update in chatClient.GetStreamingResponseAsync(
                                // Correlation IDs are transport metadata only. Always resend the
                                // full active branch and let server-owned context policy trim it.
                                session.Messages,
                                context.ChatOptions,
                                responseCancellation.Token))
                            {
                                if (!receivedFirstContent)
                                {
                                    receivedFirstContent = true;
                                    double ttfMs = streamStopwatch.Elapsed.TotalMilliseconds;
                                    SseStreamMetrics.FirstTokenLatency.Record(ttfMs);
                                    _currentStreamMetrics = _currentStreamMetrics with
                                    {
                                        ConnectionState = SseConnectionState.Streaming,
                                        FirstTokenLatencyMs = ttfMs,
                                        RetryCount = retryCount,
                                    };
                                    _throttledStateChanged?.Invoke();
                                }

                                eventCount++;

                                foreach (AIContent content in update.Contents)
                                {
                                    if (content is FunctionCallContent fcc && _approvalHandler.TryExtractApprovalRequest(fcc, out PendingApproval? approval) && approval is not null)
                                    {
                                        approval.SessionId = sessionId;
                                        var riskLevel = _riskAssessmentService.AssessRisk(approval.FunctionName);
                                        var autonomyLevel = _sessionStore.Value.AutonomyLevel;

                                        bool shouldAutoDecide = _autonomyPolicyService.ShouldAutoDecide(autonomyLevel, riskLevel);
                                        bool approved;

                                        if (shouldAutoDecide)
                                        {
                                            // Auto-approve: resolve immediately without blocking the user
                                            approved = true;
                                        }
                                        else
                                        {
                                            // Human-in-the-loop: show approval UI and wait
                                            _dispatcher.Dispatch(new SessionActions.SetPendingApprovalAction(sessionId, approval));
                                            NotifyBackgroundApprovalRequired(sessionId, approval);
                                            context.ApprovalTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                            _stateChanged?.Invoke();

                                            approved = await context.ApprovalTaskSource.Task;
                                            _dispatcher.Dispatch(new SessionActions.SetPendingApprovalAction(sessionId, null));
                                            context.ApprovalTaskSource = null;
                                            DismissNotification(approval.ApprovalId);
                                        }

                                        // Record audit entry for all decisions
                                        var auditEntry = new AuditEntry
                                        {
                                            Id = Guid.NewGuid().ToString("N"),
                                            ApprovalId = approval.ApprovalId,
                                            FunctionName = approval.FunctionName,
                                            RiskLevel = riskLevel,
                                            AutonomyLevel = autonomyLevel,
                                            WasApproved = approved,
                                            WasAutoDecided = shouldAutoDecide,
                                            SessionId = sessionId,
                                        };
                                        _dispatcher.Dispatch(new SessionActions.AddAuditEntryAction(sessionId, auditEntry));

                                        var response = _approvalHandler.CreateApprovalResponse(sessionId, approval.ApprovalId, approved);
                                        approvalResponses.Add(response);
                                        streamingMessage.Contents.Add(response);
                                        _stateChanged?.Invoke();
                                    }
                                    else if (content is DataContent dataContent && TryHandleDataContent(sessionId, context, dataContent, ref sawDocumentPreview))
                                    {
                                    }
                                }

                                foreach (AIContent content in update.Contents)
                                {
                                    if (content is TextContent)
                                    {
                                        continue;
                                    }

                                    if (content is FunctionCallContent fcc)
                                    {
                                        if (fcc.CallId is not null && context.SeenFunctionCallIds.Add(fcc.CallId))
                                        {
                                            streamingMessage.Contents.Add(content);
                                            _observabilityService.StartToolCall(fcc.CallId, fcc.Name ?? "unknown", fcc.Arguments);

                                            if (fcc.Name is not null)
                                            {
                                                context.FunctionCallIdToToolName[fcc.CallId] = fcc.Name;
                                            }
                                        }
                                    }
                                    else if (content is DataContent dc)
                                    {
                                        ChatHelpers.ConsolidateDataContent(streamingMessage, dc);
                                    }
                                    else if (content is FunctionResultContent frc)
                                    {
                                        if (frc.CallId is not null && context.SeenFunctionResultCallIds.Add(frc.CallId))
                                        {
                                            streamingMessage.Contents.Add(content);
                                            _observabilityService.CompleteToolCall(frc.CallId, frc.Result);
                                            TryDispatchCanvasToolArtifact(sessionId, context, frc);
                                        }
                                        else if (frc.CallId is null)
                                        {
                                            streamingMessage.Contents.Add(content);
                                        }
                                    }
                                    else
                                    {
                                        streamingMessage.Contents.Add(content);
                                    }
                                }

                                rawResponseText.Append(update.Text);
                                responseText.Text = ConfidenceMarkers.StripLeadingConfidenceComment(rawResponseText.ToString(), out double? parsedConfidence);
                                if (parsedConfidence.HasValue)
                                {
                                    responseConfidence = parsedConfidence;
                                }

                                if (responseConfidence.HasValue)
                                {
                                    SetConfidenceScore(streamingMessage, responseConfidence.Value);
                                }

                                context.ChatOptions.ConversationId = update.ConversationId;
                                _dispatcher.Dispatch(new SessionActions.SetConversationIdAction(sessionId, update.ConversationId));

                                // Capture the AG-UI thread ID from the first update and store
                                // it on the context for cross-turn reuse.
                                string? aguiThreadId = null;
                                if (update.AdditionalProperties?.TryGetValue("agui_thread_id", out string? rawAguiThreadId) is true
                                    && !string.IsNullOrEmpty(rawAguiThreadId))
                                {
                                    aguiThreadId = rawAguiThreadId;
                                    context.AguiThreadId = aguiThreadId;
                                }

                                string? serverSessionId = null;
                                if (update.AdditionalProperties?.TryGetValue("server_session_id", out string? rawServerSessionId) is true
                                    && !string.IsNullOrEmpty(rawServerSessionId))
                                {
                                    serverSessionId = rawServerSessionId;
                                }

                                if ((aguiThreadId is not null || serverSessionId is not null) &&
                                    TryGetSession(sessionId, out SessionEntry currentEntry) &&
                                    (!string.Equals(currentEntry.Metadata.AguiThreadId, aguiThreadId ?? currentEntry.Metadata.AguiThreadId, StringComparison.Ordinal) ||
                                     !string.Equals(currentEntry.Metadata.ServerSessionId, serverSessionId ?? currentEntry.Metadata.ServerSessionId, StringComparison.Ordinal)))
                                {
                                    _dispatcher.Dispatch(new SessionActions.SetSessionCorrelationAction(
                                        sessionId,
                                        aguiThreadId ?? currentEntry.Metadata.AguiThreadId,
                                        serverSessionId ?? currentEntry.Metadata.ServerSessionId));
                                }

                                if (update.AuthorName is not null)
                                {
                                    _dispatcher.Dispatch(new SessionActions.SetAuthorNameAction(sessionId, update.AuthorName));
                                    streamingMessage.AuthorName = update.AuthorName;
                                }

                                _currentStreamMetrics = _currentStreamMetrics with
                                {
                                    EventCount = eventCount,
                                    DurationMs = streamStopwatch.Elapsed.TotalMilliseconds,
                                };
                                _throttledStateChanged?.Invoke();
                            }

                            break; // Stream completed normally
                        }
                        catch (HttpRequestException) when (!receivedFirstContent && retryCount < MaxRetryAttempts)
                        {
                            retryCount++;
                            SseStreamMetrics.RetryAttempts.Add(1);
                            _currentStreamMetrics = _currentStreamMetrics with
                            {
                                ConnectionState = SseConnectionState.Retrying,
                                RetryCount = retryCount,
                            };
                            _throttledStateChanged?.Invoke();

                            // Exponential backoff: 1s, 2s, 4s
                            int delayMs = 1000 * (1 << (retryCount - 1));
                            await Task.Delay(delayMs, responseCancellation.Token);

                            _currentStreamMetrics = _currentStreamMetrics with
                            {
                                ConnectionState = SseConnectionState.Connecting,
                            };
                            _throttledStateChanged?.Invoke();
                        }
                    }

                    if (!responseConfidence.HasValue)
                    {
                        responseConfidence = ConfidenceMarkers.EstimateConfidenceScore(responseText.Text);
                        SetConfidenceScore(streamingMessage, responseConfidence.Value);
                    }

                    streamStopwatch.Stop();
                    SseStreamMetrics.StreamDuration.Record(streamStopwatch.Elapsed.TotalMilliseconds);
                    SseStreamMetrics.EventsPerStream.Record(eventCount);
                    SseStreamMetrics.StreamsCompleted.Add(1, new KeyValuePair<string, object?>("outcome", "completed"));
                    _currentStreamMetrics = _currentStreamMetrics with
                    {
                        ConnectionState = SseConnectionState.Completed,
                        DurationMs = streamStopwatch.Elapsed.TotalMilliseconds,
                    };
                }
                catch (OperationCanceledException)
                {
                    SseStreamMetrics.StreamsCompleted.Add(1, new KeyValuePair<string, object?>("outcome", "cancelled"));
                    _currentStreamMetrics = (_currentStreamMetrics ?? new SseStreamSnapshot()) with { ConnectionState = SseConnectionState.Idle };
                    wasCancelled = true;
                }
                catch (HttpRequestException ex)
                {
                    SseStreamMetrics.StreamsCompleted.Add(1, new KeyValuePair<string, object?>("outcome", "failed"));
                    _currentStreamMetrics = (_currentStreamMetrics ?? new SseStreamSnapshot()) with { ConnectionState = SseConnectionState.Error };
                    encounteredError = true;
                    errorNotificationMessage = ex.Message;
                    responseText.Text = $"Error: {ex.Message}";
                    Components.Pages.Chat.ChatMessageItem.NotifyChanged(streamingMessage);
                }
                catch (Exception ex)
                {
                    SseStreamMetrics.StreamsCompleted.Add(1, new KeyValuePair<string, object?>("outcome", "failed"));
                    _currentStreamMetrics = (_currentStreamMetrics ?? new SseStreamSnapshot()) with { ConnectionState = SseConnectionState.Error };
                    encounteredError = true;
                    errorNotificationMessage = ex.Message;
                    responseText.Text = $"An unexpected error occurred: {ex.Message}";
                    Components.Pages.Chat.ChatMessageItem.NotifyChanged(streamingMessage);
                }
                finally
                {
                    if (ReferenceEquals(context.ResponseCancellation, responseCancellation))
                    {
                        context.ResponseCancellation?.Dispose();
                        context.ResponseCancellation = null;
                    }
                }

                if (ReferenceEquals(context.StreamingMessage, streamingMessage))
                {
                    _dispatcher.Dispatch(new SessionActions.SetRunningAction(sessionId, false));
                    _dispatcher.Dispatch(new SessionActions.AddMessageAction(sessionId, streamingMessage));
                    context.StreamingMessage = null;
                    _dispatcher.Dispatch(new SessionActions.UpdateResponseMessageAction(sessionId, null));

                    if (encounteredError)
                    {
                        _dispatcher.Dispatch(new SessionActions.SetSessionStatusAction(sessionId, SessionStatus.Error));
                        NotifyBackgroundError(sessionId, errorNotificationMessage);
                    }
                }

                if (approvalResponses.Count > 0)
                {
                    bool hasAnyRejection = approvalResponses.Any(ChatHelpers.IsRejectionResponse);

                    foreach (FunctionResultContent response in approvalResponses)
                    {
                        _dispatcher.Dispatch(new SessionActions.AddMessageAction(sessionId, new ChatMessage(ChatRole.Tool, [response])));
                    }

                    if (hasAnyRejection)
                    {
                        hasApprovalResponses = false;
                        context.ChatOptions.ConversationId = null;
                        _dispatcher.Dispatch(new SessionActions.SetConversationIdAction(sessionId, null));
                    }
                    else
                    {
                        hasApprovalResponses = true;
                    }
                }

                if (sawDocumentPreview && GetSessionState(sessionId).CurrentDocumentState is not null)
                {
                    _dispatcher.Dispatch(new SessionActions.SetDocumentPreviewAction(sessionId, false));
                    _stateChanged?.Invoke();
                }

                if (!wasCancelled && !encounteredError && !hasApprovalResponses)
                {
                    NotifyBackgroundCompletion(sessionId);
                }
            }
            while (hasApprovalResponses);
        }
        finally
        {
            lock (_streamGate)
            {
                _runningSessions.Remove(sessionId);
                if (_sessionContexts.TryGetValue(sessionId, out SessionStreamingContext? currentContext)
                    && ReferenceEquals(currentContext, context))
                {
                    context.ActiveResponseTask = null;
                }

                shouldPromoteQueuedStream = _queuedRequests.Count > 0;
            }

            if (shouldPromoteQueuedStream)
            {
                await StartQueuedStreamsAsync();
            }
        }
    }

    private async Task StartQueuedStreamsAsync()
    {
        while (true)
        {
            QueuedStreamRequest? request = null;
            SessionStreamingContext? context = null;

            lock (_streamGate)
            {
                if (_runningSessions.Count >= MaxConcurrentStreams)
                {
                    return;
                }

                while (_queuedRequests.Count > 0)
                {
                    QueuedStreamRequest next = _queuedRequests.Dequeue();
                    if (!TryGetSession(next.SessionId, out _))
                    {
                        _sessionContexts.TryRemove(next.SessionId, out _);
                        continue;
                    }

                    context = GetOrCreateContext(next.SessionId);
                    context.IsQueued = false;
                    request = next;
                    break;
                }
            }

            if (request is null || context is null)
            {
                return;
            }

            await InvokeOnUiAsync(() =>
            {
                lock (_streamGate)
                {
                    if (_runningSessions.Count >= MaxConcurrentStreams)
                    {
                        context.IsQueued = true;
                        _queuedRequests.Enqueue(request);
                        return Task.CompletedTask;
                    }

                    _ = StartSessionStream(request.SessionId, context);
                }

                return Task.CompletedTask;
            });
        }
    }

    private Task InvokeOnUiAsync(Func<Task> callback) => _invokeAsync is null ? callback() : _invokeAsync(callback);

    private bool IsBackgroundSession(string sessionId) =>
        !string.Equals(SessionSelectors.GetActiveSessionId(_sessionStore.Value), sessionId, StringComparison.Ordinal);

    private bool TryGetSession(string sessionId, out SessionEntry entry) => SessionSelectors.TryGetSession(_sessionStore.Value, sessionId, out entry);

    private SessionState GetSessionState(string sessionId) => SessionSelectors.GetSessionStateOrDefault(_sessionStore.Value, sessionId);

    private void EnsureContextCorrelation(string sessionId, SessionStreamingContext context)
    {
        if (!TryGetSession(sessionId, out SessionEntry entry))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(context.AguiThreadId))
        {
            string aguiThreadId = string.IsNullOrWhiteSpace(entry.Metadata.AguiThreadId)
                ? SessionMetadata.CreateAguiThreadId()
                : entry.Metadata.AguiThreadId;

            context.AguiThreadId = aguiThreadId;

            if (!string.Equals(entry.Metadata.AguiThreadId, aguiThreadId, StringComparison.Ordinal))
            {
                _dispatcher.Dispatch(new SessionActions.SetSessionCorrelationAction(
                    sessionId,
                    aguiThreadId,
                    entry.Metadata.ServerSessionId));
            }
        }

        if (string.IsNullOrWhiteSpace(context.AguiThreadId))
        {
            context.ChatOptions.AdditionalProperties?.Remove("agui_thread_id");
            return;
        }

        context.ChatOptions.AdditionalProperties ??= [];
        context.ChatOptions.AdditionalProperties["agui_thread_id"] = context.AguiThreadId;
    }

    private static void SetConfidenceScore(ChatMessage message, double confidenceScore)
    {
        message.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        message.AdditionalProperties[ConfidenceMarkers.ConfidenceScoreKey] = confidenceScore;
    }

    private void StartUndoGracePeriod(string sessionId, Checkpoint checkpoint, string summary)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        _dispatcher.Dispatch(new SessionActions.StartUndoGracePeriodAction(
            sessionId,
            checkpoint.Id,
            checkpoint.Label,
            summary,
            startedAt,
            startedAt.AddSeconds(5)));
    }

    private void RestoreFromCheckpoint(string sessionId, Checkpoint checkpoint)
    {
        if (checkpoint.PlanSnapshot is not null)
        {
            Plan? restoredPlan = JsonSerializer.Deserialize<Plan>(checkpoint.PlanSnapshot, JsonDefaults.Options);
            if (restoredPlan is not null)
            {
                _dispatcher.Dispatch(new SessionActions.SetPlanAction(sessionId, restoredPlan));
            }
            else
            {
                _dispatcher.Dispatch(new SessionActions.ClearPlanAction(sessionId));
            }
        }
        else
        {
            _dispatcher.Dispatch(new SessionActions.ClearPlanAction(sessionId));
        }

        _dispatcher.Dispatch(new SessionActions.ClearArtifactsAction(sessionId));

        if (checkpoint.RecipeSnapshot is not null)
        {
            Recipe? recipe = JsonSerializer.Deserialize<Recipe>(checkpoint.RecipeSnapshot, JsonDefaults.Options);
            if (recipe is not null)
            {
                _dispatcher.Dispatch(new SessionActions.SetRecipeAction(sessionId, recipe));
            }
        }

        if (checkpoint.DocumentSnapshot is not null)
        {
            DocumentState? restoredDoc = JsonSerializer.Deserialize<DocumentState>(checkpoint.DocumentSnapshot, JsonDefaults.Options);
            if (restoredDoc is not null)
            {
                _dispatcher.Dispatch(new SessionActions.SetDocumentAction(sessionId, restoredDoc));
            }

            _dispatcher.Dispatch(new SessionActions.SetDocumentPreviewAction(sessionId, false));
        }

        if (checkpoint.MessageCount < GetSessionState(sessionId).Messages.Count)
        {
            _dispatcher.Dispatch(new SessionActions.TrimMessagesAction(sessionId, checkpoint.MessageCount));
        }

        if (string.Equals(SessionSelectors.GetActiveSessionId(_sessionStore.Value), sessionId, StringComparison.Ordinal))
        {
            SyncSessionState(sessionId);
        }
    }

    private void ResetConversationContext(string sessionId)
    {
        SessionStreamingContext context = GetOrCreateContext(sessionId);
        context.ChatOptions.ConversationId = null;
        _dispatcher.Dispatch(new SessionActions.SetConversationIdAction(sessionId, null));
        context.AguiThreadId = null;
        context.ChatOptions.AdditionalProperties?.Remove("agui_thread_id");
        context.LastDiffBefore = null;
        context.LastDiffAfter = null;
        context.LastDiffTitle = "State Diff";
    }

    private bool TryHandleDataContent(string sessionId, SessionStreamingContext context, DataContent dataContent, ref bool sawDocumentPreview)
    {
        if (ChatHelpers.IsPlanStateDelta(dataContent))
        {
            SessionState session = GetSessionState(sessionId);
            if (session.Plan is not null)
            {
                List<JsonPatchOperation>? operations = ChatHelpers.TryParsePatchOperations(dataContent);
                if (operations is not null)
                {
                    Checkpoint checkpoint = _checkpointService.CreateCheckpoint(
                        sessionId,
                        "Before plan step update",
                        session.Plan,
                        GetRecipeCheckpointSnapshot(sessionId),
                        session.CurrentDocumentState,
                        session.Messages.Count);
                    _jsonPatchApplier.ApplyPatch(session.Plan, operations);
                    _dispatcher.Dispatch(new SessionActions.ApplyPlanDeltaAction(sessionId, operations));
                    StartUndoGracePeriod(sessionId, checkpoint, "Plan step updated");
                    _stateChanged?.Invoke();
                }
            }

            return true;
        }

        if (ChatHelpers.TryGetTypedEnvelopePayload(dataContent, out string? contentType, out JsonElement payload))
        {
            switch (contentType)
            {
                case ChatHelpers.PlanSnapshotType:
                    Plan? typedPlan = JsonSerializer.Deserialize<Plan>(payload.GetRawText(), JsonDefaults.Options);
                    if (typedPlan is not null)
                    {
                        ApplyPlanSnapshot(sessionId, context, typedPlan);
                    }

                    return true;

                case ChatHelpers.RecipeSnapshotType:
                    if (TryExtractRecipeFromPayload(payload, out Recipe? typedRecipe) && typedRecipe is not null)
                    {
                        ApplyRecipeSnapshot(sessionId, context, typedRecipe);
                    }

                    return true;

                case ChatHelpers.DocumentPreviewType:
                    DocumentState? typedDocument = JsonSerializer.Deserialize<DocumentState>(payload.GetRawText(), JsonDefaults.Options);
                    if (typedDocument is not null)
                    {
                        ApplyDocumentSnapshot(sessionId, typedDocument);
                        sawDocumentPreview = true;
                    }

                    return true;

                default:
                    Debug.WriteLine($"AgentStreamingService: Unrecognized DataContent $type '{contentType}'.");
                    return true;
            }
        }

        if (ChatHelpers.IsPlanSnapshot(dataContent))
        {
            Plan? plan = ChatHelpers.TryParsePlanSnapshot(dataContent);
            if (plan is not null)
            {
                ApplyPlanSnapshot(sessionId, context, plan);
            }

            return true;
        }

        if (_stateManager.TryExtractRecipeSnapshot(dataContent, out Recipe? recipe) && recipe is not null)
        {
            ApplyRecipeSnapshot(sessionId, context, recipe);
            return true;
        }

        if (ChatHelpers.TryExtractDocumentSnapshot(dataContent, out DocumentState? docState) && docState is not null)
        {
            ApplyDocumentSnapshot(sessionId, docState);
            sawDocumentPreview = true;
            return true;
        }

        return false;
    }

    private void ApplyPlanSnapshot(string sessionId, SessionStreamingContext context, Plan plan)
    {
        SessionState session = GetSessionState(sessionId);
        Checkpoint checkpoint = _checkpointService.CreateCheckpoint(
            sessionId,
            "Before plan update",
            session.Plan,
            GetRecipeCheckpointSnapshot(sessionId),
            session.CurrentDocumentState,
            session.Messages.Count);
        context.LastDiffBefore = session.Plan;
        _dispatcher.Dispatch(new SessionActions.SetPlanAction(sessionId, plan));
        context.LastDiffAfter = plan;
        context.LastDiffTitle = "Plan State Change";
        _dispatcher.Dispatch(new SessionActions.SetDiffPreviewArtifactAction(sessionId, context.LastDiffBefore, plan, context.LastDiffTitle));
        StartUndoGracePeriod(sessionId, checkpoint, "Plan updated");
        _stateChanged?.Invoke();
    }

    private void ApplyRecipeSnapshot(string sessionId, SessionStreamingContext context, Recipe recipe)
    {
        SessionState session = GetSessionState(sessionId);
        Checkpoint checkpoint = _checkpointService.CreateCheckpoint(
            sessionId,
            "Before recipe update",
            session.Plan,
            session.CurrentRecipe,
            session.CurrentDocumentState,
            session.Messages.Count);
        context.LastDiffBefore = session.CurrentRecipe;
        _dispatcher.Dispatch(new SessionActions.SetRecipeAction(sessionId, recipe));
        context.LastDiffAfter = recipe;
        context.LastDiffTitle = "Recipe State Change";
        _dispatcher.Dispatch(new SessionActions.SetDiffPreviewArtifactAction(sessionId, context.LastDiffBefore, recipe, context.LastDiffTitle));
        StartUndoGracePeriod(sessionId, checkpoint, "Recipe updated");

        if (string.Equals(SessionSelectors.GetActiveSessionId(_sessionStore.Value), sessionId, StringComparison.Ordinal))
        {
            _stateManager.UpdateFromServerSnapshot(recipe);
        }

        _stateChanged?.Invoke();
    }

    private void ApplyDocumentSnapshot(string sessionId, DocumentState docState)
    {
        SessionState session = GetSessionState(sessionId);
        if (session.CurrentDocumentState is null)
        {
            Checkpoint checkpoint = _checkpointService.CreateCheckpoint(
                sessionId,
                "Before document update",
                session.Plan,
                GetRecipeCheckpointSnapshot(sessionId),
                session.CurrentDocumentState,
                session.Messages.Count);
            StartUndoGracePeriod(sessionId, checkpoint, "Document preview added");
        }

        _dispatcher.Dispatch(new SessionActions.SetDocumentAction(sessionId, docState));
        _dispatcher.Dispatch(new SessionActions.SetDocumentPreviewAction(sessionId, true));
        _throttledStateChanged?.Invoke();
    }

    private Recipe? GetRecipeCheckpointSnapshot(string sessionId) => GetSessionState(sessionId).CurrentRecipe;

    private bool TryExtractRecipeFromPayload(JsonElement payload, out Recipe? recipe)
    {
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload.GetRawText());
        return _stateManager.TryExtractRecipeSnapshot(new DataContent(payloadBytes, "application/json"), out recipe);
    }

    private void TryDispatchCanvasToolArtifact(string sessionId, SessionStreamingContext context, FunctionResultContent frc)
    {
        if (frc.CallId is null)
        {
            return;
        }

        if (!context.FunctionCallIdToToolName.TryGetValue(frc.CallId, out string? toolName)
            || !_toolRegistry.TryGetMetadata(toolName, out ToolMetadata? metadata)
            || metadata?.IsVisual != true
            || metadata.RenderLocation != RenderLocation.CanvasPane)
        {
            return;
        }

        object? parsedData = ToolResultParser.TryParseToolResult(toolName, frc);
        if (parsedData is null)
        {
            return;
        }

        _dispatcher.Dispatch(new SessionActions.UpsertToolArtifactAction(
            sessionId,
            new ToolArtifactState
            {
                ArtifactId = frc.CallId,
                ToolName = toolName,
                Title = ToolResultParser.GetArtifactTitle(toolName, parsedData),
                ParsedData = parsedData,
                CanMoveToContext = metadata.CanTogglePlacement && !metadata.CanvasOnly,
            }));
    }

    private void NotifyBackgroundApprovalRequired(string sessionId, PendingApproval approval)
    {
        if (!IsBackgroundSession(sessionId) || !TryGetSession(sessionId, out SessionEntry entry))
        {
            return;
        }

        EnqueueNotification(new SessionNotification
        {
            Id = approval.ApprovalId,
            Type = NotificationType.ApprovalRequired,
            SessionId = sessionId,
            Title = entry.Metadata.Title,
            Message = $"Approval needed: {approval.FunctionName}",
            Timestamp = DateTimeOffset.UtcNow,
            IsUrgent = true,
            IsPersistent = true,
            DurationMilliseconds = 0,
        });
    }

    private void NotifyBackgroundCompletion(string sessionId)
    {
        if (!IsBackgroundSession(sessionId) || !TryGetSession(sessionId, out SessionEntry entry))
        {
            return;
        }

        EnqueueNotification(new SessionNotification
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = NotificationType.SessionCompleted,
            SessionId = sessionId,
            Title = entry.Metadata.Title,
            Message = "Agent finished responding",
            Timestamp = DateTimeOffset.UtcNow,
            IsUrgent = false,
            IsPersistent = false,
            DurationMilliseconds = 5000,
        });
    }

    private void NotifyBackgroundError(string sessionId, string? errorMessage)
    {
        if (!IsBackgroundSession(sessionId) || !TryGetSession(sessionId, out SessionEntry entry))
        {
            return;
        }

        EnqueueNotification(new SessionNotification
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = NotificationType.SessionError,
            SessionId = sessionId,
            Title = entry.Metadata.Title,
            Message = BuildErrorNotificationMessage(errorMessage),
            Timestamp = DateTimeOffset.UtcNow,
            IsUrgent = false,
            IsPersistent = false,
            DurationMilliseconds = 8000,
        });
    }

    private void EnqueueNotification(SessionNotification notification)
    {
        bool added;
        lock (_notificationGate)
        {
            if (_notifications.Any(existing => string.Equals(existing.Id, notification.Id, StringComparison.Ordinal)))
            {
                return;
            }

            _notifications.Add(notification);
            added = true;
        }

        if (added)
        {
            if (!notification.IsPersistent && notification.DurationMilliseconds > 0)
            {
                ScheduleNotificationDismissal(notification);
            }

            _ = RaiseNotificationsChangedAsync();
        }
    }

    private void DismissNotifications(Func<SessionNotification, bool> predicate)
    {
        HashSet<string> removedIds;
        lock (_notificationGate)
        {
            removedIds = _notifications.Where(predicate).Select(notification => notification.Id).ToHashSet(StringComparer.Ordinal);
            if (removedIds.Count == 0)
            {
                return;
            }

            _notifications.RemoveAll(notification => removedIds.Contains(notification.Id, StringComparer.Ordinal));
        }

        foreach (string notificationId in removedIds)
        {
            CancelNotificationDismissal(notificationId);
        }

        _ = RaiseNotificationsChangedAsync();
    }

    private void ScheduleNotificationDismissal(SessionNotification notification)
    {
        CancellationTokenSource dismissal = new();
        if (!_notificationDismissals.TryAdd(notification.Id, dismissal))
        {
            dismissal.Dispose();
            return;
        }

        _ = AutoDismissNotificationAsync(notification.Id, notification.DurationMilliseconds, dismissal.Token);
    }

    private async Task AutoDismissNotificationAsync(string notificationId, int delayMilliseconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delayMilliseconds, cancellationToken);
            DismissNotification(notificationId);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelNotificationDismissal(string notificationId)
    {
        if (_notificationDismissals.TryRemove(notificationId, out CancellationTokenSource? dismissal))
        {
            dismissal.Cancel();
            dismissal.Dispose();
        }
    }

    private Task RaiseNotificationsChangedAsync() => InvokeOnUiAsync(() =>
    {
        NotificationsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    });

    private static string BuildErrorNotificationMessage(string? errorMessage)
    {
        string detail = string.IsNullOrWhiteSpace(errorMessage) ? "An unexpected streaming error occurred." : errorMessage.Trim();
        return $"Error: {Truncate(detail, 100)}";
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength
            ? text
            : $"{text[..Math.Max(0, maxLength - 1)]}…";

    /// <inheritdoc />
    public void Dispose()
    {
        _sessionStore.StateChanged -= OnSessionStoreChanged;

        foreach ((string sessionId, _) in _sessionContexts)
        {
            RemoveSessionContext(sessionId);
        }

        foreach (CancellationTokenSource dismissal in _notificationDismissals.Values)
        {
            dismissal.Cancel();
            dismissal.Dispose();
        }

        _notificationDismissals.Clear();
    }

    private sealed record QueuedStreamRequest(string SessionId);
}
