using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using AGUIDojoClient.Store.SessionManager;
using Fluxor;
using Microsoft.Extensions.AI;
using Moq;

namespace AGUIDojoClient.Tests.Services;

public sealed class AgentStreamingServiceTests
{
    [Fact]
    public async Task ProcessAgentResponseAsync_WhenQueueIsFull_ThrowsAndCanQueueResponseReturnsFalse()
    {
        // Arrange
        string[] sessionIds = CreateSessionIds(9);
        BlockingChatClient[] clients = CreateBlockingClients(8);
        using AgentStreamingService service = CreateService(CreateState(sessionIds), clients);

        Task[] activeTasks =
        [
            service.ProcessAgentResponseAsync(sessionIds[0]),
            service.ProcessAgentResponseAsync(sessionIds[1]),
            service.ProcessAgentResponseAsync(sessionIds[2])
        ];

        await WaitForStartsAsync(clients[..3]);

        for (int i = 3; i < 8; i++)
        {
            await service.ProcessAgentResponseAsync(sessionIds[i]);
        }

        // Act / Assert
        Assert.False(service.CanQueueResponse(sessionIds[8]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessAgentResponseAsync(sessionIds[8]));

        await ReleaseAndDrainAsync(service, sessionIds[..8], clients, activeTasks);
    }

    [Fact]
    public async Task ProcessAgentResponseAsync_PromotesQueuedSession_WhenCapacityFreesUp()
    {
        // Arrange
        string[] sessionIds = CreateSessionIds(4);
        BlockingChatClient[] clients = CreateBlockingClients(4);
        using AgentStreamingService service = CreateService(CreateState(sessionIds), clients);

        Task[] activeTasks =
        [
            service.ProcessAgentResponseAsync(sessionIds[0]),
            service.ProcessAgentResponseAsync(sessionIds[1]),
            service.ProcessAgentResponseAsync(sessionIds[2])
        ];

        await WaitForStartsAsync(clients[..3]);

        // Act
        await service.ProcessAgentResponseAsync(sessionIds[3]);
        Assert.False(clients[3].Started.Task.IsCompleted);

        clients[0].ReleaseStream();
        await activeTasks[0];
        await clients[3].Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task promotedTask = service.ProcessAgentResponseAsync(sessionIds[3]);

        // Assert
        Assert.True(clients[3].Started.Task.IsCompleted);
        Assert.False(promotedTask.IsCompleted);

        await ReleaseAndDrainAsync(service, sessionIds[1..], clients[1..], activeTasks[1], activeTasks[2], promotedTask);
    }

    [Fact]
    public async Task ProcessAgentResponseAsync_ForRunningSession_ReturnsExistingTask()
    {
        // Arrange
        string[] sessionIds = CreateSessionIds(1);
        BlockingChatClient[] clients = CreateBlockingClients(1);
        using AgentStreamingService service = CreateService(CreateState(sessionIds), clients);

        // Act
        Task first = service.ProcessAgentResponseAsync(sessionIds[0]);
        await clients[0].Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task second = service.ProcessAgentResponseAsync(sessionIds[0]);

        // Assert
        Assert.Same(first, second);

        clients[0].ReleaseStream();
        await first;
    }

    [Fact]
    public async Task ProcessAgentResponseAsync_FollowUpTurnSendsFullActiveBranchDespiteConversationCorrelation()
    {
        const string sessionId = "session-follow-up";
        SessionManagerState state = CreateConversationState(
            sessionId,
            [
                new ChatMessage(ChatRole.System, "You are a precise trip planner."),
                new ChatMessage(ChatRole.User, "Plan a Seattle weekend."),
                new ChatMessage(ChatRole.Assistant, "Here is a first draft itinerary."),
                new ChatMessage(ChatRole.User, "Add museum stops and ferry time.")
            ],
            conversationId: "conv-follow-up",
            aguiThreadId: "thread-follow-up");

        ScriptedChatClient client = new([[CreateTextUpdate("Updated itinerary")]]);
        var (service, _, _) = CreateReducingService(state, client);

        using (service)
        {
            await service.ProcessAgentResponseAsync(sessionId);
        }

        ChatCallSnapshot call = Assert.Single(client.Calls);
        Assert.Equal("conv-follow-up", call.ConversationId);
        Assert.Equal("thread-follow-up", call.AguiThreadId);
        AssertCallMessages(
            call,
            (ChatRole.System, "You are a precise trip planner.", false, false),
            (ChatRole.User, "Plan a Seattle weekend.", false, false),
            (ChatRole.Assistant, "Here is a first draft itinerary.", false, false),
            (ChatRole.User, "Add museum stops and ferry time.", false, false));
    }

    [Fact]
    public async Task ProcessAgentResponseAsync_FirstTurnCapturesServerSessionIdFromResponseMetadata()
    {
        const string sessionId = "session-first-turn";
        SessionManagerState state = CreateConversationState(
            sessionId,
            [
                new ChatMessage(ChatRole.System, "You are a helpful planner."),
                new ChatMessage(ChatRole.User, "Start a new session.")
            ],
            aguiThreadId: "thread-first-turn");

        ScriptedChatClient client = new(
            [[CreateTextUpdate("Started", aguiThreadId: "thread-first-turn", serverSessionId: "server-session-123")]]);
        var (service, _, getState) = CreateReducingService(state, client);

        using (service)
        {
            await service.ProcessAgentResponseAsync(sessionId);
        }

        Assert.True(SessionSelectors.TryGetSession(getState(), sessionId, out SessionEntry entry));
        Assert.Equal("thread-first-turn", entry.Metadata.AguiThreadId);
        Assert.Equal("server-session-123", entry.Metadata.ServerSessionId);
    }

    [Fact]
    public async Task ProcessAgentResponseAsync_ForwardsOwnershipAndWorkflowMetadataOnLiveRequests()
    {
        const string sessionId = "session-forwarded-metadata";
        SessionManagerState state = CreateConversationState(
            sessionId,
            [
                new ChatMessage(ChatRole.System, "You are a helpful planner."),
                new ChatMessage(ChatRole.User, "Continue this workflow.")
            ],
            aguiThreadId: "thread-forwarded",
            ownerId: "owner-123",
            tenantId: "tenant-456",
            workflowInstanceId: "workflow-789",
            runtimeInstanceId: "runtime-abc",
            preferredModelId: "model-preferred");

        ScriptedChatClient client = new([[CreateTextUpdate("Forwarded")]]);
        var (service, _, _) = CreateReducingService(state, client);

        using (service)
        {
            await service.ProcessAgentResponseAsync(sessionId);
        }

        ChatCallSnapshot call = Assert.Single(client.Calls);
        Assert.Equal("thread-forwarded", call.AguiThreadId);
        Assert.Equal("model-preferred", call.ModelId);
        Assert.Equal("owner-123", AssertAdditionalProperty(call, "ownerId"));
        Assert.Equal("tenant-456", AssertAdditionalProperty(call, "tenantId"));
        Assert.Equal("workflow-789", AssertAdditionalProperty(call, "workflowInstanceId"));
        Assert.Equal("runtime-abc", AssertAdditionalProperty(call, "runtimeInstanceId"));
        Assert.Equal("model-preferred", AssertAdditionalProperty(call, "preferredModelId"));
    }

    [Fact]
    public async Task ProcessAgentResponseAsync_EditAndRegenerateUsesFullReplacementBranch()
    {
        const string sessionId = "session-edit";
        SessionManagerState state = CreateConversationState(
            sessionId,
            [
                new ChatMessage(ChatRole.System, "You are an email assistant."),
                new ChatMessage(ChatRole.User, "Draft a project update."),
                new ChatMessage(ChatRole.Assistant, "Here is the first version."),
                new ChatMessage(ChatRole.User, "Make it more upbeat."),
                new ChatMessage(ChatRole.Assistant, "Here is a warmer revision.")
            ],
            conversationId: "conv-edit",
            aguiThreadId: "thread-edit");

        state = ApplyAction(state, new SessionActions.EditAndRegenerateAction(sessionId, 3, "Make it more concise instead."));

        ScriptedChatClient client = new([[CreateTextUpdate("Regenerated response")]]);
        var (service, _, getState) = CreateReducingService(state, client);

        using (service)
        {
            await service.ProcessAgentResponseAsync(sessionId);
        }

        Assert.Null(SessionSelectors.GetSessionStateOrDefault(getState(), sessionId).ConversationId);
        ChatCallSnapshot call = Assert.Single(client.Calls);
        Assert.Null(call.ConversationId);
        AssertCallMessages(
            call,
            (ChatRole.System, "You are an email assistant.", false, false),
            (ChatRole.User, "Draft a project update.", false, false),
            (ChatRole.Assistant, "Here is the first version.", false, false),
            (ChatRole.User, "Make it more concise instead.", false, false));
    }

    [Fact]
    public async Task ProcessAgentResponseAsync_CheckpointReentryUsesTrimmedActiveBranch()
    {
        const string sessionId = "session-revert";
        SessionManagerState state = CreateConversationState(
            sessionId,
            [
                new ChatMessage(ChatRole.System, "You are a planning assistant."),
                new ChatMessage(ChatRole.User, "Create a launch checklist."),
                new ChatMessage(ChatRole.Assistant, "Draft checklist ready."),
                new ChatMessage(ChatRole.User, "Add rollback notes."),
                new ChatMessage(ChatRole.Assistant, "Rollback notes added.")
            ],
            aguiThreadId: "thread-revert");

        state = ApplyAction(state, new SessionActions.TrimMessagesAction(sessionId, 3));

        ScriptedChatClient client = new([[CreateTextUpdate("Checkpoint continuation")]]);
        var (service, _, _) = CreateReducingService(state, client);

        using (service)
        {
            await service.ProcessAgentResponseAsync(sessionId);
        }

        ChatCallSnapshot call = Assert.Single(client.Calls);
        AssertCallMessages(
            call,
            (ChatRole.System, "You are a planning assistant.", false, false),
            (ChatRole.User, "Create a launch checklist.", false, false),
            (ChatRole.Assistant, "Draft checklist ready.", false, false));
    }

    [Fact]
    public async Task ProcessAgentResponseAsync_ApprovedReentryResendsFullHistoryWithApprovalDecision()
    {
        const string sessionId = "session-approved-reentry";
        SessionManagerState state = CreateConversationState(
            sessionId,
            [
                new ChatMessage(ChatRole.System, "You are a release coordinator."),
                new ChatMessage(ChatRole.User, "Send the launch email.")
            ],
            aguiThreadId: "thread-approved");

        ScriptedChatClient client = new(
            [
                [CreateApprovalRequestUpdate("call-approved", "approval-approved", "send_email", "conv-approved")],
                [CreateTextUpdate("Approval complete", "conv-approved")]
            ]);

        var (service, dispatcher, _) = CreateReducingService(state, client, new ApprovalHandler());

        using (service)
        {
            Task responseTask = service.ProcessAgentResponseAsync(sessionId);
            await WaitUntilAsync(() => dispatcher.Actions
                .OfType<SessionActions.SetPendingApprovalAction>()
                .Any(action => action.PendingApproval is not null));

            service.ResolveApproval(sessionId, approved: true);
            await responseTask;
        }

        Assert.Equal(2, client.Calls.Length);
        ChatCallSnapshot reentryCall = client.Calls[1];
        Assert.Equal("conv-approved", reentryCall.ConversationId);
        Assert.Equal("thread-approved", reentryCall.AguiThreadId);
        AssertCallMessages(
            reentryCall,
            (ChatRole.System, "You are a release coordinator.", false, false),
            (ChatRole.User, "Send the launch email.", false, false),
            (ChatRole.Assistant, string.Empty, true, true),
            (ChatRole.Tool, string.Empty, false, true));
    }

    [Fact]
    public async Task ProcessAgentResponseAsync_RejectedApprovalContinuationStillResendsFullHistory()
    {
        const string sessionId = "session-rejected-continuation";
        SessionManagerState state = CreateConversationState(
            sessionId,
            [
                new ChatMessage(ChatRole.System, "You are a release coordinator."),
                new ChatMessage(ChatRole.User, "Send the launch email.")
            ],
            aguiThreadId: "thread-rejected");

        ScriptedChatClient client = new(
            [
                [CreateApprovalRequestUpdate("call-rejected", "approval-rejected", "send_email", "conv-rejected")],
                [CreateTextUpdate("Rejected path continued")]
            ]);

        var (service, dispatcher, getState) = CreateReducingService(state, client, new ApprovalHandler());

        using (service)
        {
            Task firstTurn = service.ProcessAgentResponseAsync(sessionId);
            await WaitUntilAsync(() => dispatcher.Actions
                .OfType<SessionActions.SetPendingApprovalAction>()
                .Any(action => action.PendingApproval is not null));

            service.ResolveApproval(sessionId, approved: false);
            await firstTurn;

            dispatcher.Dispatch(new SessionActions.AddMessageAction(
                sessionId,
                new ChatMessage(ChatRole.User, "Do not send it. Summarize the risks instead.")));

            await service.ProcessAgentResponseAsync(sessionId);
        }

        Assert.Null(SessionSelectors.GetSessionStateOrDefault(getState(), sessionId).ConversationId);
        Assert.Equal(2, client.Calls.Length);
        ChatCallSnapshot continuationCall = client.Calls[1];
        Assert.Null(continuationCall.ConversationId);
        Assert.Equal("thread-rejected", continuationCall.AguiThreadId);
        AssertCallMessages(
            continuationCall,
            (ChatRole.System, "You are a release coordinator.", false, false),
            (ChatRole.User, "Send the launch email.", false, false),
            (ChatRole.Assistant, string.Empty, true, true),
            (ChatRole.Tool, string.Empty, false, true),
            (ChatRole.User, "Do not send it. Summarize the risks instead.", false, false));
    }

    private static async Task ReleaseAndDrainAsync(
        AgentStreamingService service,
        IEnumerable<string> sessionIds,
        IEnumerable<BlockingChatClient> clients,
        params Task[] knownTasks)
    {
        foreach (BlockingChatClient client in clients)
        {
            client.ReleaseStream();
        }

        foreach (Task task in knownTasks)
        {
            await task;
        }

        foreach (string sessionId in sessionIds)
        {
            service.CancelResponse(sessionId);
        }
    }

    private static async Task WaitForStartsAsync(IEnumerable<BlockingChatClient> clients)
    {
        foreach (BlockingChatClient client in clients)
        {
            await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(5));
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition(), "Timed out waiting for the expected condition.");
    }

