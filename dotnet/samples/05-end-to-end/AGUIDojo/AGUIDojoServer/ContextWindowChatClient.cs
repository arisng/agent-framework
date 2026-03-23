using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer;

/// <summary>
/// A <see cref="DelegatingChatClient"/> that trims older messages to keep conversations
/// within the LLM's context window budget.
/// </summary>
/// <remarks>
/// <para>
/// In the AG-UI protocol the client sends the full conversation history with every request.
/// For long conversations this can exceed the model's token limit. This middleware keeps
/// all system-role messages and retains only the most recent non-system messages, trimming
/// from the oldest end while preserving tool-call / tool-result pairing integrity.
/// </para>
/// <para>
/// <b>Pipeline placement:</b> This middleware wraps the innermost client so trimming
/// happens before any other middleware (including <see cref="ToolResultStreamingChatClient"/>):
/// <code>
/// Agent ← FunctionInvokingChatClient ← ToolResultStreamingChatClient ← ContextWindowChatClient ← LLM
/// </code>
/// </para>
/// </remarks>
public sealed class ContextWindowChatClient : DelegatingChatClient
{
    private readonly int _maxNonSystemMessages;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextWindowChatClient"/> class.
    /// </summary>
    /// <param name="innerClient">The inner <see cref="IChatClient"/> to delegate to.</param>
    /// <param name="maxNonSystemMessages">
    /// Maximum number of non-system messages to retain. Older messages beyond this limit
    /// are dropped. Defaults to 80 which comfortably fits within 128 K token models.
    /// </param>
    public ContextWindowChatClient(IChatClient innerClient, int maxNonSystemMessages = 80)
        : base(innerClient)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxNonSystemMessages, 2);
        _maxNonSystemMessages = maxNonSystemMessages;
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await base.GetResponseAsync(
            TrimMessages(messages),
            options,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(
            TrimMessages(messages), options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Trims the message list to stay within the configured budget.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    ///   <item>Separates system messages (always kept) from conversation messages.</item>
    ///   <item>If conversation messages exceed the limit, keeps the last N.</item>
    ///   <item>Drops any orphaned leading tool-role messages (tool results whose
    ///         corresponding assistant function-call message was trimmed).</item>
    /// </list>
    /// </remarks>
    private List<ChatMessage> TrimMessages(IEnumerable<ChatMessage> messages)
    {
        var all = messages as IList<ChatMessage> ?? messages.ToList();

        var systemMessages = new List<ChatMessage>();
        var conversationMessages = new List<ChatMessage>();

        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Role == ChatRole.System)
            {
                systemMessages.Add(all[i]);
            }
            else
            {
                conversationMessages.Add(all[i]);
            }
        }

        if (conversationMessages.Count <= _maxNonSystemMessages)
        {
            // No trimming needed — return original list unchanged.
            return all as List<ChatMessage> ?? [.. all];
        }

        // Keep the most recent messages.
        var recent = conversationMessages
            .Skip(conversationMessages.Count - _maxNonSystemMessages)
            .ToList();

        // Drop orphaned leading tool-role messages whose assistant function-call
        // was trimmed away. Also drop a leading assistant message that only contains
        // FunctionCallContent (its tool results may have been trimmed).
        int startIndex = 0;
        while (startIndex < recent.Count)
        {
            var msg = recent[startIndex];
            if (msg.Role == ChatRole.Tool)
            {
                // Orphaned tool result — its function call was trimmed.
                startIndex++;
                continue;
            }

            if (msg.Role == ChatRole.Assistant
                && msg.Contents.Count > 0
                && msg.Contents.All(c => c is FunctionCallContent))
            {
                // Assistant message with only function calls — tool results may be
                // missing if they were trimmed. Drop to avoid confusing the LLM.
                startIndex++;
                continue;
            }

            break;
        }

        if (startIndex > 0)
        {
            recent = recent.GetRange(startIndex, recent.Count - startIndex);
        }

        var result = new List<ChatMessage>(systemMessages.Count + recent.Count);
        result.AddRange(systemMessages);
        result.AddRange(recent);
        return result;
    }
}
