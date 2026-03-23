using AGUIDojoClient.Models;
using AGUIDojoClient.Helpers;
using AGUIDojoClient.Services;
using Microsoft.Extensions.AI;
using Microsoft.JSInterop;

namespace AGUIDojoClient.Tests.Services;

public sealed class SessionPersistenceServiceTests
{
    [Fact]
    public async Task SaveMetadataAsync_AndLoadMetadataAsync_RoundTripMetadata()
    {
        FakeJsRuntime jsRuntime = new();
        string? storedMetadata = null;
        jsRuntime.SetHandler("sessionPersistence.saveMetadata", args =>
        {
            storedMetadata = Assert.IsType<string>(args[0]);
            return null;
        });
        jsRuntime.SetHandler("sessionPersistence.loadMetadata", _ => storedMetadata);
        SessionPersistenceService service = new(jsRuntime);
        List<SessionMetadataDto> metadata =
        [
            new("session-1", "First", "/chat", "Running", 10, 20, "thread_1", "server-1"),
            new("session-2", "Second", "/chat", "Completed", 30, 40, "thread_2", null)
        ];

        await service.SaveMetadataAsync(metadata);
        List<SessionMetadataDto>? loaded = await service.LoadMetadataAsync();

        Assert.NotNull(storedMetadata);
        Assert.NotNull(loaded);
        Assert.Equal(metadata, loaded);
    }

    [Fact]
    public async Task SaveConversationAsync_AndLoadConversationAsync_PreserveConversationTree()
    {
        FakeJsRuntime jsRuntime = new();
        string? storedConversation = null;
        jsRuntime.SetHandler("sessionPersistence.saveConversation", args =>
        {
            Assert.Equal("session-1", args[0]);
            storedConversation = Assert.IsType<string>(args[1]);
            return null;
        });
        jsRuntime.SetHandler("sessionPersistence.loadConversation", args =>
        {
            Assert.Equal("session-1", args[0]);
            return storedConversation;
        });
        SessionPersistenceService service = new(jsRuntime);

        ConversationTree tree = new();
        tree = tree.AddMessage(new ChatMessage(ChatRole.User, "Root") { AuthorName = "User" });
        string rootId = Assert.IsType<string>(tree.RootId);
        tree = tree.AddMessage(new ChatMessage(ChatRole.Assistant, "Original") { AuthorName = "Assistant" });
        ChatMessage branchMessage = new(ChatRole.Assistant, "Branch")
        {
            AuthorName = "Assistant",
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [ConfidenceMarkers.ConfidenceScoreKey] = 0.61d,
            }
        };
        tree = tree.BranchAt(rootId, branchMessage);

        await service.SaveConversationAsync("session-1", tree);
        ConversationTree? loaded = await service.LoadConversationAsync("session-1");

        Assert.NotNull(loaded);
        Assert.Equal(tree.RootId, loaded.RootId);
        Assert.Equal(tree.ActiveLeafId, loaded.ActiveLeafId);
        Assert.Equal(tree.GetActiveBranchMessages().Select(message => message.Text), loaded.GetActiveBranchMessages().Select(message => message.Text));
        Assert.Equal(tree.GetActiveBranchMessages().Select(message => message.AuthorName), loaded.GetActiveBranchMessages().Select(message => message.AuthorName));
        Assert.Equal(tree.Nodes.Keys.Order(), loaded.Nodes.Keys.Order());

        ChatMessage loadedAssistant = Assert.Single(loaded.GetActiveBranchMessages(), message => message.Role == ChatRole.Assistant);
        Assert.True(ConfidenceMarkers.TryGetConfidenceScore(loadedAssistant.AdditionalProperties, out double confidenceScore));
        Assert.Equal(0.61d, confidenceScore, 3);
    }

    [Fact]
    public async Task LoadConversationAsync_WithInvalidJson_ReturnsNull()
    {
        FakeJsRuntime jsRuntime = new();
        jsRuntime.SetHandler("sessionPersistence.loadConversation", _ => "not json");
        SessionPersistenceService service = new(jsRuntime);

        ConversationTree? loaded = await service.LoadConversationAsync("session-1");

        Assert.Null(loaded);
    }

    [Fact]
    public void SessionMetadataDto_ToMetadata_BackfillsMissingThreadId()
    {
        SessionMetadataDto dto = new("session-1", "First", "/chat", "Completed", 10, 20);

        SessionMetadata metadata = dto.ToMetadata();

        Assert.False(string.IsNullOrWhiteSpace(metadata.AguiThreadId));
        Assert.Null(metadata.ServerSessionId);
    }

    private sealed class FakeJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, Func<object?[], object?>> _handlers = new(StringComparer.Ordinal);

        public void SetHandler(string identifier, Func<object?[], object?> handler) => _handlers[identifier] = handler;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (_handlers.TryGetValue(identifier, out Func<object?[], object?>? handler))
            {
                object? result = handler(args ?? []);
                return new ValueTask<TValue>(result is null ? default! : (TValue)result);
            }

            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