    private static AgentStreamingService CreateService(SessionManagerState state, params BlockingChatClient[] clients)
    {
        Mock<IDispatcher> dispatcher = new();
        Mock<IState<SessionManagerState>> sessionStore = new();
        sessionStore.SetupGet(store => store.Value).Returns(state);

        Mock<IApprovalHandler> approvalHandler = new();
        approvalHandler.SetupGet(handler => handler.HasPendingApprovals).Returns(false);
        approvalHandler.SetupGet(handler => handler.PendingApprovals).Returns(Array.Empty<PendingApproval>());
        PendingApproval? approval = null;
        approvalHandler
            .Setup(handler => handler.TryExtractApprovalRequest(It.IsAny<FunctionCallContent>(), out approval))
            .Returns(false);

        Mock<IStateManager> stateManager = new();
        stateManager.SetupGet(manager => manager.CurrentRecipe).Returns((Recipe)null!);
        stateManager.SetupGet(manager => manager.HasActiveState).Returns(false);
        Recipe? recipe = null;
        stateManager
            .Setup(manager => manager.TryExtractRecipeSnapshot(It.IsAny<DataContent>(), out recipe))
            .Returns(false);

        Mock<IObservabilityService> observability = new();
        observability.Setup(service => service.GetSteps()).Returns(Array.Empty<ReasoningStep>());

        Mock<ICheckpointService> checkpointService = new();
        checkpointService.Setup(service => service.GetLatestCheckpoint(It.IsAny<string>())).Returns((Checkpoint?)null);
        checkpointService.Setup(service => service.RevertToCheckpoint(It.IsAny<string>(), It.IsAny<string>())).Returns((Checkpoint?)null);
        checkpointService.Setup(service => service.GetAllCheckpoints(It.IsAny<string>())).Returns(Array.Empty<Checkpoint>());

        Mock<IRiskAssessmentService> riskAssessment = new();
        riskAssessment.Setup(service => service.AssessRisk(It.IsAny<string>())).Returns(RiskLevel.Low);
        riskAssessment.Setup(service => service.GetRiskDescription(It.IsAny<RiskLevel>())).Returns("Low");

        return new AgentStreamingService(
            dispatcher.Object,
            sessionStore.Object,
            approvalHandler.Object,
            Mock.Of<IJsonPatchApplier>(),
            stateManager.Object,
            observability.Object,
            checkpointService.Object,
            new QueueChatClientFactory(clients),
            new ToolComponentRegistry(),
            riskAssessment.Object,
            new AutonomyPolicyService());
    }

