using System.Collections.Immutable;
using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using AGUIDojoClient.Store.SessionManager;
using Fluxor;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AGUIDojoClient.Tests.Store.SessionManager;

public sealed class SessionPersistenceEffectTests
{
    [Fact]
    public async Task OnAddMessage_SkipsServerActiveLeafSyncWhileSessionIsRunning()
    {
        const string sessionId = "session-1";
        const string serverSessionId = "server-session-1";
        const string activeLeafId = "1234567890abcdef1234567890abcdef";

        var tree = CreateServerBackedTree(activeLeafId);
        SessionEntry entry = SessionManagerState.CreateSessionEntry(
            sessionId,
            aguiThreadId: "thread_session-1",
            serverSessionId: serverSessionId) with
        {
            State = new SessionState
            {
                Tree = tree,
                IsRunning = true,
            },
        };

        SessionPersistenceEffect effect = CreateEffect(
            CreateStore(entry),
            out Mock<ISessionApiService> sessionApiService,
            out FakeSessionPersistenceService persistence);

        await effect.OnAddMessage(new SessionActions.AddMessageAction(sessionId, new ChatMessage(ChatRole.User, "hello")), Mock.Of<IDispatcher>());
        await Task.Delay(TimeSpan.FromMilliseconds(700));

        Assert.Equal(1, persistence.SaveConversationCallCount);
        sessionApiService.Verify(
            service => service.SetActiveLeafAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnAddMessage_SyncsServerActiveLeafWhenSessionIsIdle()
    {
        const string sessionId = "session-2";
        const string serverSessionId = "server-session-2";
        const string activeLeafId = "fedcba0987654321fedcba0987654321";

        var tree = CreateServerBackedTree(activeLeafId);
        SessionEntry entry = SessionManagerState.CreateSessionEntry(
            sessionId,
            aguiThreadId: "thread_session-2",
            serverSessionId: serverSessionId) with
        {
            State = new SessionState
            {
                Tree = tree,
                IsRunning = false,
            },
        };

        SessionPersistenceEffect effect = CreateEffect(
            CreateStore(entry),
            out Mock<ISessionApiService> sessionApiService,
            out FakeSessionPersistenceService persistence);

        await effect.OnAddMessage(new SessionActions.AddMessageAction(sessionId, new ChatMessage(ChatRole.User, "hello")), Mock.Of<IDispatcher>());
        await Task.Delay(TimeSpan.FromMilliseconds(700));

        Assert.Equal(1, persistence.SaveConversationCallCount);
        sessionApiService.Verify(
            service => service.SetActiveLeafAsync(serverSessionId, activeLeafId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnSetPlan_CapturesWorkspaceImportBeforeCorrelationDispatch()
    {
        const string sessionId = "session-3";
        const string aguiThreadId = "thread_session-3";
        const string serverSessionId = "server-session-3";

        Plan originalPlan = new()
        {
            Steps =
            [
                new Step
                {
                    Description = "Keep this plan in the import request",
                },
            ],
        };

        SessionEntry originalEntry = SessionManagerState.CreateSessionEntry(
            sessionId,
            aguiThreadId: aguiThreadId) with
        {
            State = new SessionState
            {
                Plan = originalPlan,
            },
        };

        SessionManagerState currentState = new()
        {
            ActiveSessionId = sessionId,
            Sessions = ImmutableDictionary<string, SessionEntry>.Empty.Add(sessionId, originalEntry),
        };

        Mock<IState<SessionManagerState>> sessionStore = new();
        sessionStore.SetupGet(store => store.Value).Returns(() => currentState);

        SessionPersistenceEffect effect = CreateEffect(
            sessionStore,
            out Mock<ISessionApiService> sessionApiService,
            out _);

        sessionApiService
            .Setup(service => service.ListSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ServerSessionSummary
                {
                    Id = serverSessionId,
                    Status = "active",
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastActivityAt = DateTimeOffset.UtcNow,
                    AguiThreadId = aguiThreadId,
                },
            ]);

        ServerSessionWorkspaceImportRequest? importedRequest = null;
        sessionApiService
            .Setup(service => service.ImportWorkspaceAsync(serverSessionId, It.IsAny<ServerSessionWorkspaceImportRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, ServerSessionWorkspaceImportRequest, CancellationToken>((_, request, _) => importedRequest = request)
            .ReturnsAsync(true);

        Mock<IDispatcher> dispatcher = new();
        dispatcher
            .Setup(x => x.Dispatch(It.IsAny<object>()))
            .Callback<object>(
                action =>
                {
                    if (action is SessionActions.SetSessionCorrelationAction)
                    {
                        currentState = new SessionManagerState
                        {
                            ActiveSessionId = sessionId,
                            Sessions = ImmutableDictionary<string, SessionEntry>.Empty.Add(
                                sessionId,
                                originalEntry with
                                {
                                    Metadata = originalEntry.Metadata with { ServerSessionId = serverSessionId },
                                    State = new SessionState(),
                                }),
                        };
                    }
                });

        await effect.OnSetPlan(new SessionActions.SetPlanAction(sessionId, originalPlan), dispatcher.Object);
        await Task.Delay(TimeSpan.FromMilliseconds(700));

        Assert.NotNull(importedRequest);
        Assert.NotNull(importedRequest!.Snapshot);
        Assert.NotNull(importedRequest.Snapshot!.CurrentPlan);
        Assert.Single(importedRequest.Snapshot.CurrentPlan.Steps);
        Assert.Equal("Keep this plan in the import request", importedRequest.Snapshot.CurrentPlan.Steps[0].Description);
        sessionApiService.Verify(
            service => service.ImportWorkspaceAsync(serverSessionId, It.IsAny<ServerSessionWorkspaceImportRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static SessionPersistenceEffect CreateEffect(
        Mock<IState<SessionManagerState>> sessionStore,
        out Mock<ISessionApiService> sessionApiService,
        out FakeSessionPersistenceService persistence)
    {
        persistence = new FakeSessionPersistenceService();
        sessionApiService = new Mock<ISessionApiService>();
        sessionApiService
            .Setup(service => service.SetActiveLeafAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        sessionApiService
            .Setup(service => service.ClearConversationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        sessionApiService
            .Setup(service => service.ListSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        sessionApiService
            .Setup(service => service.ImportWorkspaceAsync(It.IsAny<string>(), It.IsAny<ServerSessionWorkspaceImportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return new SessionPersistenceEffect(
            sessionStore.Object,
            persistence,
            sessionApiService.Object,
            NullLogger<SessionPersistenceEffect>.Instance);
    }

    private static Mock<IState<SessionManagerState>> CreateStore(SessionEntry entry)
    {
        SessionManagerState state = new()
        {
            ActiveSessionId = entry.Metadata.Id,
            Sessions = ImmutableDictionary<string, SessionEntry>.Empty.Add(entry.Metadata.Id, entry),
        };

        Mock<IState<SessionManagerState>> sessionStore = new();
        sessionStore.SetupGet(store => store.Value).Returns(state);
        return sessionStore;
    }

    private static ConversationTree CreateServerBackedTree(string activeLeafId)
    {
        ChatMessage message = new(ChatRole.User, "hello");
        ConversationNode node = new()
        {
            Id = activeLeafId,
            ParentId = null,
            Message = message,
            ChildIds = ImmutableList<string>.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return new ConversationTree
        {
            Nodes = ImmutableDictionary<string, ConversationNode>.Empty.Add(activeLeafId, node),
            RootId = activeLeafId,
            ActiveLeafId = activeLeafId,
        };
    }

    private sealed class FakeSessionPersistenceService : ISessionPersistenceService
    {
        public int SaveConversationCallCount { get; private set; }

        public Task SaveMetadataAsync(IEnumerable<SessionMetadataDto> metadata) => Task.CompletedTask;

        public Task<List<SessionMetadataDto>?> LoadMetadataAsync() => Task.FromResult<List<SessionMetadataDto>?>(null);

        public Task SaveActiveSessionIdAsync(string sessionId) => Task.CompletedTask;

        public Task<string?> LoadActiveSessionIdAsync() => Task.FromResult<string?>(null);

        public Task SaveConversationAsync(string sessionId, ConversationTree tree)
        {
            SaveConversationCallCount++;
            return Task.CompletedTask;
        }

        public Task<ConversationTree?> LoadConversationAsync(string sessionId) => Task.FromResult<ConversationTree?>(null);

        public Task DeleteConversationAsync(string sessionId) => Task.CompletedTask;
    }
}
