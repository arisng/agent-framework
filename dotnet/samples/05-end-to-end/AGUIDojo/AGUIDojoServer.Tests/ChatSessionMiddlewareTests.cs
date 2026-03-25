using System.Text;
using System.Text.Json;
using AGUIDojoServer.ChatSessions;
using AGUIDojoServer.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AGUIDojoServer.Tests;

public sealed class ChatSessionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_PopulatesRoutingContextFromPersistedSessionMetadata()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            ChatSessionMiddleware middleware = new(_ => Task.CompletedTask);

            using (IServiceScope firstScope = provider.CreateScope())
            {
                DefaultHttpContext firstContext = CreateChatContext(
                    firstScope.ServiceProvider,
                    new
                    {
                        threadId = "thread-routing-seam",
                        messages = new[]
                        {
                            new { role = "user", content = "Investigate the ownership seam." },
                        },
                        forwardedProps = new
                        {
                            subjectModule = "Todo",
                            subjectEntityType = "TodoItem",
                            subjectEntityId = "todo-42",
                            ownerId = "alice",
                            tenantId = "tenant-red",
                            workflowInstanceId = "workflow-42",
                            runtimeInstanceId = "runtime-42",
                            preferredModelId = "gpt-4.1",
                        },
                    });

                await middleware.InvokeAsync(firstContext);

                Assert.True(ChatSessionHttpContextItems.TryGetRoutingContext(firstContext, out ChatSessionRoutingContext? firstRoutingContext));
                Assert.NotNull(firstRoutingContext);
                Assert.Equal(firstRoutingContext.SessionId, firstContext.Response.Headers["X-Session-Id"].ToString());
                Assert.Equal(ChatSessionProtocolVersions.Current, firstRoutingContext.ServerProtocolVersion);
                Assert.Equal("Todo", firstRoutingContext.SubjectModule);
                Assert.Equal("TodoItem", firstRoutingContext.SubjectEntityType);
                Assert.Equal("todo-42", firstRoutingContext.SubjectEntityId);
                Assert.Equal("alice", firstRoutingContext.OwnerId);
                Assert.Equal("tenant-red", firstRoutingContext.TenantId);
                Assert.Equal("workflow-42", firstRoutingContext.WorkflowInstanceId);
                Assert.Equal("runtime-42", firstRoutingContext.RuntimeInstanceId);
                Assert.Equal("thread-routing-seam", firstRoutingContext.AguiThreadId);
                Assert.Equal("gpt-4.1", firstRoutingContext.PreferredModelId);
            }

            using (IServiceScope secondScope = provider.CreateScope())
            {
                DefaultHttpContext secondContext = CreateChatContext(
                    secondScope.ServiceProvider,
                    new
                    {
                        threadId = "thread-routing-seam",
                        messages = new[]
                        {
                            new { role = "user", content = "Continue the same work." },
                        },
                    });

                await middleware.InvokeAsync(secondContext);

                Assert.True(ChatSessionHttpContextItems.TryGetRoutingContext(secondContext, out ChatSessionRoutingContext? secondRoutingContext));
                Assert.NotNull(secondRoutingContext);
                Assert.Equal("Todo", secondRoutingContext.SubjectModule);
                Assert.Equal("TodoItem", secondRoutingContext.SubjectEntityType);
                Assert.Equal("todo-42", secondRoutingContext.SubjectEntityId);
                Assert.Equal("alice", secondRoutingContext.OwnerId);
                Assert.Equal("tenant-red", secondRoutingContext.TenantId);
                Assert.Equal("workflow-42", secondRoutingContext.WorkflowInstanceId);
                Assert.Equal("runtime-42", secondRoutingContext.RuntimeInstanceId);
                Assert.Equal("thread-routing-seam", secondRoutingContext.AguiThreadId);
                Assert.Equal("gpt-4.1", secondRoutingContext.PreferredModelId);
                Assert.Equal(secondRoutingContext.SessionId, secondContext.Response.Headers["X-Session-Id"].ToString());
                Assert.Equal(ChatSessionProtocolVersions.Current, secondContext.Response.Headers["X-Chat-Session-Protocol"].ToString());
                Assert.True(ChatSessionHttpContextItems.TryGetSessionId(secondContext, out string? sessionId));
                Assert.Equal(secondRoutingContext.SessionId, sessionId);
            }
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task InvokeAsync_AssignsSimulatedOwnerAndTenantWhenNoExplicitContextExists()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            ChatSessionMiddleware middleware = new(_ => Task.CompletedTask);

            using IServiceScope scope = provider.CreateScope();
            DefaultHttpContext context = CreateChatContext(
                scope.ServiceProvider,
                new
                {
                    threadId = "thread-routing-defaults",
                    messages = new[]
                    {
                        new { role = "user", content = "Seed a fake owner and tenant." },
                    },
                });

            await middleware.InvokeAsync(context);

            Assert.True(ChatSessionHttpContextItems.TryGetRoutingContext(context, out ChatSessionRoutingContext? routingContext));
            Assert.NotNull(routingContext);
            Assert.Equal(ChatSessionOwnershipDefaults.OwnerId, routingContext.OwnerId);
            Assert.Equal(ChatSessionOwnershipDefaults.TenantId, routingContext.TenantId);
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
        return services.BuildServiceProvider();
    }

    private static async Task InitializeDatabaseAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        ChatSessionsDbContext db = scope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>();
        await ChatSessionsDatabaseInitializer.InitializeAsync(db);
    }

    private static DefaultHttpContext CreateChatContext(IServiceProvider services, object payload)
    {
        DefaultHttpContext context = new()
        {
            RequestServices = services,
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/chat";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        return context;
    }

    private static string CreateDatabasePath()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, ".testdata");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"aguidojo-chat-middleware-{Guid.NewGuid():N}.db");
    }
}