    private static (AgentStreamingService Service, ApplyingDispatcher Dispatcher, Func<SessionManagerState> GetState) CreateReducingService(
        SessionManagerState initialState,
        ScriptedChatClient client,
        IApprovalHandler? approvalHandler = null)
    {
        SessionManagerState currentState = initialState;
        Mock<IState<SessionManagerState>> sessionStore = new();
        sessionStore.SetupGet(store => store.Value).Returns(() => currentState);

        ApplyingDispatcher dispatcher = new(action => currentState = ApplyAction(currentState, action));
        IApprovalHandler resolvedApprovalHandler = approvalHandler ?? CreatePassiveApprovalHandler();

        Mock<IStateManager> stateManager = new();
        stateManager.SetupGet(manager => manager.CurrentRecipe).Returns((Recipe)null!);
        stateManager.SetupGet(manager => manager.HasActiveState).Returns(false);
        Recipe? recipe = null;
        stateManager
            .Setup(manager => manager.TryExtractRecipeSnapshot(It.IsAny<DataContent>(), out recipe))
            .Returns(false);

        Mock<IObservabilityService> observability = new();
        observability.Setup(service => service.GetSteps()).Returns(Array.Empty<ReasoningStep>());

        Mock<ICheckpointService> checkpointService = new();
        checkpointService.Setup(service => service.GetLatestCheckpoint(It.IsAny<string>())).Returns((Checkpoint?)null);
        checkpointService.Setup(service => service.RevertToCheckpoint(It.IsAny<string>(), It.IsAny<string>())).Returns((Checkpoint?)null);
        checkpointService.Setup(service => service.GetAllCheckpoints(It.IsAny<string>())).Returns(Array.Empty<Checkpoint>());

        Mock<IRiskAssessmentService> riskAssessment = new();
        riskAssessment.Setup(service => service.AssessRisk(It.IsAny<string>())).Returns(RiskLevel.High);
        riskAssessment.Setup(service => service.GetRiskDescription(It.IsAny<RiskLevel>())).Returns("High");

        AgentStreamingService service = new(
            dispatcher,
            sessionStore.Object,
            resolvedApprovalHandler,
            Mock.Of<IJsonPatchApplier>(),
            stateManager.Object,
            observability.Object,
            checkpointService.Object,
            new SingleChatClientFactory(client),
            new ToolComponentRegistry(),
            riskAssessment.Object,
            new AutonomyPolicyService());

        return (service, dispatcher, () => currentState);
    }

