using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using AGUIDojoClient.Store.SessionManager;
using Fluxor;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AGUIDojoClient.Tests.Store.SessionManager;

public sealed class SessionHydrationEffectTests
{
    [Fact]
    public async Task HandleHydrateFromStorage_MergesCorrelatedServerSessionIntoLocalSession()
    {
        const string localSessionId = "local-session";
        const string serverSessionId = "server-session";
        const string aguiThreadId = "thread_local-session";

        ConversationTree localTree = new();
        localTree = localTree.AddMessage(new ChatMessage(ChatRole.User, "hello"));

        FakeSessionPersistenceService persistence = new()
        {
            Metadata = [new SessionMetadataDto(localSessionId, SessionMetadata.DefaultTitle, "chat", "gpt-4.1", "Completed", 10, 20, aguiThreadId, null)],
            ActiveSessionId = localSessionId,
            Conversations = { [localSessionId] = localTree },
        };

        Mock<ISessionApiService> sessionApiService = CreateSessionApiServiceMock();
        sessionApiService
            .Setup(service => service.ListSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ServerSessionSummary
                {
                    Id = serverSessionId,
                    Title = "Persisted title",
                    Status = "Active",
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(5),
                    LastActivityAt = DateTimeOffset.FromUnixTimeMilliseconds(30),
                    AguiThreadId = aguiThreadId,
                }
            ]);

        SessionHydrationEffect effect = new(
            persistence,
            sessionApiService.Object,
            NullLogger<SessionHydrationEffect>.Instance);
        CapturingDispatcher dispatcher = new();

        await effect.HandleHydrateFromStorage(new SessionActions.HydrateFromStorageAction(), dispatcher);

