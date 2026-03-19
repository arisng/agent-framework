// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Immutable;
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
            riskAssessment.Object);
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
