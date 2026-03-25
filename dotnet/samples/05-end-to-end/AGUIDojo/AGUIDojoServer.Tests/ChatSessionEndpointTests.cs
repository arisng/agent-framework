using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AGUIDojoServer.AgenticUI;
using AGUIDojoServer.Api;
using AGUIDojoServer.ChatSessions;
using AGUIDojoServer.Data;
using AGUIDojoServer.HumanInTheLoop;
using AGUIDojoServer.Models;
using AGUIDojoServer.PredictiveStateUpdates;
using AGUIDojoServer.SharedState;
using AGUIDojoServer.Tools;
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
    private static readonly string[] s_duplicateAuditPlanSteps = ["Inspect", "Implement"];

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
                    OwnerId = "alice",
                    TenantId = "tenant-red",
                    WorkflowInstanceId = "workflow-42",
                    RuntimeInstanceId = "runtime-42",
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
            Assert.Equal("alice", summary.OwnerId);
            Assert.Equal("tenant-red", summary.TenantId);
            Assert.Equal("workflow-42", summary.WorkflowInstanceId);
            Assert.Equal("runtime-42", summary.RuntimeInstanceId);
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
            Assert.Equal("alice", detail.OwnerId);
            Assert.Equal("tenant-red", detail.TenantId);
            Assert.Equal("workflow-42", detail.WorkflowInstanceId);
            Assert.Equal("runtime-42", detail.RuntimeInstanceId);
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

    [Fact]
    public async Task WorkspaceEndpoints_CombineDerivedStateWithImportedBrowserState()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using TestApiHost host = await TestApiHost.StartAsync(
                dbPath,
                new Dictionary<string, string?> { ["OPENAI_MODEL"] = "gpt-4.1" });

            string sessionId = await host.CreateSessionAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-api-workspace",
                    FirstUserMessage = "Build a durable work log for this session.",
                    PreferredModelId = "gpt-4.1",
                });

            ChatMessage root = new(ChatRole.User, "Build a durable work log for this session.")
            {
                AuthorName = "User",
                MessageId = "workspace-user-1",
            };

            byte[] planSnapshotBytes = Encoding.UTF8.GetBytes(
                """
                {"$type":"plan_snapshot","data":{"steps":[{"description":"Inspect tools","status":"Completed"},{"description":"Publish results","status":"Pending"}]}}
                """);

            ChatMessage assistant = new(
                ChatRole.Assistant,
                [
                    new DataContent(planSnapshotBytes, "application/json"),
                    new FunctionCallContent(
                        callId: "approval-1",
                        name: "request_approval",
                        arguments: new Dictionary<string, object?>
                        {
                            ["request"] = new ApprovalRequest
                            {
                                ApprovalId = "approval-1",
                                FunctionName = "send_email",
                                Message = "Approve execution of 'send_email'?",
                                FunctionArguments = JsonSerializer.SerializeToElement(new { to = "demo@example.com" }),
                            }
                        }),
                    new FunctionResultContent(
                        callId: "approval-1",
                        result: JsonSerializer.SerializeToElement(new ApprovalResponse
                        {
                            ApprovalId = "approval-1",
                            Approved = true,
                        })),
                    new FunctionCallContent(
                        callId: "grid-1",
                        name: "show_data_grid",
                        arguments: new Dictionary<string, object?> { ["title"] = "Inventory" }),
                    new FunctionResultContent(
                        callId: "grid-1",
                        result: JsonSerializer.SerializeToElement(new DataGridResult(
                            "Inventory",
                            ["Name"],
                            [new Dictionary<string, string> { ["Name"] = "AGUIDojo" }]))),
                ])
            {
                AuthorName = "Planner",
                MessageId = "workspace-assistant-1",
            };

            await host.PersistConversationAsync(sessionId, root, assistant);

            using (IServiceScope scope = host.App.Services.CreateScope())
            {
                ChatSessionAuditService auditService = scope.ServiceProvider.GetRequiredService<ChatSessionAuditService>();
                await auditService.AppendAsync(
                    sessionId,
                    ChatAuditEventType.ModelRouting,
                    "Model routed to gpt-4.1",
                    "Routing to preferred model gpt-4.1.",
                    new
                    {
                        preferredModelId = "gpt-4.1",
                        effectiveModelId = "gpt-4.1",
                        routingReason = "Routing to preferred model gpt-4.1.",
                    });
                await auditService.AppendAsync(
                    sessionId,
                    ChatAuditEventType.CompactionCheckpoint,
                    "Compaction reduced invocation context",
                    "Input messages: 12; compacted messages: 8.",
                    new
                    {
                        preferredModelId = "gpt-4.1",
                        effectiveModelId = "gpt-4.1",
                        inputMessageCount = 12,
                        outputMessageCount = 8,
                        wasCompacted = true,
                    });
            }

            HttpResponseMessage importResponse = await host.Client.PostAsJsonAsync(
                $"/api/chat-sessions/{sessionId}/workspace/import",
                new ChatSessionWorkspaceImportRequest
                {
                    Snapshot = new ChatSessionWorkspaceImportSnapshotDto
                    {
                        CurrentRecipe = new Recipe { Title = "Imported recipe" },
                        CurrentDocument = new DocumentState { Document = "# Imported document" },
                        IsDocumentPreview = false,
                    },
                    PendingApproval = new ChatSessionPendingApprovalImportDto
                    {
                        ApprovalId = "approval-2",
                        FunctionName = "write_document",
                        Message = "Approve execution of 'write_document'?",
                        FunctionArgumentsJson = "{\"document\":\"# Imported document\"}",
                        RequestedAt = DateTimeOffset.UtcNow,
                    },
                    AuditEntries =
                    [
                        new ChatSessionAuditImportEntryDto
                        {
                            Id = "audit-import-1",
                            EventType = "ApprovalResolved",
                            OccurredAt = DateTimeOffset.UtcNow,
                            ApprovalId = "approval-2",
                            FunctionName = "write_document",
                            RiskLevel = "Medium",
                            AutonomyLevel = "Suggest",
                            WasApproved = true,
                            WasAutoDecided = false,
                        }
                    ],
                });

            Assert.Equal(HttpStatusCode.NoContent, importResponse.StatusCode);

            ChatSessionDetail? detail =
                await host.Client.GetFromJsonAsync<ChatSessionDetail>($"/api/chat-sessions/{sessionId}");
            ChatSessionWorkspaceDto? workspace =
                await host.Client.GetFromJsonAsync<ChatSessionWorkspaceDto>($"/api/chat-sessions/{sessionId}/workspace");

            Assert.NotNull(detail);
            Assert.Equal(2, detail.ApprovalCount);
            Assert.Equal(0, detail.PendingApprovalCount);
            Assert.True(detail.AuditEventCount >= 4);
            Assert.True(detail.ArtifactCount >= 3);
            Assert.Equal("gpt-4.1", detail.LatestEffectiveModelId);
            Assert.NotNull(detail.LatestCompactionAt);

            Assert.NotNull(workspace);
            Assert.NotNull(workspace.Snapshot);
            Assert.NotNull(workspace.Snapshot!.CurrentPlan);
            Assert.Equal(2, workspace.Snapshot.CurrentPlan!.Steps.Count);
            Assert.Equal("Imported recipe", workspace.Snapshot.CurrentRecipe?.Title);
            Assert.Equal("# Imported document", workspace.Snapshot.CurrentDocument?.Document);
            Assert.Contains(workspace.Approvals, item => item.ApprovalId == "approval-1" && item.Status == "Approved");
            Assert.Contains(workspace.Approvals, item => item.ApprovalId == "approval-2" && item.Status == "Approved");
            Assert.Contains(workspace.AuditEvents, item => item.EventType == "ModelRouting" && item.EffectiveModelId == "gpt-4.1");
            Assert.Contains(workspace.AuditEvents, item => item.EventType == "CompactionCheckpoint" && item.WasCompacted == true);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task PersistConversationAsync_DeduplicatesDerivedAuditEventsWithinSingleRefresh()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using TestApiHost host = await TestApiHost.StartAsync(dbPath);
            string sessionId = await host.CreateSessionAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-api-audit-dedup",
                    FirstUserMessage = "Create a plan and keep the audit projection stable.",
                });

            ChatMessage root = new(ChatRole.User, "Create a plan and keep the audit projection stable.")
            {
                AuthorName = "User",
                MessageId = "audit-user-1",
            };

            ChatMessage assistant = new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        callId: "plan-call-1",
                        name: "create_plan",
                        arguments: new Dictionary<string, object?> { ["steps"] = s_duplicateAuditPlanSteps }),
                    new FunctionCallContent(
                        callId: "plan-call-1",
                        name: "create_plan",
                        arguments: new Dictionary<string, object?> { ["steps"] = s_duplicateAuditPlanSteps }),
                ])
            {
                AuthorName = "Planner",
                MessageId = "audit-assistant-1",
            };

            await host.PersistConversationAsync(sessionId, root, assistant);

            ChatSessionWorkspaceDto? workspace =
                await host.Client.GetFromJsonAsync<ChatSessionWorkspaceDto>($"/api/chat-sessions/{sessionId}/workspace");

            Assert.NotNull(workspace);
            ChatSessionAuditEventDto toolCallEvent = Assert.Single(
                workspace!.AuditEvents,
                item =>
                    item.EventType == ChatAuditEventType.ToolCall.ToString() &&
                    item.FunctionName == "create_plan");
            Assert.Equal("Tool call: create_plan", toolCallEvent.Title);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task WorkspaceImport_DeduplicatesAuditEntriesWithinSingleRequest()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using TestApiHost host = await TestApiHost.StartAsync(dbPath);
            string sessionId = await host.CreateSessionAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-api-import-audit-dedup",
                    FirstUserMessage = "Import workspace audit safely.",
                });

            DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
            ChatSessionAuditImportEntryDto auditEntry = new()
            {
                Id = "audit-import-dedup-1",
                EventType = ChatAuditEventType.ApprovalResolved.ToString(),
                OccurredAt = occurredAt,
                ApprovalId = "approval-import-1",
                FunctionName = "send_email",
                RiskLevel = "Medium",
                AutonomyLevel = "Suggest",
                WasApproved = true,
                WasAutoDecided = false,
            };

            HttpResponseMessage importResponse = await host.Client.PostAsJsonAsync(
                $"/api/chat-sessions/{sessionId}/workspace/import",
                new ChatSessionWorkspaceImportRequest
                {
                    AuditEntries = [auditEntry, auditEntry],
                });

            Assert.Equal(HttpStatusCode.NoContent, importResponse.StatusCode);

            ChatSessionWorkspaceDto? workspace =
                await host.Client.GetFromJsonAsync<ChatSessionWorkspaceDto>($"/api/chat-sessions/{sessionId}/workspace");

            Assert.NotNull(workspace);
            ChatSessionAuditEventDto importedEvent = Assert.Single(
                workspace!.AuditEvents,
                item => item.Id == "audit-import-dedup-1");
            Assert.Equal(ChatAuditEventType.ApprovalResolved.ToString(), importedEvent.EventType);
            Assert.Equal("send_email", importedEvent.FunctionName);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task PersistConversationAsync_AllowsSameDerivedAuditSuffixAcrossDifferentSessions()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using TestApiHost host = await TestApiHost.StartAsync(dbPath);
            string firstSessionId = await host.CreateSessionAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-api-cross-session-audit-1",
                    FirstUserMessage = "Create a plan in session one.",
                });
            string secondSessionId = await host.CreateSessionAsync(
                new ChatSessionService.ChatSessionEnsureRequest
                {
                    AguiThreadId = "thread-api-cross-session-audit-2",
                    FirstUserMessage = "Create a plan in session two.",
                });

            static ChatMessage CreateAssistantMessage(string messageId) => new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        callId: "shared-plan-call",
                        name: "create_plan",
                        arguments: new Dictionary<string, object?> { ["steps"] = s_duplicateAuditPlanSteps }),
                ])
            {
                AuthorName = "Planner",
                MessageId = messageId,
            };

            await host.PersistConversationAsync(
                firstSessionId,
                new ChatMessage(ChatRole.User, "Create a plan in session one.") { AuthorName = "User", MessageId = "cross-session-user-1" },
                CreateAssistantMessage("cross-session-assistant-1"));

            await host.PersistConversationAsync(
                secondSessionId,
                new ChatMessage(ChatRole.User, "Create a plan in session two.") { AuthorName = "User", MessageId = "cross-session-user-2" },
                CreateAssistantMessage("cross-session-assistant-2"));

            ChatSessionWorkspaceDto? secondWorkspace =
                await host.Client.GetFromJsonAsync<ChatSessionWorkspaceDto>($"/api/chat-sessions/{secondSessionId}/workspace");

            Assert.NotNull(secondWorkspace);
            Assert.Single(
                secondWorkspace!.AuditEvents,
                item => item.EventType == ChatAuditEventType.ToolCall.ToString() &&
                    item.FunctionName == "create_plan");
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
            builder.Services.AddScoped<ChatSessionAuditService>();
            builder.Services.AddScoped<ChatSessionWorkspaceService>();
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
            ChatSessionWorkspaceService workspaceService = scope.ServiceProvider.GetRequiredService<ChatSessionWorkspaceService>();
            await workspaceService.RefreshDerivedStateAsync(sessionId);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}