        SessionActions.HydrateSessionsAction hydrateAction = Assert.IsType<SessionActions.HydrateSessionsAction>(Assert.Single(dispatcher.Actions));
        Assert.Equal(localSessionId, hydrateAction.ActiveSessionId);
        Assert.Single(hydrateAction.Sessions);
        Assert.True(hydrateAction.Sessions.ContainsKey(localSessionId));
        SessionEntry entry = hydrateAction.Sessions[localSessionId];
        Assert.Equal(serverSessionId, entry.Metadata.ServerSessionId);
        Assert.Equal(aguiThreadId, entry.Metadata.AguiThreadId);
        Assert.Equal("gpt-4.1", entry.Metadata.PreferredModelId);
        Assert.Equal("Persisted title", entry.Metadata.Title);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(5), entry.Metadata.CreatedAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(30), entry.Metadata.LastActivityAt);
        Assert.Equal(localTree.Nodes.Count, entry.State.Tree.Nodes.Count);
        Assert.DoesNotContain(serverSessionId, hydrateAction.Sessions.Keys);
    }

    [Fact]
    public async Task HandleHydrateFromStorage_BackfillsLegacyLocalThreadIds()
    {
        const string sessionId = "legacy-session";

        FakeSessionPersistenceService persistence = new()
        {
            Metadata = [new SessionMetadataDto(sessionId, "Legacy", "chat", null, "Completed", 10, 20)],
            ActiveSessionId = sessionId,
        };

        Mock<ISessionApiService> sessionApiService = CreateSessionApiServiceMock();
        sessionApiService
            .Setup(service => service.ListSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ServerSessionSummary>?)null);

        SessionHydrationEffect effect = new(
            persistence,
            sessionApiService.Object,
            NullLogger<SessionHydrationEffect>.Instance);
        CapturingDispatcher dispatcher = new();

        await effect.HandleHydrateFromStorage(new SessionActions.HydrateFromStorageAction(), dispatcher);

        SessionActions.HydrateSessionsAction hydrateAction = Assert.IsType<SessionActions.HydrateSessionsAction>(Assert.Single(dispatcher.Actions));
        SessionEntry entry = hydrateAction.Sessions[sessionId];
        Assert.False(string.IsNullOrWhiteSpace(entry.Metadata.AguiThreadId));
        Assert.Null(entry.Metadata.ServerSessionId);
    }

    [Fact]
    public async Task HandleHydrateFromStorage_UsesServerSessionsWhenBrowserMetadataIsEmpty()
    {
        FakeSessionPersistenceService persistence = new()
        {
            Metadata = null,
            ActiveSessionId = null,
        };

        Mock<ISessionApiService> sessionApiService = CreateSessionApiServiceMock();
        sessionApiService
            .Setup(service => service.ListSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ServerSessionSummary
                {
                    Id = "server-session-1",
                    Title = "Server owned session",
                    Status = "Active",
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(10),
                    LastActivityAt = DateTimeOffset.FromUnixTimeMilliseconds(20),
                    AguiThreadId = "thread-server-1",
                }
            ]);

        SessionHydrationEffect effect = new(
            persistence,
            sessionApiService.Object,
            NullLogger<SessionHydrationEffect>.Instance);
        CapturingDispatcher dispatcher = new();

        await effect.HandleHydrateFromStorage(new SessionActions.HydrateFromStorageAction(), dispatcher);

        SessionActions.HydrateSessionsAction hydrateAction = Assert.IsType<SessionActions.HydrateSessionsAction>(Assert.Single(dispatcher.Actions));
        Assert.Null(hydrateAction.ActiveSessionId);
        SessionEntry entry = Assert.Single(hydrateAction.Sessions.Values);
        Assert.Equal("server-session-1", entry.Metadata.Id);
        Assert.Equal("Server owned session", entry.Metadata.Title);
        Assert.Equal("server-session-1", entry.Metadata.ServerSessionId);
        Assert.Equal("thread-server-1", entry.Metadata.AguiThreadId);
        Assert.Empty(entry.State.Tree.Nodes);
    }

    [Fact]
    public async Task HandleHydrateFromStorage_PrefersServerConversationGraphOverBrowserCache()
    {
        const string localSessionId = "local-session";
        const string serverSessionId = "server-session";
        const string aguiThreadId = "thread_local-session";

        ConversationTree browserTree = new();
        browserTree = browserTree.AddMessage(new ChatMessage(ChatRole.User, "Root question"));
        browserTree = browserTree.AddMessage(new ChatMessage(ChatRole.Assistant, "Browser cached reply"));

        FakeSessionPersistenceService persistence = new()
        {
            Metadata = [new SessionMetadataDto(localSessionId, SessionMetadata.DefaultTitle, "chat", null, "Completed", 10, 20, aguiThreadId, null)],
            ActiveSessionId = localSessionId,
            Conversations = { [localSessionId] = browserTree },
        };

        Mock<ISessionApiService> sessionApiService = CreateSessionApiServiceMock();
        sessionApiService
            .Setup(service => service.ListSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ServerSessionSummary
                {
                    Id = serverSessionId,
                    Title = "Recovered from server",
                    Status = "Active",
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(5),
                    LastActivityAt = DateTimeOffset.FromUnixTimeMilliseconds(30),
                    AguiThreadId = aguiThreadId,
                }
            ]);
        sessionApiService
            .Setup(service => service.GetConversationAsync(serverSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ServerConversationGraph
                {
                    RootId = "server-root",
                    ActiveLeafId = "server-branch",
                    Nodes =
                    [
                        new ServerConversationNode
                        {
                            Id = "server-root",
                            ParentId = null,
                            Role = ChatRole.User.Value,
                            Text = "Root question",
                            AuthorName = "User",
                            ChildIds = ["server-original", "server-branch"],
                        },
                        new ServerConversationNode
                        {
                            Id = "server-original",
                            ParentId = "server-root",
                            Role = ChatRole.Assistant.Value,
                            Text = "Server original reply",
                            AuthorName = "Assistant",
                            ChildIds = [],
                        },
                        new ServerConversationNode
                        {
                            Id = "server-branch",
                            ParentId = "server-root",
                            Role = ChatRole.Assistant.Value,
                            Text = "Server preferred branch",
                            AuthorName = "Assistant",
                            ChildIds = [],
                        }
                    ],
                });

        SessionHydrationEffect effect = new(
            persistence,
            sessionApiService.Object,
            NullLogger<SessionHydrationEffect>.Instance);
        CapturingDispatcher dispatcher = new();

        await effect.HandleHydrateFromStorage(new SessionActions.HydrateFromStorageAction(), dispatcher);

        SessionActions.HydrateSessionsAction hydrateAction = Assert.IsType<SessionActions.HydrateSessionsAction>(Assert.Single(dispatcher.Actions));
        SessionEntry entry = Assert.Single(hydrateAction.Sessions.Values);

        Assert.Equal(localSessionId, hydrateAction.ActiveSessionId);
        Assert.Equal(serverSessionId, entry.Metadata.ServerSessionId);
        Assert.Equal("server-root", entry.State.Tree.RootId);
        Assert.Equal("server-branch", entry.State.Tree.ActiveLeafId);
        Assert.Equal(
            ["Root question", "Server preferred branch"],
            entry.State.Tree.GetActiveBranchMessages().Select(message => message.Text));
        Assert.DoesNotContain("Browser cached reply", entry.State.Tree.GetActiveBranchMessages().Select(message => message.Text));
        sessionApiService.Verify(service => service.GetConversationAsync(serverSessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleHydrateFromStorage_ServerOnlyRecoverySelectsMostRecentServerSession()
    {
        FakeSessionPersistenceService persistence = new()
        {
            Metadata = null,
            ActiveSessionId = null,
        };

        Mock<ISessionApiService> sessionApiService = CreateSessionApiServiceMock();
        sessionApiService
            .Setup(service => service.ListSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ServerSessionSummary
                {
                    Id = "server-session-1",
                    Title = "Older server session",
                    Status = "Active",
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(10),
                    LastActivityAt = DateTimeOffset.FromUnixTimeMilliseconds(20),
                    AguiThreadId = "thread-server-1",
                },
                new ServerSessionSummary
                {
                    Id = "server-session-2",
                    Title = "Newest server session",
                    Status = "Active",
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(30),
                    LastActivityAt = DateTimeOffset.FromUnixTimeMilliseconds(40),
                    AguiThreadId = "thread-server-2",
                }
            ]);

        SessionHydrationEffect effect = new(
            persistence,
            sessionApiService.Object,
            NullLogger<SessionHydrationEffect>.Instance);
        CapturingDispatcher dispatcher = new();

        await effect.HandleHydrateFromStorage(new SessionActions.HydrateFromStorageAction(), dispatcher);

        SessionActions.HydrateSessionsAction hydrateAction =
            Assert.IsType<SessionActions.HydrateSessionsAction>(Assert.Single(dispatcher.Actions));
        SessionManagerState hydratedState =
            SessionReducers.OnHydrateSessions(new SessionManagerState(), hydrateAction);

        Assert.Equal("server-session-2", hydratedState.ActiveSessionId);
        Assert.Equal(2, hydratedState.Sessions.Count);
        Assert.Equal("server-session-2", hydratedState.Sessions["server-session-2"].Metadata.ServerSessionId);
        Assert.Equal("Newest server session", hydratedState.Sessions["server-session-2"].Metadata.Title);
    }

    [Fact]
    public async Task HandleHydrateFromStorage_ServerOnlyRecoveryRestoresServerActiveBranch()
    {
        const string serverSessionId = "server-session-1";

        FakeSessionPersistenceService persistence = new()
        {
            Metadata = null,
            ActiveSessionId = null,
        };

        Mock<ISessionApiService> sessionApiService = CreateSessionApiServiceMock();
        sessionApiService
            .Setup(service => service.ListSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ServerSessionSummary
                {
                    Id = serverSessionId,
                    Title = "Recovered session",
                    Status = "Active",
                    CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(10),
                    LastActivityAt = DateTimeOffset.FromUnixTimeMilliseconds(20),
                    AguiThreadId = "thread-server-1",
                }
            ]);
        sessionApiService
            .Setup(service => service.GetConversationAsync(serverSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ServerConversationGraph
                {
                    RootId = "server-root",
                    ActiveLeafId = "server-branch-child",
                    Nodes =
                    [
                        new ServerConversationNode
                        {
                            Id = "server-root",
                            ParentId = null,
                            Role = ChatRole.User.Value,
                            Text = "Root question",
                            ChildIds = ["server-original", "server-branch"],
                        },
                        new ServerConversationNode
                        {
                            Id = "server-original",
                            ParentId = "server-root",
                            Role = ChatRole.Assistant.Value,
                            Text = "Server original reply",
                            ChildIds = [],
                        },
                        new ServerConversationNode
                        {
                            Id = "server-branch",
                            ParentId = "server-root",
                            Role = ChatRole.Assistant.Value,
                            Text = "Alternative branch",
                            ChildIds = ["server-branch-child"],
                        },
                        new ServerConversationNode
                        {
                            Id = "server-branch-child",
                            ParentId = "server-branch",
                            Role = ChatRole.User.Value,
                            Text = "Continue on preferred branch",
                            ChildIds = [],
                        }
                    ],
                });

        SessionHydrationEffect effect = new(
            persistence,
            sessionApiService.Object,
            NullLogger<SessionHydrationEffect>.Instance);
        CapturingDispatcher dispatcher = new();

        await effect.HandleHydrateFromStorage(new SessionActions.HydrateFromStorageAction(), dispatcher);

        SessionActions.HydrateSessionsAction hydrateAction =
            Assert.IsType<SessionActions.HydrateSessionsAction>(Assert.Single(dispatcher.Actions));
        SessionManagerState hydratedState =
            SessionReducers.OnHydrateSessions(new SessionManagerState(), hydrateAction);

        Assert.Equal(serverSessionId, hydratedState.ActiveSessionId);
        SessionEntry entry = hydratedState.Sessions[serverSessionId];
        Assert.Equal("server-branch-child", entry.State.Tree.ActiveLeafId);
        Assert.Equal(
            ["Root question", "Alternative branch", "Continue on preferred branch"],
            entry.State.Messages.Select(message => message.Text));
    }

    private static Mock<ISessionApiService> CreateSessionApiServiceMock()
    {
        Mock<ISessionApiService> sessionApiService = new();
        sessionApiService
            .Setup(service => service.GetConversationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServerConversationGraph?)null);

        return sessionApiService;
    }

    private sealed class FakeSessionPersistenceService : ISessionPersistenceService
    {
        public List<SessionMetadataDto>? Metadata { get; init; }

        public string? ActiveSessionId { get; init; }

        public Dictionary<string, ConversationTree> Conversations { get; } = new(StringComparer.Ordinal);

        public Task SaveMetadataAsync(IEnumerable<SessionMetadataDto> metadata) => Task.CompletedTask;

        public Task<List<SessionMetadataDto>?> LoadMetadataAsync() => Task.FromResult(Metadata);

        public Task SaveActiveSessionIdAsync(string sessionId) => Task.CompletedTask;

        public Task<string?> LoadActiveSessionIdAsync() => Task.FromResult(ActiveSessionId);

        public Task SaveConversationAsync(string sessionId, ConversationTree tree) => Task.CompletedTask;

        public Task<ConversationTree?> LoadConversationAsync(string sessionId)
            => Task.FromResult(Conversations.TryGetValue(sessionId, out ConversationTree? tree) ? tree : null);

        public Task DeleteConversationAsync(string sessionId) => Task.CompletedTask;
    }

    private sealed class CapturingDispatcher : IDispatcher
    {
        public List<object> Actions { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Actions.Add(action);
    }
}
