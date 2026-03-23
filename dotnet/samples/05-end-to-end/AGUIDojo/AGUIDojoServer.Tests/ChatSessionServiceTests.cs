using AGUIDojoServer.ChatSessions;
using AGUIDojoServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AGUIDojoServer.Tests;

public sealed class ChatSessionServiceTests
{
    [Fact]
    public async Task EnsureSessionForThreadAsync_CreatesSessionWithGeneratedTitle()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            using IServiceScope scope = provider.CreateScope();
            ChatSessionService service = scope.ServiceProvider.GetRequiredService<ChatSessionService>();

            string sessionId = await service.EnsureSessionForThreadAsync(
                "thread-title",
                "Plan a Seattle weekend with ferry time and museum stops.");

            ChatSessionDetail? detail = await service.GetSessionAsync(sessionId);

            Assert.NotNull(detail);
            Assert.Equal("Plan a Seattle weekend with ferry time and museum stops.", detail.Title);
            Assert.Equal("Active", detail.Status);
            Assert.Equal("thread-title", detail.AguiThreadId);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task EnsureSessionForThreadAsync_BackfillsMissingTitleOnExistingSession()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            using IServiceScope scope = provider.CreateScope();
            ChatSessionService service = scope.ServiceProvider.GetRequiredService<ChatSessionService>();

            string sessionId = await service.EnsureSessionForThreadAsync("thread-backfill");
            string sameSessionId = await service.EnsureSessionForThreadAsync(
                "thread-backfill",
                "Summarize the deployment regression and next steps.");

            ChatSessionDetail? detail = await service.GetSessionAsync(sessionId);

            Assert.Equal(sessionId, sameSessionId);
            Assert.NotNull(detail);
            Assert.Equal("Summarize the deployment regression and next steps.", detail.Title);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ArchiveSessionAsync_HidesArchivedSessionFromListButKeepsDetail()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            using IServiceScope scope = provider.CreateScope();
            ChatSessionService service = scope.ServiceProvider.GetRequiredService<ChatSessionService>();

            string archivedSessionId = await service.EnsureSessionForThreadAsync("thread-archive", "Archive this session.");
            string activeSessionId = await service.EnsureSessionForThreadAsync("thread-active", "Keep this session active.");

            bool archived = await service.ArchiveSessionAsync(archivedSessionId);
            List<ChatSessionSummary> sessions = await service.ListSessionsAsync();
            ChatSessionDetail? archivedDetail = await service.GetSessionAsync(archivedSessionId);
            ChatSessionDetail? activeDetail = await service.GetSessionAsync(activeSessionId);

            Assert.True(archived);
            Assert.DoesNotContain(sessions, session => session.Id == archivedSessionId);
            Assert.Contains(sessions, session => session.Id == activeSessionId);
            Assert.NotNull(archivedDetail);
            Assert.Equal("Archived", archivedDetail.Status);
            Assert.NotNull(archivedDetail.ArchivedAt);
            Assert.NotNull(activeDetail);
            Assert.Equal("Active", activeDetail.Status);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task EnsureSessionForThreadAsync_RejectsConflictingImmutableSubjectMetadata()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            using IServiceScope scope = provider.CreateScope();
            ChatSessionService service = scope.ServiceProvider.GetRequiredService<ChatSessionService>();

            await service.EnsureSessionForThreadAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-subject",
                    FirstUserMessage = "Investigate todo ownership.",
                    SubjectModule = "Todo",
                    SubjectEntityType = "WorkItem",
                    SubjectEntityId = "42",
                });

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.EnsureSessionForThreadAsync(
                    new ChatSessionService.ChatSessionEnsureRequest
                    {
                        AguiThreadId = "thread-subject",
                        FirstUserMessage = "Investigate todo ownership.",
                        SubjectModule = "Ticket",
                        SubjectEntityType = "WorkItem",
                        SubjectEntityId = "42",
                    }));

            Assert.Contains(nameof(ChatSession.SubjectModule), ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task EnsureSessionForThreadAsync_PersistsThinMetadataAndReactivatesArchivedSession()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            using IServiceScope scope = provider.CreateScope();
            ChatSessionService service = scope.ServiceProvider.GetRequiredService<ChatSessionService>();

            ChatSessionService.ChatSessionEnsureResult created = await service.EnsureSessionForThreadAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-metadata",
                    FirstUserMessage = "Investigate an approval timeout.",
                    SubjectModule = "Todo",
                    SubjectEntityType = "TodoItem",
                    SubjectEntityId = "todo-42",
                    PreferredModelId = "gpt-4.1",
                });

            await service.ArchiveSessionAsync(created.SessionId);

            ChatSessionService.ChatSessionEnsureResult reactivated = await service.EnsureSessionForThreadAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-metadata",
                    PreferredModelId = "gpt-4o-mini",
                });

            ChatSessionDetail? detail = await service.GetSessionAsync(created.SessionId);
            List<ChatSessionSummary> sessions = await service.ListSessionsAsync();

            Assert.Equal(created.SessionId, reactivated.SessionId);
            Assert.NotNull(detail);
            Assert.Equal("Active", detail.Status);
            Assert.Null(detail.ArchivedAt);
            Assert.Equal("Todo", detail.SubjectModule);
            Assert.Equal("TodoItem", detail.SubjectEntityType);
            Assert.Equal("todo-42", detail.SubjectEntityId);
            Assert.Equal("gpt-4o-mini", detail.PreferredModelId);
            Assert.Equal(ChatSessionProtocolVersions.Current, detail.ServerProtocolVersion);
            Assert.Contains(sessions, session => session.Id == created.SessionId && session.ServerProtocolVersion == ChatSessionProtocolVersions.Current);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    private static ServiceProvider CreateServiceProvider(string dbPath)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<ChatSessionsDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<ChatSessionService>();
        return services.BuildServiceProvider();
    }

    private static async Task InitializeDatabaseAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        ChatSessionsDbContext db = scope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>();
        await ChatSessionsDatabaseInitializer.InitializeAsync(db);
    }

    private static string CreateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"aguidojo-chat-sessions-{Guid.NewGuid():N}.db");
}
