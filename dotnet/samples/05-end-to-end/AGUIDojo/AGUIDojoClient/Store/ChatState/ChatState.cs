// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.ChatState;

/// <summary>
/// Immutable Fluxor state record for chat message management.
/// Holds the conversation messages, in-progress response, conversation identity,
/// stateful message tracking, and pending approval state.
/// </summary>
/// <remarks>
/// This state is managed via Fluxor actions and reducers. Components read from
/// <c>IState&lt;ChatState&gt;</c> instead of local fields. The streaming loop in
/// <c>Chat.razor</c> dispatches actions such as <see cref="ChatActions.AddMessageAction"/>
/// and <see cref="ChatActions.UpdateResponseMessageAction"/> as SSE events arrive.
/// <para>
/// Uses a flat <see cref="ImmutableList{T}"/> for message storage rather than a
/// DAG structure — DAG-based conversation branching is deferred to a future iteration.
/// </para>
/// <para>
/// Chat.razor should NOT inherit from <c>FluxorComponent</c>. Instead, it uses manual
/// <c>IState&lt;ChatState&gt;.StateChanged</c> subscriptions with
/// <c>ThrottledStateHasChanged()</c> for rendering control during high-frequency
/// SSE streaming events.
/// </para>
/// The feature is registered via <see cref="ChatFeature"/> which provides the initial state.
/// </remarks>
[SuppressMessage("Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Justification = "Fluxor convention: state record matches folder/namespace name.")]
public sealed record ChatState
{
    /// <summary>
    /// The conversation messages, stored as an immutable list for thread safety.
    /// Includes system, user, and assistant messages in chronological order.
    /// </summary>
    public ImmutableList<ChatMessage> Messages { get; init; } = ImmutableList<ChatMessage>.Empty;

    /// <summary>
    /// The assistant message currently being streamed, or <c>null</c> if no response is in progress.
    /// This message accumulates text tokens as they arrive from the SSE stream.
    /// </summary>
    public ChatMessage? CurrentResponseMessage { get; init; }

    /// <summary>
    /// The unique identifier for the current conversation session, or <c>null</c> if not yet assigned.
    /// Populated from the AG-UI SSE stream's conversation context.
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// The count of messages that have been sent to the server with stateful context.
    /// Used to skip already-sent messages when resuming streaming via
    /// <c>messages.Skip(StatefulMessageCount)</c>.
    /// </summary>
    public int StatefulMessageCount { get; init; }

    /// <summary>
    /// The current pending approval request awaiting user decision, or <c>null</c> if none.
    /// Set when a function call requires human-in-the-loop approval.
    /// </summary>
    public Services.PendingApproval? PendingApproval { get; init; }
}