    private static IApprovalHandler CreatePassiveApprovalHandler()
    {
        Mock<IApprovalHandler> approvalHandler = new();
        approvalHandler.SetupGet(handler => handler.HasPendingApprovals).Returns(false);
        approvalHandler.SetupGet(handler => handler.PendingApprovals).Returns(Array.Empty<PendingApproval>());
        PendingApproval? approval = null;
        approvalHandler
            .Setup(handler => handler.TryExtractApprovalRequest(It.IsAny<FunctionCallContent>(), out approval))
            .Returns(false);
        return approvalHandler.Object;
    }

    private static SessionManagerState CreateState(IEnumerable<string> sessionIds)
    {
        string[] sessionIdArray = sessionIds as string[] ?? sessionIds.ToArray();
        ImmutableDictionary<string, SessionEntry> sessions = Enumerable
            .Select(sessionIdArray, sessionId => SessionManagerState.CreateSessionEntry(sessionId))
            .ToImmutableDictionary(entry => entry.Metadata.Id, StringComparer.Ordinal);

        return new SessionManagerState
        {
            ActiveSessionId = sessionIdArray[0],
            Sessions = sessions,
            AutonomyLevel = AutonomyLevel.Suggest,
        };
    }

    private static SessionManagerState CreateConversationState(
        string sessionId,
        IReadOnlyList<ChatMessage> messages,
        string? conversationId = null,
        string? aguiThreadId = null,
        string? ownerId = null,
        string? tenantId = null,
        string? workflowInstanceId = null,
        string? runtimeInstanceId = null,
        string? preferredModelId = null)
    {
        ConversationTree tree = new();
        foreach (ChatMessage message in messages)
        {
            tree = tree.AddMessage(message);
        }

        SessionEntry entry = SessionManagerState.CreateSessionEntry(
            sessionId,
            aguiThreadId: aguiThreadId,
            preferredModelId: preferredModelId);
        entry = entry with
        {
            Metadata = entry.Metadata with
            {
                OwnerId = ownerId,
                TenantId = tenantId,
                WorkflowInstanceId = workflowInstanceId,
                RuntimeInstanceId = runtimeInstanceId,
            },
            State = entry.State with { Tree = tree, ConversationId = conversationId },
        };

        return new SessionManagerState
        {
            ActiveSessionId = sessionId,
            Sessions = ImmutableDictionary<string, SessionEntry>.Empty.Add(sessionId, entry),
            AutonomyLevel = AutonomyLevel.Suggest,
        };
    }

