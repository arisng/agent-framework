using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer.Tests;

public sealed class ToolResultStreamingChatClientTests
{
    [Fact]
    public async Task GetStreamingResponseAsync_EmitsNewTrailingToolResultAfterInnerClientUpdateAsync()
    {
        StubChatClient innerClient = new(
        [
            new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("Done")])
        ]);

        ToolResultUnwrappingChatClient client = CreateVisibleClient(innerClient);
        List<ChatMessage> messages =
        [
            new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent("call_1", "update_plan_step", new Dictionary<string, object?> { ["index"] = 0 })
                ]),
            new(
                ChatRole.Tool,
                [
                    new FunctionResultContent("call_1", new { status = "completed" })
                ])
        ];

        List<ChatResponseUpdate> updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync(messages));

        Assert.Equal(ChatRole.Assistant, updates[0].Role);
        Assert.Contains(updates[0].Contents.OfType<TextContent>(), content => content.Text == "Done");
        Assert.Equal(ChatRole.Tool, updates[1].Role);
        Assert.Contains(updates[1].Contents.OfType<FunctionResultContent>(), content => content.CallId == "call_1");
        Assert.Contains(updates, update => update.Role == ChatRole.Tool &&
            update.Contents.OfType<FunctionResultContent>().Any(content => content.CallId == "call_1"));
        Assert.Contains(updates, update => update.Contents.OfType<TextContent>().Any(content => content.Text == "Done"));
        Assert.All(updates, update => Assert.DoesNotContain(update.Contents, content => content.GetType().Name == "StreamingFunctionResultContent"));
    }

    [Fact]
    public async Task GetStreamingResponseAsync_ReplaysBufferedToolResultAfterFirstAssistantToolCallBoundaryAsync()
    {
        StubChatClient innerClient = new(
        [
            new ChatResponseUpdate(
                ChatRole.Assistant,
                [new FunctionCallContent("call_2", "show_form", new Dictionary<string, object?>())])
            {
                FinishReason = ChatFinishReason.ToolCalls
            },
            new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("Later")])
        ]);

        ToolResultUnwrappingChatClient client = CreateVisibleClient(innerClient);
        List<ChatMessage> messages =
        [
            new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent("call_1", "update_plan_step", new Dictionary<string, object?> { ["index"] = 0 })
                ]),
            new(
                ChatRole.Tool,
                [
                    new FunctionResultContent("call_1", new { status = "completed" })
                ])
        ];

        List<ChatResponseUpdate> updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync(messages));

        Assert.Equal(ChatRole.Assistant, updates[0].Role);
        Assert.Contains(updates[0].Contents.OfType<FunctionCallContent>(), content => content.CallId == "call_2");
        Assert.Equal(ChatRole.Tool, updates[1].Role);
        Assert.Contains(updates[1].Contents.OfType<FunctionResultContent>(), content => content.CallId == "call_1");
    }

    [Fact]
    public async Task GetStreamingResponseAsync_DoesNotReemitToolResultCallIdAlreadySeenEarlierAsync()
    {
        StubChatClient innerClient = new([]);
        ToolResultUnwrappingChatClient client = CreateVisibleClient(innerClient);

        List<ChatMessage> messages =
        [
            new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent("call_1", "update_plan_step", new Dictionary<string, object?> { ["index"] = 0 })
                ]),
            new(
                ChatRole.Tool,
                [
                    new FunctionResultContent("call_1", new { status = "completed" })
                ]),
            new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent("call_2", "show_form", new Dictionary<string, object?> { ["formType"] = "showcase_preferences" })
                ]),
            new(
                ChatRole.Tool,
                [
                    new FunctionResultContent("call_1", new { status = "completed" })
                ])
        ];

        List<ChatResponseUpdate> updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync(messages));

        Assert.DoesNotContain(updates, update => update.Role == ChatRole.Tool &&
            update.Contents.OfType<FunctionResultContent>().Any(content => content.CallId == "call_1"));
    }

    [Fact]
    public async Task FunctionInvokingPipeline_DoesNotLeakStreamedToolResultsIntoRecursiveRequestsAsync()
    {
        RecordingScriptedChatClient innerClient = new(
        [
            [
                new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new FunctionCallContent("call_1", "TestTool", new Dictionary<string, object?>())])
                {
                    FinishReason = ChatFinishReason.ToolCalls
                }
            ],
            [
                new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new FunctionCallContent("call_2", "TestTool", new Dictionary<string, object?>())])
                {
                    FinishReason = ChatFinishReason.ToolCalls
                }
            ],
            [
                new ChatResponseUpdate(ChatRole.Assistant, [new TextContent("Done")])
            ]
        ]);

        ToolResultReplayStore replayStore = new();
        ToolResultUnwrappingChatClient client = new(
            new FunctionInvokingChatClient(
                new ToolResultStreamingChatClient(innerClient, replayStore),
                null,
                null),
            replayStore);

        AIFunction tool = AIFunctionFactory.Create(() => "ok", "TestTool");
        List<ChatResponseUpdate> updates = await CollectUpdatesAsync(
            client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Implement the plan")],
                new ChatOptions { Tools = [tool] }));

        Assert.Equal(3, innerClient.RecordedCalls.Count);
        Assert.Collection(
            innerClient.RecordedCalls,
            firstCall =>
            {
                Assert.Collection(
                    firstCall,
                    message => Assert.Equal(ChatRole.User, message.Role));
            },
            secondCall =>
            {
                Assert.Collection(
                    secondCall,
                    message => Assert.Equal(ChatRole.User, message.Role),
                    message => AssertAssistantCall(message, "call_1", "TestTool"),
                    message => AssertToolResult(message, "call_1"));
            },
            thirdCall =>
            {
                Assert.Collection(
                    thirdCall,
                    message => Assert.Equal(ChatRole.User, message.Role),
                    message => AssertAssistantCall(message, "call_1", "TestTool"),
                    message => AssertToolResult(message, "call_1"),
                    message => AssertAssistantCall(message, "call_2", "TestTool"),
                    message => AssertToolResult(message, "call_2"));
            });

        Assert.All(innerClient.RecordedCalls.Skip(1), AssertToolMessagesFollowMatchingAssistantCall);
        Assert.Contains(updates, update => update.Contents.OfType<FunctionResultContent>().Any(result => result.CallId == "call_1"));
        Assert.Contains(updates, update => update.Contents.OfType<FunctionResultContent>().Any(result => result.CallId == "call_2"));
        Assert.Contains(updates, update => update.Contents.OfType<TextContent>().Any(text => text.Text == "Done"));
        Assert.All(updates, update => Assert.DoesNotContain(update.Contents, content => content.GetType().Name == "StreamingFunctionResultContent"));
    }

    private static ToolResultUnwrappingChatClient CreateVisibleClient(IChatClient innerClient) =>
        CreateVisibleClientCore(innerClient);

    private static ToolResultUnwrappingChatClient CreateVisibleClientCore(IChatClient innerClient)
    {
        ToolResultReplayStore replayStore = new();
        return new ToolResultUnwrappingChatClient(
            new ToolResultStreamingChatClient(innerClient, replayStore),
            replayStore);
    }

    private static async Task<List<ChatResponseUpdate>> CollectUpdatesAsync(IAsyncEnumerable<ChatResponseUpdate> updates)
    {
        List<ChatResponseUpdate> collected = [];
        await foreach (ChatResponseUpdate update in updates)
        {
            collected.Add(update);
        }

        return collected;
    }

    private static void AssertAssistantCall(ChatMessage message, string expectedCallId, string expectedName)
    {
        Assert.Equal(ChatRole.Assistant, message.Role);
        FunctionCallContent call = Assert.Single(message.Contents.OfType<FunctionCallContent>());
        Assert.Equal(expectedCallId, call.CallId);
        Assert.Equal(expectedName, call.Name);
    }

    private static void AssertToolResult(ChatMessage message, string expectedCallId)
    {
        Assert.Equal(ChatRole.Tool, message.Role);
        FunctionResultContent result = Assert.Single(message.Contents.OfType<FunctionResultContent>());
        Assert.Equal(expectedCallId, result.CallId);
    }

    private static void AssertToolMessagesFollowMatchingAssistantCall(IReadOnlyList<ChatMessage> messages)
    {
        for (int i = 0; i < messages.Count; i++)
        {
            if (messages[i].Role != ChatRole.Tool)
            {
                continue;
            }

            Assert.True(i > 0, "Tool messages must have a preceding assistant tool-call message.");
            FunctionResultContent result = Assert.Single(messages[i].Contents.OfType<FunctionResultContent>());
            ChatMessage previousMessage = messages[i - 1];
            Assert.Equal(ChatRole.Assistant, previousMessage.Role);
            Assert.Contains(
                previousMessage.Contents.OfType<FunctionCallContent>(),
                call => string.Equals(call.CallId, result.CallId, StringComparison.Ordinal));
        }
    }

    private sealed class StubChatClient(IEnumerable<ChatResponseUpdate> updates) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse());

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (ChatResponseUpdate update in updates)
            {
                yield return update;
            }

            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingScriptedChatClient(IReadOnlyList<IReadOnlyList<ChatResponseUpdate>> scriptedResponses) : IChatClient
    {
        private int _responseIndex;

        public List<List<ChatMessage>> RecordedCalls { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse());

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            this.RecordedCalls.Add(CloneMessages(messages));
            foreach (ChatResponseUpdate update in scriptedResponses[_responseIndex++])
            {
                yield return update;
            }

            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        private static List<ChatMessage> CloneMessages(IEnumerable<ChatMessage> messages)
        {
            return messages
                .Select(message => new ChatMessage(message.Role, message.Contents.ToList())
                {
                    MessageId = message.MessageId,
                    AuthorName = message.AuthorName,
                    CreatedAt = message.CreatedAt
                })
                .ToList();
        }
    }
}
