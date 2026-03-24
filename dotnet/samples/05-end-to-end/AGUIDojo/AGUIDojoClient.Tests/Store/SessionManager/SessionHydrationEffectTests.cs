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
            Metadata = [new SessionMetadataDto(localSessionId, SessionMetadata.DefaultTitle, "chat", "Completed", 10, 20, aguiThreadId, null)],
            ActiveSessionId = localSessionId,
            Conversations = { [localSessionId] = localTree },
        };

        Mock<ISessionApiService> sessionApiService = new();
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
            Metadata = [new SessionMetadataDto(sessionId, "Legacy", "chat", "Completed", 10, 20)],
            ActiveSessionId = sessionId,
        };

        Mock<ISessionApiService> sessionApiService = new();
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

        Mock<ISessionApiService> sessionApiService = new();
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
    public async Task HandleHydrateFromStorage_ServerOnlyRecoverySelectsMostRecentServerSession()
    {
        FakeSessionPersistenceService persistence = new()
        {
            Metadata = null,
            ActiveSessionId = null,
        };

        Mock<ISessionApiService> sessionApiService = new();
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