    private static SessionManagerState ApplyAction(SessionManagerState state, object action) => action switch
    {
        SessionActions.AddMessageAction typed => SessionReducers.OnAddMessage(state, typed),
        SessionActions.UpdateResponseMessageAction typed => SessionReducers.OnUpdateResponseMessage(state, typed),
        SessionActions.SetConversationIdAction typed => SessionReducers.OnSetConversationId(state, typed),
        SessionActions.SetSessionCorrelationAction typed => SessionReducers.OnSetSessionCorrelation(state, typed),
        SessionActions.SetPendingApprovalAction typed => SessionReducers.OnSetPendingApproval(state, typed),
        SessionActions.ClearUndoGracePeriodAction typed => SessionReducers.OnClearUndoGracePeriod(state, typed),
        SessionActions.TrimMessagesAction typed => SessionReducers.OnTrimMessages(state, typed),
        SessionActions.SetRunningAction typed => SessionReducers.OnSetRunning(state, typed),
        SessionActions.SetAuthorNameAction typed => SessionReducers.OnSetAuthorName(state, typed),
        SessionActions.SetSessionStatusAction typed => SessionReducers.OnSetSessionStatus(state, typed),
        SessionActions.EditAndRegenerateAction typed => SessionReducers.OnEditAndRegenerate(state, typed),
        SessionActions.AddAuditEntryAction typed => SessionReducers.OnAddAuditEntry(state, typed),
        _ => state,
    };

