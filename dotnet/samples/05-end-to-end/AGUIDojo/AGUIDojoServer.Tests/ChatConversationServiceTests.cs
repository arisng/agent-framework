using System.Text.Json;
using AGUIDojoServer.ChatSessions;
using AGUIDojoServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AGUIDojoServer.Tests;

public sealed class ChatConversationServiceTests
{
    [Fact]
    public async Task PersistConversationAsync_PersistsBranchingGraphAcrossContexts()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using (ServiceProvider provider = CreateServiceProvider(dbPath))
            {
                await InitializeDatabaseAsync(provider);

                using IServiceScope scope = provider.CreateScope();
                ChatSessionService sessionService = scope.ServiceProvider.GetRequiredService<ChatSessionService>();
                ChatConversationService conversationService = scope.ServiceProvider.GetRequiredService<ChatConversationService>();

                string sessionId = await sessionService.EnsureSessionForThreadAsync(
                    "thread-graph-persistence",
                    "Draft a launch checklist.");

                ChatMessage root = new(ChatRole.User, "Draft a launch checklist.")
                {
                    AuthorName = "User",
                    MessageId = "user-runtime-1",
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["subjectModule"] = "Todo",
                    },
                };

                ChatMessage originalAssistant = new(
                    ChatRole.Assistant,
                    [
                        new TextContent("Here is the first checklist draft."),
                        new FunctionCallContent("tool-call-1", "create_plan", new Dictionary<string, object?>
                        {
                            ["goal"] = "launch",
                            ["steps"] = 3,
                        }),
                    ])
                {
                    AuthorName = "Planner",
                    MessageId = "assistant-runtime-1",
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["confidence"] = 0.91,
                    },
                };

                await conversationService.PersistConversationAsync(sessionId, [root, originalAssistant]);

                ChatMessage branchedAssistant = new(
                    ChatRole.Assistant,
                    [
                        new TextContent("Here is a revised checklist with rollback notes."),
                        new FunctionResultContent("tool-call-1", new Dictionary<string, object?>
                        {
                            ["rollbackAdded"] = true,
                        }),
                    ])
                {
                    AuthorName = "Planner",
                    MessageId = "assistant-runtime-2",
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["branchReason"] = JsonSerializer.SerializeToElement(new { source = "edit-and-regenerate" }),
                    },
                };

                await conversationService.PersistConversationAsync(sessionId, [root, branchedAssistant]);
            }

            using ServiceProvider verificationProvider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(verificationProvider);

            using IServiceScope verificationScope = verificationProvider.CreateScope();
            ChatSessionService verificationSessionService = verificationScope.ServiceProvider.GetRequiredService<ChatSessionService>();
            ChatConversationService verificationConversationService = verificationScope.ServiceProvider.GetRequiredService<ChatConversationService>();

            string existingSessionId = await verificationSessionService.EnsureSessionForThreadAsync("thread-graph-persistence");
            ChatSessionDetail? detail = await verificationSessionService.GetSessionAsync(existingSessionId);
            ChatConversationGraph? graph = await verificationConversationService.GetConversationAsync(detail!.Id);

            Assert.NotNull(graph);
            Assert.NotNull(detail.RootMessageId);
            Assert.NotNull(detail.ActiveLeafMessageId);
            Assert.Equal(detail.RootMessageId, graph.RootId);
            Assert.Equal(detail.ActiveLeafMessageId, graph.ActiveLeafId);
            Assert.Equal(3, graph.Nodes.Count);

            ChatConversationNodeDto rootNode = Assert.Single(graph.Nodes, node => node.Id == graph.RootId);
            Assert.Equal("user", rootNode.Role);
            Assert.Equal("user-runtime-1", rootNode.MessageId);
            Assert.Equal(2, rootNode.ChildIds.Count);
            Assert.Equal("Draft a launch checklist.", rootNode.Text);
            Assert.Equal("Todo", rootNode.AdditionalProperties!.Value.GetProperty("subjectModule").GetString());

            ChatConversationNodeDto activeLeaf = Assert.Single(graph.Nodes, node => node.Id == graph.ActiveLeafId);
            Assert.Equal(rootNode.Id, activeLeaf.ParentId);
            Assert.Equal("Planner", activeLeaf.AuthorName);
            Assert.Equal("assistant-runtime-2", activeLeaf.MessageId);
            Assert.Contains("TextContent", activeLeaf.Content?.GetRawText(), StringComparison.Ordinal);
            Assert.Equal(
                "edit-and-regenerate",
                activeLeaf.AdditionalProperties!.Value.GetProperty("branchReason").GetProperty("source").GetString());
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task PersistConversationAsync_NormalizesExistingDuplicateFunctionResultsForReplayAsync()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            using IServiceScope scope = provider.CreateScope();
            ChatSessionsDbContext db = scope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>();
            ChatSessionService sessionService = scope.ServiceProvider.GetRequiredService<ChatSessionService>();
            ChatConversationService conversationService = scope.ServiceProvider.GetRequiredService<ChatConversationService>();

            string sessionId = await sessionService.EnsureSessionForThreadAsync(
                "thread-duplicate-tool-result-normalization",
                "Apply the tool result once.");

            ChatMessage root = new(ChatRole.User, "Apply the tool result once.")
            {
                MessageId = "user-runtime-dup-1",
            };

            ChatMessage toolMessage = new(
                ChatRole.Tool,
                [
                    new FunctionResultContent("tool-call-dup-1", new Dictionary<string, object?>
                    {
                        ["status"] = "completed",
                    }),
                ])
            {
                MessageId = "tool-runtime-dup-1",
            };

            ChatConversationGraph initialGraph = await conversationService.PersistConversationAsync(sessionId, [root, toolMessage]);
            string toolNodeId = Assert.Single(initialGraph.Nodes, node => node.Role == ChatRole.Tool.Value).Id;

            ChatConversationNode storedToolNode = await db.ChatConversationNodes.SingleAsync(node => node.NodeId == toolNodeId);
            JsonElement storedPayload = Assert.Single(ParseJson(storedToolNode.ContentJson)!.Value.EnumerateArray());
            string duplicatedPayload = storedPayload.GetRawText();
            storedToolNode.ContentJson = $"[{duplicatedPayload},{duplicatedPayload}]";
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            ChatConversationGraph? rehydratedGraph = await conversationService.GetConversationAsync(sessionId);
            ChatConversationNodeDto rehydratedToolNode = Assert.Single(rehydratedGraph!.Nodes, node => node.Id == toolNodeId);
            Assert.Equal(1, CountFunctionResults(rehydratedToolNode.Content, "tool-call-dup-1"));

            ChatConversationGraph persistedGraph = await conversationService.PersistConversationAsync(sessionId, [root, toolMessage]);

            Assert.Equal(2, persistedGraph.Nodes.Count);
            Assert.Equal(toolNodeId, persistedGraph.ActiveLeafId);

            ChatConversationNodeDto normalizedToolNode = Assert.Single(persistedGraph.Nodes, node => node.Id == toolNodeId);
            Assert.Equal(1, CountFunctionResults(normalizedToolNode.Content, "tool-call-dup-1"));

            storedToolNode = await db.ChatConversationNodes.SingleAsync(node => node.NodeId == toolNodeId);
            Assert.Equal(1, CountFunctionResults(ParseJson(storedToolNode.ContentJson), "tool-call-dup-1"));
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
        Path.Combine(GetTestArtifactDirectory(), $"aguidojo-chat-conversation-{Guid.NewGuid():N}.db");

    private static string GetTestArtifactDirectory()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "test-artifacts");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

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
}
