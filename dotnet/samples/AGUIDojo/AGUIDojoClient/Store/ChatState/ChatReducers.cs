// Copyright (c) Microsoft. All rights reserved.

using Fluxor;

namespace AGUIDojoClient.Store.ChatState;

/// <summary>
/// Pure reducer functions for <see cref="ChatState"/>.
/// Each method takes the current state and an action, returning a new immutable state record.
/// </summary>
/// <remarks>
/// Reducers are discovered by Fluxor via assembly scanning (registered in <c>Program.cs</c>).
/// The <see cref="ReducerMethodAttribute"/> marks static methods as reducer handlers.
/// <para>
/// All reducers produce new immutable state records using the <c>with</c> expression.
/// The <see cref="ChatState.Messages"/> collection uses <see cref="System.Collections.Immutable.ImmutableList{T}"/>
/// for safe concurrent access during SSE streaming.
/// </para>
/// </remarks>
public static class ChatReducers
{
    /// <summary>
    /// Handles <see cref="ChatActions.AddMessageAction"/> by appending a new message
    /// to the conversation history.
    /// </summary>
    /// <param name="state">The current chat state.</param>
    /// <param name="action">The action containing the message to add.</param>
    /// <returns>A new <see cref="ChatState"/> with the message appended to the list.</returns>
    [ReducerMethod]
    public static ChatState OnAddMessage(ChatState state, ChatActions.AddMessageAction action) =>
        state with { Messages = state.Messages.Add(action.Message) };

    /// <summary>
    /// Handles <see cref="ChatActions.UpdateResponseMessageAction"/> by replacing the
    /// in-progress response message with the updated version containing accumulated text.
    /// </summary>
    /// <param name="state">The current chat state.</param>
    /// <param name="action">The action containing the updated response message, or <c>null</c> to clear it.</param>
    /// <returns>A new <see cref="ChatState"/> with the updated response message.</returns>
    [ReducerMethod]
    public static ChatState OnUpdateResponseMessage(ChatState state, ChatActions.UpdateResponseMessageAction action) =>
        state with { CurrentResponseMessage = action.ResponseMessage };

    /// <summary>
    /// Handles <see cref="ChatActions.SetConversationIdAction"/> by updating the conversation identifier.
    /// </summary>
    /// <param name="state">The current chat state.</param>
    /// <param name="action">The action containing the new conversation ID.</param>
    /// <returns>A new <see cref="ChatState"/> with the updated conversation ID.</returns>
    [ReducerMethod]
    public static ChatState OnSetConversationId(ChatState state, ChatActions.SetConversationIdAction action) =>
        state with { ConversationId = action.ConversationId };

    /// <summary>
    /// Handles <see cref="ChatActions.SetPendingApprovalAction"/> by setting or clearing
    /// the pending approval request.
    /// </summary>
    /// <param name="state">The current chat state.</param>
    /// <param name="action">The action containing the pending approval, or <c>null</c> to clear it.</param>
    /// <returns>A new <see cref="ChatState"/> with the updated pending approval.</returns>
    [ReducerMethod]
    public static ChatState OnSetPendingApproval(ChatState state, ChatActions.SetPendingApprovalAction action) =>
        state with { PendingApproval = action.PendingApproval };

    /// <summary>
    /// Handles <see cref="ChatActions.ClearMessagesAction"/> by resetting all chat state
    /// to initial values: empty message list, no response, no conversation ID,
    /// zero stateful count, and no pending approval.
    /// </summary>
    /// <param name="state">The current chat state.</param>
    /// <param name="action">The clear action.</param>
    /// <returns>A new <see cref="ChatState"/> with all fields reset to defaults.</returns>
    [ReducerMethod]
    public static ChatState OnClearMessages(ChatState state, ChatActions.ClearMessagesAction action) =>
        new()
        {
            Messages = System.Collections.Immutable.ImmutableList<Microsoft.Extensions.AI.ChatMessage>.Empty,
            CurrentResponseMessage = null,
            ConversationId = null,
            StatefulMessageCount = 0,
            PendingApproval = null
        };

    /// <summary>
    /// Handles <see cref="ChatActions.IncrementStatefulCountAction"/> by incrementing
    /// the stateful message count by one.
    /// </summary>
    /// <param name="state">The current chat state.</param>
    /// <param name="action">The increment action.</param>
    /// <returns>A new <see cref="ChatState"/> with the stateful count incremented.</returns>
    [ReducerMethod]
    public static ChatState OnIncrementStatefulCount(ChatState state, ChatActions.IncrementStatefulCountAction action) =>
        state with { StatefulMessageCount = state.StatefulMessageCount + 1 };

    /// <summary>
    /// Handles <see cref="ChatActions.SetStatefulCountAction"/> by setting the stateful
    /// message count to the specified value.
    /// </summary>
    /// <param name="state">The current chat state.</param>
    /// <param name="action">The action containing the new count value.</param>
    /// <returns>A new <see cref="ChatState"/> with the stateful count set to the specified value.</returns>
    [ReducerMethod]
    public static ChatState OnSetStatefulCount(ChatState state, ChatActions.SetStatefulCountAction action) =>
        state with { StatefulMessageCount = action.Count };

    /// <summary>
    /// Handles <see cref="ChatActions.TrimMessagesAction"/> by keeping only the first
    /// N messages and discarding the rest. Used during checkpoint restore.
    /// </summary>
    /// <param name="state">The current chat state.</param>
    /// <param name="action">The action containing the number of messages to keep.</param>
    /// <returns>A new <see cref="ChatState"/> with the message list trimmed.</returns>
    [ReducerMethod]
    public static ChatState OnTrimMessages(ChatState state, ChatActions.TrimMessagesAction action) =>
        action.KeepCount >= state.Messages.Count
            ? state
            : state with { Messages = state.Messages.GetRange(0, action.KeepCount) };
}
