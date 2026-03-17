// Copyright (c) Microsoft. All rights reserved.

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
/// <see cref="FunctionInvokingChatClient"/> and the LLM client:
/// <code>
/// Consumer ← FunctionInvokingChatClient ← ToolResultStreamingChatClient ← LLM
/// </code>
/// </para>
/// <para>
/// <b>Detection heuristic:</b> When <see cref="FunctionInvokingChatClient"/> invokes a tool,
/// it appends an assistant message (with <see cref="FunctionCallContent"/>) followed by
/// tool-role messages (with <see cref="FunctionResultContent"/>) at the END of the message list,
/// then calls the inner client again. This middleware detects trailing tool-role messages
/// preceded by an assistant message and emits those <see cref="FunctionResultContent"/> items
/// before forwarding the call to the LLM. On the first call (original messages ending with
/// a user message), no tool results are emitted.
/// </para>
/// </remarks>
public sealed class ToolResultStreamingChatClient : DelegatingChatClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ToolResultStreamingChatClient"/> class.
    /// </summary>
    /// <param name="innerClient">The inner <see cref="IChatClient"/> to delegate to (typically the LLM client).</param>
    public ToolResultStreamingChatClient(IChatClient innerClient)
        : base(innerClient)
    {
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
            for (int i = trailingToolStart; i < messageList.Count; i++)
            {
                foreach (var content in messageList[i].Contents)
                {
                    if (content is FunctionResultContent functionResult)
                    {
                        yield return new ChatResponseUpdate(
                            ChatRole.Tool,
                            [functionResult])
                        {
                            CreatedAt = DateTimeOffset.UtcNow
                        };
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
}
