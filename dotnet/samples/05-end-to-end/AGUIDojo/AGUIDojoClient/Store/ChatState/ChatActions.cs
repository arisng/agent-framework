// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.ChatState;

/// <summary>
/// Fluxor actions for chat state management.
/// Dispatched from the SSE streaming loop in <c>Chat.razor</c> and from user
/// interactions such as sending messages, approving tools, and resetting conversations.
/// </summary>
public static class ChatActions
{
    /// <summary>
    /// Adds a message to the conversation history.
    /// Dispatched when a user sends a message or when a completed assistant
    /// response is finalized from the SSE stream.
    /// </summary>
    /// <param name="Message">The chat message to add to the conversation.</param>
    public sealed record AddMessageAction(ChatMessage Message);

    /// <summary>
    /// Updates the text of the in-progress assistant response message.
    /// Dispatched periodically during SSE streaming to batch token updates
    /// (e.g., every 100ms or N tokens) to prevent render thrashing.
    /// </summary>
    /// <param name="ResponseMessage">The updated response message with accumulated text.</param>
    public sealed record UpdateResponseMessageAction(ChatMessage? ResponseMessage);

    /// <summary>
    /// Sets the conversation identifier from the AG-UI SSE stream context.
    /// Dispatched when the conversation ID is first received or changes.
    /// </summary>
    /// <param name="ConversationId">The unique conversation identifier.</param>
    public sealed record SetConversationIdAction(string? ConversationId);

    /// <summary>
    /// Sets the pending approval request that awaits user decision.
    /// Dispatched when a function call requires human-in-the-loop approval,
    /// or set to <c>null</c> when the approval is resolved.
    /// </summary>
    /// <param name="PendingApproval">The pending approval request, or <c>null</c> to clear it.</param>
    public sealed record SetPendingApprovalAction(Services.PendingApproval? PendingApproval);

    /// <summary>
    /// Clears all messages, resets the response message, conversation ID,
    /// stateful message count, and pending approval to their initial values.
    /// Dispatched when the user resets the conversation or switches endpoints.
    /// </summary>
    public sealed record ClearMessagesAction;

    /// <summary>
    /// Increments the stateful message count by one.
    /// Dispatched after a message round-trip completes to track which messages
    /// have been sent to the server for stateful context management.
    /// </summary>
    public sealed record IncrementStatefulCountAction;

    /// <summary>
    /// Sets the stateful message count to an explicit value.
    /// Dispatched when the count needs to match the current message list length,
    /// such as after approval rejection or checkpoint restore.
    /// </summary>
    /// <param name="Count">The new stateful message count.</param>
    public sealed record SetStatefulCountAction(int Count);

    /// <summary>
    /// Trims the message list to keep only the first <paramref name="KeepCount"/> messages.
    /// Dispatched when reverting to a checkpoint and discarding messages beyond
    /// the checkpoint boundary.
    /// </summary>
    /// <param name="KeepCount">The number of messages to retain from the beginning of the list.</param>
    public sealed record TrimMessagesAction(int KeepCount);
}
