// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Holds the mutable streaming state for a single chat session.
/// </summary>
public sealed class SessionStreamingContext : IDisposable
{
    /// <summary>
    /// Gets or sets the cancellation token source for the in-flight response.
    /// </summary>
    public CancellationTokenSource? ResponseCancellation { get; set; }

    /// <summary>
    /// Gets the function call identifiers already emitted for the active conversation.
    /// </summary>
    public HashSet<string> SeenFunctionCallIds { get; } = [];

    /// <summary>
    /// Gets the function result identifiers already emitted for the active conversation.
    /// </summary>
    public HashSet<string> SeenFunctionResultCallIds { get; } = [];

    /// <summary>
    /// Gets the mapping of tool call identifier to tool name.
    /// </summary>
    public Dictionary<string, string> FunctionCallIdToToolName { get; } = [];

    /// <summary>
    /// Gets the chat options used for this session's AG-UI stream.
    /// </summary>
    public ChatOptions ChatOptions { get; } = new();

    /// <summary>
    /// Gets or sets the AG-UI thread ID for cross-turn continuity.
    /// AGUIChatClient clears ConversationId on every response (full-history protocol),
    /// so this field preserves the thread identity across turns.
    /// </summary>
    public string? AguiThreadId { get; set; }

    /// <summary>
    /// Gets or sets the pending approval task source for the active stream.
    /// </summary>
    public TaskCompletionSource<bool>? ApprovalTaskSource { get; set; }

    /// <summary>
    /// Gets or sets the currently streaming assistant message.
    /// </summary>
    public ChatMessage? StreamingMessage { get; set; }

    /// <summary>
    /// Gets or sets the last diff "before" snapshot.
    /// </summary>
    public object? LastDiffBefore { get; set; }

    /// <summary>
    /// Gets or sets the last diff "after" snapshot.
    /// </summary>
    public object? LastDiffAfter { get; set; }

    /// <summary>
    /// Gets or sets the last diff title.
    /// </summary>
    public string LastDiffTitle { get; set; } = "State Diff";

    /// <summary>
    /// Gets or sets the active response task for the session.
    /// </summary>
    public Task? ActiveResponseTask { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the session has a queued response.
    /// </summary>
    public bool IsQueued { get; set; }

    /// <summary>
    /// Cancels and clears the current response resources without resetting conversation state.
    /// </summary>
    public void CancelCurrentResponse()
    {
        this.ResponseCancellation?.Cancel();
        this.ResponseCancellation?.Dispose();
        this.ResponseCancellation = null;

        this.ApprovalTaskSource?.TrySetCanceled();
        this.ApprovalTaskSource = null;
        this.StreamingMessage = null;
        this.ActiveResponseTask = null;
        this.IsQueued = false;
    }

    /// <summary>
    /// Resets all per-session conversation tracking state.
    /// </summary>
    public void ResetConversationState()
    {
        this.CancelCurrentResponse();
        this.ChatOptions.ConversationId = null;
        this.AguiThreadId = null;
        this.SeenFunctionCallIds.Clear();
        this.SeenFunctionResultCallIds.Clear();
        this.FunctionCallIdToToolName.Clear();
        this.LastDiffBefore = null;
        this.LastDiffAfter = null;
        this.LastDiffTitle = "State Diff";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.ResetConversationState();
    }
}
