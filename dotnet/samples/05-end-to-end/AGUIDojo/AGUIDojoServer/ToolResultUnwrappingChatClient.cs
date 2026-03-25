using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer;

/// <summary>
/// Replays tool results buffered by <see cref="ToolResultStreamingChatClient"/> after the
/// enclosing <see cref="FunctionInvokingChatClient"/> has already consumed them for its
/// recursive tool loop and yielded the next safe outward update boundary.
/// </summary>
public sealed class ToolResultUnwrappingChatClient : DelegatingChatClient
{
    private readonly ToolResultReplayStore _replayStore;

    public ToolResultUnwrappingChatClient(IChatClient innerClient, ToolResultReplayStore replayStore)
        : base(innerClient)
    {
        this._replayStore = replayStore;
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return this.GetStreamingResponseAsync(messages, options, cancellationToken)
            .ToChatResponseAsync(cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string replayKey = Guid.NewGuid().ToString("N");
        ChatOptions? innerOptions = options;
        if (options is not null)
        {
            innerOptions = options.Clone();
        }
        else
        {
            innerOptions = new ChatOptions();
        }

        innerOptions.AdditionalProperties ??= [];
        innerOptions.AdditionalProperties[ToolResultStreamingChatClient.ReplayKeyAdditionalPropertyName] = replayKey;

        try
        {
            await foreach (ChatResponseUpdate update in base.GetStreamingResponseAsync(messages, innerOptions, cancellationToken).ConfigureAwait(false))
            {
                yield return update;

                foreach (ChatResponseUpdate bufferedToolResult in this._replayStore.Drain(replayKey))
                {
                    yield return bufferedToolResult;
                }
            }

            foreach (ChatResponseUpdate bufferedToolResult in this._replayStore.Drain(replayKey))
            {
                yield return bufferedToolResult;
            }
        }
        finally
        {
            this._replayStore.Clear(replayKey);
        }
    }
}