    private static ChatResponseUpdate CreateTextUpdate(
        string text,
        string? conversationId = null,
        string? aguiThreadId = null,
        string? serverSessionId = null)
    {
        ChatResponseUpdate update = new(role: ChatRole.Assistant, content: text)
        {
            ConversationId = conversationId,
        };

        if (!string.IsNullOrWhiteSpace(aguiThreadId) || !string.IsNullOrWhiteSpace(serverSessionId))
        {
            update.AdditionalProperties = [];
        }

        if (!string.IsNullOrWhiteSpace(aguiThreadId))
        {
            update.AdditionalProperties!["agui_thread_id"] = aguiThreadId;
        }

        if (!string.IsNullOrWhiteSpace(serverSessionId))
        {
            update.AdditionalProperties!["server_session_id"] = serverSessionId;
        }

        return update;
    }

    private static ChatResponseUpdate CreateApprovalRequestUpdate(
        string callId,
        string approvalId,
        string functionName,
        string? conversationId = null)
    {
        Dictionary<string, object?> arguments = new()
        {
            ["request"] = JsonSerializer.SerializeToElement(new
            {
                approval_id = approvalId,
                function_name = functionName,
                function_arguments = new { to = "team@example.com" },
                message = $"Approve execution of '{functionName}'?",
            }),
        };

        return new ChatResponseUpdate(
            ChatRole.Assistant,
            [new FunctionCallContent(callId: callId, name: "request_approval", arguments: arguments)])
        {
            ConversationId = conversationId,
        };
    }

