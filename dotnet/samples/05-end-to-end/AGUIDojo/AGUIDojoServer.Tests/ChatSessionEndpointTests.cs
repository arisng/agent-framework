using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AGUIDojoServer.Api;
using AGUIDojoServer.ChatSessions;
using AGUIDojoServer.Data;
using AGUIDojoServer.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer.Tests;

public sealed class ChatSessionEndpointTests
{
    [Fact]
    public async Task ChatSessionEndpoints_SurfaceThinRecoveryMetadata()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using TestApiHost host = await TestApiHost.StartAsync(
                dbPath,
                new Dictionary<string, string?> { ["OPENAI_MODEL"] = "gpt-4.1" });

            const string firstUserMessage =
                """
                Plan a Seattle weekend with ferry time,
                museum stops, lunch ideas, and rainy-day fallback activities for kids.
                """;

            string sessionId = await host.CreateSessionAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-api-metadata",
                    FirstUserMessage = firstUserMessage,
                    SubjectModule = "Todo",
                    SubjectEntityType = "TodoItem",
                    SubjectEntityId = "todo-42",
                    PreferredModelId = "gpt-4.1",
                });

            List<ChatSessionSummary>? summaries =
                await host.Client.GetFromJsonAsync<List<ChatSessionSummary>>("/api/chat-sessions");
            Assert.NotNull(summaries);

            ChatSessionSummary summary = Assert.Single(summaries);
            Assert.Equal(sessionId, summary.Id);
            Assert.Equal(BuildExpectedTitle(firstUserMessage), summary.Title);
            Assert.Equal("Todo", summary.SubjectModule);
            Assert.Equal("TodoItem", summary.SubjectEntityType);
            Assert.Equal("todo-42", summary.SubjectEntityId);
            Assert.Equal("gpt-4.1", summary.PreferredModelId);
            Assert.Equal(ChatSessionProtocolVersions.Current, summary.ServerProtocolVersion);

            ChatSessionDetail? detail =
                await host.Client.GetFromJsonAsync<ChatSessionDetail>($"/api/chat-sessions/{sessionId}");

            Assert.NotNull(detail);
            Assert.Equal(summary.Title, detail.Title);
            Assert.Equal("Active", detail.Status);
            Assert.Equal("thread-api-metadata", detail.AguiThreadId);
            Assert.Equal("Todo", detail.SubjectModule);
            Assert.Equal("TodoItem", detail.SubjectEntityType);
            Assert.Equal("todo-42", detail.SubjectEntityId);
            Assert.Equal("gpt-4.1", detail.PreferredModelId);
            Assert.Equal(ChatSessionProtocolVersions.Current, detail.ServerProtocolVersion);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ConversationEndpoint_ReturnsCanonicalBranchingGraph()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using TestApiHost host = await TestApiHost.StartAsync(dbPath);
            string sessionId = await host.CreateSessionAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-api-conversation",
                    FirstUserMessage = "Plan a launch checklist.",
                });

            ChatMessage root = new(ChatRole.User, "Plan a launch checklist.")
            {
                AuthorName = "User",
                MessageId = "api-user-1",
            };
            ChatMessage original = new(ChatRole.Assistant, "Here is the original checklist.")
            {
                AuthorName = "Planner",
                MessageId = "api-assistant-1",
            };
            ChatMessage branch = new(
                ChatRole.Assistant,
                [
                    new TextContent("Here is the revised checklist."),
                    new FunctionResultContent("tool-call-2", new { revised = true }),
                ])
            {
                AuthorName = "Planner",
                MessageId = "api-assistant-2",
            };

            await host.PersistConversationAsync(sessionId, root, original);
            await host.PersistConversationAsync(sessionId, root, branch);

            ChatConversationGraph? graph =
                await host.Client.GetFromJsonAsync<ChatConversationGraph>($"/api/chat-sessions/{sessionId}/conversation");

            Assert.NotNull(graph);
            Assert.Equal(3, graph.Nodes.Count);
            ChatConversationNodeDto rootNode = Assert.Single(graph.Nodes, node => node.Id == graph.RootId);
            Assert.Equal(2, rootNode.ChildIds.Count);
            ChatConversationNodeDto activeLeaf = Assert.Single(graph.Nodes, node => node.Id == graph.ActiveLeafId);
            Assert.Equal(rootNode.Id, activeLeaf.ParentId);
            Assert.Equal("api-assistant-2", activeLeaf.MessageId);
            Assert.Contains("FunctionResultContent", activeLeaf.Content?.GetRawText(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ConversationEndpoints_UpdateActiveLeafAndClearGraph()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using TestApiHost host = await TestApiHost.StartAsync(dbPath);
            string sessionId = await host.CreateSessionAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-api-branch-selection",
                    FirstUserMessage = "Walk me through the branch selection flow.",
                });

            ChatMessage root = new(ChatRole.User, "Walk me through the branch selection flow.")
            {
                AuthorName = "User",
                MessageId = "api-user-selection",
            };
            ChatMessage original = new(ChatRole.Assistant, "Original branch")
            {
                AuthorName = "Planner",
                MessageId = "api-assistant-selection-1",
            };
            ChatMessage alternative = new(ChatRole.Assistant, "Alternative branch")
            {
                AuthorName = "Planner",
                MessageId = "api-assistant-selection-2",
            };

            await host.PersistConversationAsync(sessionId, root, original);
            await host.PersistConversationAsync(sessionId, root, alternative);

            ChatConversationGraph? initialGraph =
                await host.Client.GetFromJsonAsync<ChatConversationGraph>($"/api/chat-sessions/{sessionId}/conversation");
            Assert.NotNull(initialGraph);

            ChatConversationNodeDto rootNode = Assert.Single(initialGraph.Nodes, node => node.Id == initialGraph.RootId);
            ChatConversationNodeDto originalNode = Assert.Single(initialGraph.Nodes, node => node.MessageId == "api-assistant-selection-1");

            HttpResponseMessage selectionResponse = await host.Client.PutAsJsonAsync(
                $"/api/chat-sessions/{sessionId}/active-leaf",
                new ChatConversationActiveLeafUpdate { ActiveLeafId = originalNode.Id });
            Assert.Equal(HttpStatusCode.NoContent, selectionResponse.StatusCode);

            ChatConversationGraph? selectedGraph =
                await host.Client.GetFromJsonAsync<ChatConversationGraph>($"/api/chat-sessions/{sessionId}/conversation");
            Assert.NotNull(selectedGraph);
            Assert.Equal(originalNode.Id, selectedGraph.ActiveLeafId);
            Assert.Equal(rootNode.Id, selectedGraph.RootId);

            HttpResponseMessage clearResponse =
                await host.Client.DeleteAsync(new Uri($"/api/chat-sessions/{sessionId}/conversation", UriKind.Relative));
            Assert.Equal(HttpStatusCode.NoContent, clearResponse.StatusCode);

            ChatConversationGraph? clearedGraph =
                await host.Client.GetFromJsonAsync<ChatConversationGraph>($"/api/chat-sessions/{sessionId}/conversation");
            Assert.NotNull(clearedGraph);
            Assert.Null(clearedGraph.RootId);
            Assert.Null(clearedGraph.ActiveLeafId);
            Assert.Empty(clearedGraph.Nodes);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ArchiveEndpoint_RemovesSessionFromActiveListButKeepsDetail()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using TestApiHost host = await TestApiHost.StartAsync(dbPath);
            string sessionId = await host.CreateSessionAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-api-archive",
                    FirstUserMessage = "Archive this session from the API surface.",
                });

            HttpResponseMessage archiveResponse =
                await host.Client.PostAsync(new Uri($"/api/chat-sessions/{sessionId}/archive", UriKind.Relative), content: null);

            Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

            List<ChatSessionSummary>? summaries =
                await host.Client.GetFromJsonAsync<List<ChatSessionSummary>>("/api/chat-sessions");
            ChatSessionDetail? detail =
                await host.Client.GetFromJsonAsync<ChatSessionDetail>($"/api/chat-sessions/{sessionId}");

            Assert.NotNull(summaries);
            Assert.DoesNotContain(summaries, summary => summary.Id == sessionId);
            Assert.NotNull(detail);
            Assert.Equal("Archived", detail.Status);
            Assert.NotNull(detail.ArchivedAt);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task ModelsEndpoint_ReturnsCatalogAndConfiguredActiveModel()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using TestApiHost host = await TestApiHost.StartAsync(
                dbPath,
                new Dictionary<string, string?> { ["OPENAI_MODEL"] = "gpt-4.1" });

            using JsonDocument response = JsonDocument.Parse(
                await host.Client.GetStringAsync(new Uri("/api/models", UriKind.Relative)));

            Assert.Equal("gpt-4.1", response.RootElement.GetProperty("activeModelId").GetString());
            JsonElement.ArrayEnumerator models = response.RootElement.GetProperty("models").EnumerateArray();
            Assert.Contains(models, model => model.GetProperty("modelId").GetString() == "gpt-4.1");
            Assert.Contains(response.RootElement.GetProperty("models").EnumerateArray(), model => model.GetProperty("modelId").GetString() == "gpt-5.4-nano");
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    private static string BuildExpectedTitle(string firstUserMessage)
    {
        string collapsed = string.Join(
            " ",
            firstUserMessage
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return collapsed.Length <= 80
            ? collapsed
            : $"{collapsed[..77]}...";
    }

    private static string CreateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"aguidojo-chat-session-endpoints-{Guid.NewGuid():N}.db");

    private sealed class TestApiHost : IAsyncDisposable
    {
        private TestApiHost(WebApplication app, HttpClient client)
        {
            App = app;
            Client = client;
        }

        public WebApplication App { get; }

        public HttpClient Client { get; }

        public static async Task<TestApiHost> StartAsync(
            string dbPath,
            IReadOnlyDictionary<string, string?>? configuration = null)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();

            if (configuration is not null)
            {
                builder.Configuration.AddInMemoryCollection(configuration);
            }

            builder.Services.AddDbContext<ChatSessionsDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
            builder.Services.AddScoped<ChatSessionService>();
            builder.Services.AddScoped<ChatConversationService>();
            builder.Services.AddSingleton<IModelRegistry, ModelRegistry>();

            WebApplication app = builder.Build();
            RouteGroupBuilder apiGroup = app.MapGroup("/api");
            apiGroup.MapModelsEndpoints();
            apiGroup.MapChatSessionEndpoints();

            await app.StartAsync();

            using IServiceScope scope = app.Services.CreateScope();
            await ChatSessionsDatabaseInitializer.InitializeAsync(
                scope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>());

            return new TestApiHost(app, app.GetTestClient());
        }

        public async Task<string> CreateSessionAsync(ChatSessionService.ChatSessionEnsureRequest request)
        {
            using IServiceScope scope = App.Services.CreateScope();
            ChatSessionService service = scope.ServiceProvider.GetRequiredService<ChatSessionService>();
            ChatSessionService.ChatSessionEnsureResult result = await service.EnsureSessionForThreadAsync(request);
            return result.SessionId;
        }

        public async Task PersistConversationAsync(string sessionId, params ChatMessage[] messages)
        {
            using IServiceScope scope = App.Services.CreateScope();
            ChatConversationService service = scope.ServiceProvider.GetRequiredService<ChatConversationService>();
            await service.PersistConversationAsync(sessionId, messages);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}
