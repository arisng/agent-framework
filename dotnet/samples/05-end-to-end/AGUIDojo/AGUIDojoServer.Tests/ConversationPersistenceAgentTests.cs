using System.Text.Json;
using AGUIDojoServer.ChatSessions;
using AGUIDojoServer.Data;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AGUIDojoServer.Tests;

public sealed class ConversationPersistenceAgentTests
{
    [Fact]
    public async Task RunStreamingAsync_PersistsRequestAndAssistantResponse()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            using IServiceScope scope = provider.CreateScope();
            ChatSessionService sessionService = scope.ServiceProvider.GetRequiredService<ChatSessionService>();
            ChatConversationService conversationService = scope.ServiceProvider.GetRequiredService<ChatConversationService>();
            IHttpContextAccessor httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

            ChatSessionService.ChatSessionEnsureResult session = await sessionService.EnsureSessionForThreadAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-agent-persistence",
                    FirstUserMessage = "Summarize the deployment issue.",
                });

            DefaultHttpContext httpContext = new()
            {
                RequestServices = scope.ServiceProvider,
            };
            httpContext.Items[ChatSessionHttpContextItems.SessionId] = session.SessionId;
            httpContextAccessor.HttpContext = httpContext;

            ChatMessage assistantResponse = new(
                ChatRole.Assistant,
                [
                    new TextContent("Deployment issue summarized."),
                    new FunctionResultContent("tool-call-9", new { status = "ok" }),
                ])
            {
                AuthorName = "Planner",
                MessageId = "assistant-runtime-9",
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["server_session_id"] = session.SessionId,
                    ["summaryKind"] = "deployment",
                },
            };

            var agent = new ConversationPersistenceAgent(
                new RecordingAgent(assistantResponse),
                httpContextAccessor);

            ChatMessage userMessage = new(ChatRole.User, "Summarize the deployment issue.")
            {
                AuthorName = "User",
                MessageId = "user-runtime-9",
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["agui_thread_id"] = "thread-agent-persistence",
                    ["correlation"] = JsonSerializer.SerializeToElement(new { source = "test" }),
                },
            };

            await foreach (AgentResponseUpdate _ in agent.RunStreamingAsync([userMessage]))
            {
            }

            ChatConversationGraph? graph = await conversationService.GetConversationAsync(session.SessionId);

            Assert.NotNull(graph);
            Assert.Equal(2, graph.Nodes.Count);
            ChatConversationNodeDto root = Assert.Single(graph.Nodes, node => node.ParentId is null);
            ChatConversationNodeDto leaf = Assert.Single(graph.Nodes, node => node.ParentId == root.Id);
            Assert.Equal(root.Id, graph.RootId);
            Assert.Equal(leaf.Id, graph.ActiveLeafId);
            Assert.Equal("user-runtime-9", root.MessageId);
            Assert.Equal("assistant-runtime-9", leaf.MessageId);
            Assert.Contains("FunctionResultContent", leaf.Content?.GetRawText(), StringComparison.Ordinal);
            Assert.Equal("deployment", leaf.AdditionalProperties!.Value.GetProperty("summaryKind").GetString());
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task RunStreamingAsync_DeduplicatesDuplicateToolResultUpdatesForSameCallIdAsync()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            using IServiceScope scope = provider.CreateScope();
            ChatSessionService sessionService = scope.ServiceProvider.GetRequiredService<ChatSessionService>();
            ChatConversationService conversationService = scope.ServiceProvider.GetRequiredService<ChatConversationService>();
            IHttpContextAccessor httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

            ChatSessionService.ChatSessionEnsureResult session = await sessionService.EnsureSessionForThreadAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-agent-duplicate-tool-result",
                    FirstUserMessage = "Execute the tool exactly once.",
                });

            DefaultHttpContext httpContext = new()
            {
                RequestServices = scope.ServiceProvider,
            };
            httpContext.Items[ChatSessionHttpContextItems.SessionId] = session.SessionId;
            httpContextAccessor.HttpContext = httpContext;

            var agent = new ConversationPersistenceAgent(
                new ScriptedStreamingAgent(
                [
                    CreateUpdate(
                        ChatRole.Assistant,
                        "assistant-tool-call-1",
                        new FunctionCallContent("tool-call-duplicate-1", "create_plan", new Dictionary<string, object?>())),
                    CreateUpdate(
                        ChatRole.Tool,
                        "tool-runtime-duplicate-1",
                        new FunctionResultContent("tool-call-duplicate-1", new { status = "completed" })),
                    CreateUpdate(
                        ChatRole.Tool,
                        "tool-runtime-duplicate-1",
                        new FunctionResultContent("tool-call-duplicate-1", new { status = "completed" })),
                    CreateUpdate(
                        ChatRole.Assistant,
                        "assistant-final-1",
                        new TextContent("Tool execution recorded.")),
                ]),
                httpContextAccessor);

            ChatMessage userMessage = new(ChatRole.User, "Execute the tool exactly once.")
            {
                AuthorName = "User",
                MessageId = "user-runtime-duplicate-1",
            };

            await foreach (AgentResponseUpdate _ in agent.RunStreamingAsync([userMessage]))
            {
            }

            ChatConversationGraph? graph = await conversationService.GetConversationAsync(session.SessionId);

            Assert.NotNull(graph);
            Assert.Equal(4, graph.Nodes.Count);

            ChatConversationNodeDto toolNode = Assert.Single(graph.Nodes, node => node.Role == ChatRole.Tool.Value);
            Assert.Equal(1, CountFunctionResults(toolNode.Content, "tool-call-duplicate-1"));
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
        services.AddHttpContextAccessor();
        services.AddDbContext<ChatSessionsDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<ChatSessionWorkspaceService>();
        services.AddScoped<ChatSessionService>();
        services.AddScoped<ChatConversationService>();
        return services.BuildServiceProvider();
    }

    private static async Task InitializeDatabaseAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        ChatSessionsDbContext db = scope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>();
        await ChatSessionsDatabaseInitializer.InitializeAsync(db);
    }

    private static string CreateDatabasePath() =>
        Path.Combine(GetTestArtifactDirectory(), $"aguidojo-chat-persistence-agent-{Guid.NewGuid():N}.db");

    private static string GetTestArtifactDirectory()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "test-artifacts");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static AgentResponseUpdate CreateUpdate(ChatRole role, string messageId, AIContent content) =>
        new()
        {
            Role = role,
            MessageId = messageId,
            Contents = [content],
        };

    private static int CountFunctionResults(JsonElement? content, string callId)
    {
        if (content is not JsonElement element || element.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        int count = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("type", out JsonElement typeElement) ||
                !string.Equals(typeElement.GetString(), nameof(FunctionResultContent), StringComparison.Ordinal) ||
                !item.TryGetProperty("value", out JsonElement valueElement) ||
                !valueElement.TryGetProperty("callId", out JsonElement callIdElement) ||
                !string.Equals(callIdElement.GetString(), callId, StringComparison.Ordinal))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private sealed class RecordingAgent(ChatMessage responseMessage) : AIAgent
    {
        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default) =>
            new(new TestAgentSession());

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default) =>
            new(JsonSerializer.SerializeToElement(new { }));

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default) =>
            new(new TestAgentSession());

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentResponse([responseMessage]));

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (AIContent content in responseMessage.Contents)
            {
                yield return new AgentResponseUpdate
                {
                    Role = responseMessage.Role,
                    AuthorName = responseMessage.AuthorName,
                    MessageId = responseMessage.MessageId,
                    AdditionalProperties = responseMessage.AdditionalProperties,
                    Contents = [content],
                };
            }

            await Task.CompletedTask;
        }

        private sealed class TestAgentSession : AgentSession;
    }

    private sealed class ScriptedStreamingAgent(IReadOnlyList<AgentResponseUpdate> updates) : AIAgent
    {
        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default) =>
            new(new TestAgentSession());

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default) =>
            new(JsonSerializer.SerializeToElement(new { }));

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default) =>
            new(new TestAgentSession());

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(updates.ToAgentResponse());

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (AgentResponseUpdate update in updates)
            {
                yield return update;
            }

            await Task.CompletedTask;
        }

        private sealed class TestAgentSession : AgentSession;
    }
}