    private static void AssertCallMessages(
        ChatCallSnapshot call,
        params (ChatRole Role, string Text, bool HasFunctionCall, bool HasFunctionResult)[] expected)
    {
        Assert.Equal(expected.Length, call.Messages.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            MessageSnapshot actual = call.Messages[i];
            Assert.Equal(expected[i].Role, actual.Role);
            Assert.Equal(expected[i].Text, actual.Text);
            Assert.Equal(expected[i].HasFunctionCall, actual.HasFunctionCall);
            Assert.Equal(expected[i].HasFunctionResult, actual.HasFunctionResult);
        }
    }

    private static string? AssertAdditionalProperty(ChatCallSnapshot call, string propertyName)
    {
        Assert.True(
            call.AdditionalProperties.TryGetValue(propertyName, out string? propertyValue),
            $"Expected additional property '{propertyName}' to be forwarded.");
        return propertyValue;
    }

    private static string[] CreateSessionIds(int count) =>
        Enumerable.Range(1, count)
            .Select(index => $"session-{index}")
            .ToArray();

    private static BlockingChatClient[] CreateBlockingClients(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new BlockingChatClient($"client-{index + 1}"))
            .ToArray();

    private sealed class QueueChatClientFactory : IAGUIChatClientFactory
    {
        private readonly Queue<IChatClient> _clients;

        public QueueChatClientFactory(IEnumerable<IChatClient> clients)
        {
            this._clients = new Queue<IChatClient>(clients);
        }

        public IChatClient CreateClient()
        {
            if (this._clients.Count == 0)
            {
                throw new InvalidOperationException("No more test chat clients are available.");
            }

            return this._clients.Dequeue();
        }
    }

    private sealed class SingleChatClientFactory : IAGUIChatClientFactory
    {
        private readonly IChatClient _client;

        public SingleChatClientFactory(IChatClient client)
        {
            _client = client;
        }

        public IChatClient CreateClient() => _client;
    }

    private sealed class ApplyingDispatcher : IDispatcher
    {
        private readonly Action<object> _apply;
        private readonly ConcurrentQueue<object> _actions = new();

        public ApplyingDispatcher(Action<object> apply)
        {
            _apply = apply;
        }

