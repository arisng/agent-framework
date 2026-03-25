using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer;

/// <summary>
/// A <see cref="DelegatingChatClient"/> middleware that intercepts <see cref="FunctionResultContent"/>
/// in input messages and emits them to the streaming output.
/// </summary>
/// <remarks>
/// <para>
/// This middleware solves a critical gap in the AG-UI event pipeline:
/// <see cref="FunctionInvokingChatClient"/> invokes tools internally and feeds
/// <see cref="FunctionResultContent"/> back to the LLM, but never emits it to the
/// streaming response. The AG-UI conversion layer
/// (<c>ChatResponseUpdateAGUIExtensions.AsAGUIEventStreamAsync</c>) already supports
/// <see cref="FunctionResultContent"/> → <c>ToolCallResultEvent</c> conversion,
/// but without this middleware, the conversion never fires because the content
/// never appears in the stream.
/// </para>
/// <para>
/// <b>Pipeline placement:</b> This middleware MUST sit between
/// <see cref="FunctionInvokingChatClient"/> and the LLM client, with
/// <see cref="ToolResultUnwrappingChatClient"/> above the function invoker:
/// <code>
/// Consumer ← ToolResultUnwrappingChatClient ← FunctionInvokingChatClient ← ToolResultStreamingChatClient ← LLM
/// </code>
/// </para>
/// <para>
/// <b>Detection heuristic:</b> When <see cref="FunctionInvokingChatClient"/> invokes a tool,
/// it appends an assistant message (with <see cref="FunctionCallContent"/>) followed by
/// tool-role messages (with <see cref="FunctionResultContent"/>) at the END of the message list,
/// then calls the inner client again. This middleware detects trailing tool-role messages
/// preceded by an assistant message and buffers those <see cref="FunctionResultContent"/> items
/// so <see cref="ToolResultUnwrappingChatClient"/> can replay them to the final caller at the
/// next safe yield boundary. On the first call (original messages ending with a user message),
/// no tool results are buffered.
/// </para>
/// </remarks>
public sealed class ToolResultStreamingChatClient : DelegatingChatClient
{
    internal const string ReplayKeyAdditionalPropertyName = "tool_result_replay_key";
    private readonly ToolResultReplayStore _replayStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolResultStreamingChatClient"/> class.
    /// </summary>
    /// <param name="innerClient">The inner <see cref="IChatClient"/> to delegate to (typically the LLM client).</param>
    /// <param name="replayStore">The shared replay store used with <see cref="ToolResultUnwrappingChatClient"/>.</param>
    public ToolResultStreamingChatClient(IChatClient innerClient, ToolResultReplayStore replayStore)
        : base(innerClient)
    {
        this._replayStore = replayStore;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        // Find trailing tool-role messages at the end of the message list.
        // FunctionInvokingChatClient appends:
        //   1. An assistant message with FunctionCallContent(s)
        //   2. One or more tool-role messages with FunctionResultContent
        // These appear at the very END of the messages list.
        int trailingToolStart = messageList.Count;
        for (int i = messageList.Count - 1; i >= 0; i--)
        {
            if (messageList[i].Role == ChatRole.Tool)
            {
                trailingToolStart = i;
            }
            else
            {
                break;
            }
        }

        // Only emit if:
        // 1. There ARE trailing tool-role messages
        // 2. The message immediately before them is an assistant message
        //    (confirming FunctionInvokingChatClient added these, not conversation history)
        if (trailingToolStart < messageList.Count
            && trailingToolStart > 0
            && messageList[trailingToolStart - 1].Role == ChatRole.Assistant)
        {
            HashSet<string> existingToolResultCallIds = CollectExistingToolResultCallIds(messageList, trailingToolStart);

            for (int i = trailingToolStart; i < messageList.Count; i++)
            {
                foreach (var content in messageList[i].Contents)
                {
                    if (content is FunctionResultContent functionResult)
                    {
                        if (!string.IsNullOrWhiteSpace(functionResult.CallId) &&
                            !existingToolResultCallIds.Add(functionResult.CallId))
                        {
                            continue;
                        }

                        ChatResponseUpdate pendingUpdate = new(
                            ChatRole.Tool,
                            [functionResult])
                        {
                            CreatedAt = DateTimeOffset.UtcNow
                        };

                        if (TryGetReplayKey(options, out string? replayKey) && replayKey is not null)
                        {
                            this._replayStore.Enqueue(replayKey, pendingUpdate);
                        }
                        else
                        {
                            yield return pendingUpdate;
                        }
                    }
                }
            }
        }

        // Forward to the inner client (LLM) and yield all its responses
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private static HashSet<string> CollectExistingToolResultCallIds(IList<ChatMessage> messageList, int endExclusive)
    {
        HashSet<string> callIds = new(StringComparer.Ordinal);

        for (int messageIndex = 0; messageIndex < endExclusive; messageIndex++)
        {
            ChatMessage message = messageList[messageIndex];
            foreach (AIContent content in message.Contents)
            {
                if (content is FunctionResultContent functionResult &&
                    !string.IsNullOrWhiteSpace(functionResult.CallId))
                {
                    callIds.Add(functionResult.CallId);
                }
            }
        }

        return callIds;
    }

    private static bool TryGetReplayKey(ChatOptions? options, out string? replayKey)
    {
        replayKey = null;
        if (options?.AdditionalProperties is null ||
            !options.AdditionalProperties.TryGetValue(ReplayKeyAdditionalPropertyName, out object? replayKeyValue))
        {
            return false;
        }

        if (replayKeyValue is string replayKeyString && !string.IsNullOrWhiteSpace(replayKeyString))
        {
            replayKey = replayKeyString;
            return true;
        }

        return false;
    }
}

public sealed class ToolResultReplayStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<ChatResponseUpdate>> _pendingUpdates = new(StringComparer.Ordinal);

    public void Enqueue(string replayKey, ChatResponseUpdate update)
    {
        lock (_lock)
        {
            if (!this._pendingUpdates.TryGetValue(replayKey, out List<ChatResponseUpdate>? pending))
            {
                pending = [];
                this._pendingUpdates[replayKey] = pending;
            }

            pending.Add(update);
        }
    }

    public IReadOnlyList<ChatResponseUpdate> Drain(string replayKey)
    {
        lock (_lock)
        {
            if (!this._pendingUpdates.TryGetValue(replayKey, out List<ChatResponseUpdate>? pending) ||
                pending.Count == 0)
            {
                return [];
            }

            List<ChatResponseUpdate> drained = [.. pending];
            pending.Clear();
            return drained;
        }
    }

    public void Clear(string replayKey)
    {
        lock (_lock)
        {
            this._pendingUpdates.Remove(replayKey);
        }
    }
}