        public object[] Actions => _actions.ToArray();

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action)
        {
            _actions.Enqueue(action);
            _apply(action);
        }
    }

    private sealed record ChatCallSnapshot(
        IReadOnlyList<MessageSnapshot> Messages,
        string? ConversationId,
        string? AguiThreadId,
        string? ModelId,
        IReadOnlyDictionary<string, string?> AdditionalProperties)
    {
        public static ChatCallSnapshot From(IEnumerable<ChatMessage> messages, ChatOptions? options)
        {
            string? aguiThreadId = null;
            Dictionary<string, string?> additionalProperties = new(StringComparer.Ordinal);
            if (options?.AdditionalProperties?.TryGetValue("agui_thread_id", out object? rawThreadId) is true)
            {
                aguiThreadId = rawThreadId?.ToString();
                additionalProperties["agui_thread_id"] = aguiThreadId;
            }

            if (options?.AdditionalProperties is not null)
            {
                CaptureAdditionalProperty(options.AdditionalProperties, additionalProperties, "preferredModelId");
                CaptureAdditionalProperty(options.AdditionalProperties, additionalProperties, "ownerId");
                CaptureAdditionalProperty(options.AdditionalProperties, additionalProperties, "tenantId");
                CaptureAdditionalProperty(options.AdditionalProperties, additionalProperties, "workflowInstanceId");
                CaptureAdditionalProperty(options.AdditionalProperties, additionalProperties, "runtimeInstanceId");
            }

            return new ChatCallSnapshot(
                messages.Select(MessageSnapshot.From).ToArray(),
                options?.ConversationId,
                aguiThreadId,
                options?.ModelId,
                additionalProperties);
        }

        private static void CaptureAdditionalProperty(
            AdditionalPropertiesDictionary additionalProperties,
            Dictionary<string, string?> capturedProperties,
            string propertyName)
        {
            if (additionalProperties.TryGetValue(propertyName, out object? propertyValue))
            {
                capturedProperties[propertyName] = propertyValue?.ToString();
            }
        }
    }

    private sealed record MessageSnapshot(
        ChatRole Role,
        string Text,
        bool HasFunctionCall,
        bool HasFunctionResult)
    {
        public static MessageSnapshot From(ChatMessage message)
        {
            return new MessageSnapshot(
                message.Role,
                message.Text ?? string.Empty,
                message.Contents.OfType<FunctionCallContent>().Any(),
                message.Contents.OfType<FunctionResultContent>().Any());
        }
    }

    private sealed class ScriptedChatClient : IChatClient
    {
        private readonly Queue<IReadOnlyList<ChatResponseUpdate>> _responsesPerCall;
        private readonly object _gate = new();
        private readonly List<ChatCallSnapshot> _calls = [];

        public ScriptedChatClient(IEnumerable<IReadOnlyList<ChatResponseUpdate>> responsesPerCall)
        {
            _responsesPerCall = new Queue<IReadOnlyList<ChatResponseUpdate>>(responsesPerCall);
            this.Metadata = new ChatClientMetadata("scripted-client", new Uri("http://localhost"), null);
        }

        public ChatClientMetadata Metadata { get; }

        public ChatCallSnapshot[] Calls
        {
            get
            {
                lock (_gate)
                {
                    return _calls.ToArray();
                }
            }
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ChatResponseUpdate> scriptedUpdates;
            lock (_gate)
            {
                _calls.Add(ChatCallSnapshot.From(messages, options));
                scriptedUpdates = _responsesPerCall.Count > 0
                    ? _responsesPerCall.Dequeue()
                    : Array.Empty<ChatResponseUpdate>();
            }

            foreach (ChatResponseUpdate update in scriptedUpdates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return update;
                await Task.Yield();
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(ChatClientMetadata) ? this.Metadata : null;

        public void Dispose()
        {
        }
    }

    private sealed class BlockingChatClient : IChatClient
    {
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingChatClient(string name)
        {
            this.Metadata = new ChatClientMetadata(name, new Uri("http://localhost"), null);
        }

        public ChatClientMetadata Metadata { get; }

        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            this.Started.TrySetResult(true);
            await this._release.Task.WaitAsync(cancellationToken);
            yield break;
        }

        public void ReleaseStream() => this._release.TrySetResult(true);

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(ChatClientMetadata) ? this.Metadata : null;

        public void Dispose()
        {
        }
    }
}
