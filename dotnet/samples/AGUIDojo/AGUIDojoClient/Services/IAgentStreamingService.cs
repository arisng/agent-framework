// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Service interface for coordinating the AG-UI SSE streaming loop, governance actions,
/// and conversation lifecycle. Encapsulates the core agent response processing logic
/// extracted from <c>Chat.razor</c> to reduce component complexity.
/// </summary>
/// <remarks>
/// This service is designed to be registered as <c>Scoped</c> (per Blazor circuit).
/// It owns all streaming-related state: cancellation tokens, deduplication sets,
/// approval <see cref="TaskCompletionSource{TResult}"/>, diff preview state, and
/// <see cref="ChatOptions"/>.
/// <para>
/// UI callbacks (<c>throttledStateChanged</c>, <c>stateChanged</c>) are provided
/// by the hosting component via <see cref="SetUiCallbacks"/> so the service can
/// trigger Blazor re-renders without taking a dependency on <c>ComponentBase</c>.
/// </para>
/// </remarks>
public interface IAgentStreamingService : IDisposable
{
    /// <summary>
    /// Gets the diff preview "before" state, or <see langword="null"/> if no diff is active.
    /// Used by the <c>DiffPreview</c> governance component in the Canvas Pane.
    /// </summary>
    object? LastDiffBefore { get; }

    /// <summary>
    /// Gets the diff preview "after" state, or <see langword="null"/> if no diff is active.
    /// Used by the <c>DiffPreview</c> governance component in the Canvas Pane.
    /// </summary>
    object? LastDiffAfter { get; }

    /// <summary>
    /// Gets the title for the current diff preview (e.g., "Plan State Change").
    /// </summary>
    string LastDiffTitle { get; }

    /// <summary>
    /// Gets the <see cref="ChatOptions"/> used for configuring streaming requests.
    /// Holds the current <c>ConversationId</c> for the AG-UI protocol.
    /// </summary>
    ChatOptions ChatOptions { get; }

    /// <summary>
    /// Registers the UI callback delegates used by the service to trigger
    /// Blazor component re-renders. Must be called once during component initialization.
    /// </summary>
    /// <param name="throttledStateChanged">
    /// A callback that triggers a throttled (debounced) <c>StateHasChanged</c> on the Blazor component.
    /// Used during high-frequency SSE streaming events.
    /// </param>
    /// <param name="stateChanged">
    /// A callback that triggers an immediate <c>StateHasChanged</c> on the Blazor component.
    /// Used for critical UI updates such as approval dialogs.
    /// </param>
    void SetUiCallbacks(Action throttledStateChanged, Action stateChanged);

    /// <summary>
    /// Processes the agent response by streaming SSE events from the specified
    /// <see cref="IChatClient"/>. Dispatches Fluxor actions for all state mutations.
    /// Handles inline approval requests by dispatching <c>SetPendingApprovalAction</c>
    /// and blocking until <see cref="ResolveApproval"/> is called.
    /// </summary>
    /// <param name="chatClient">The chat client to stream responses from.</param>
    /// <returns>A task that completes when the full response (including approval loops) finishes.</returns>
    Task ProcessAgentResponseAsync(IChatClient chatClient);

    /// <summary>
    /// Resolves a pending approval request with the user's decision.
    /// Called by the UI component when the user approves or rejects a tool call.
    /// </summary>
    /// <param name="approved"><see langword="true"/> if the user approved; <see langword="false"/> if rejected.</param>
    void ResolveApproval(bool approved);

    /// <summary>
    /// Cancels any in-progress response streaming. If a partial response was being
    /// streamed, it is added to the conversation history before cancellation.
    /// Clears the running state and pending approvals.
    /// </summary>
    void CancelAnyCurrentResponse();

    /// <summary>
    /// Resets all conversation state to initial values. Clears messages, plan,
    /// artifacts, checkpoints, function call tracking, and diff preview state.
    /// Re-adds the system prompt message.
    /// </summary>
    /// <param name="systemPrompt">The system prompt message to re-add after clearing.</param>
    /// <param name="selectedEndpointPath">The currently selected endpoint path for state manager initialization.</param>
    void ResetConversation(string systemPrompt, string selectedEndpointPath);

    /// <summary>
    /// Handles the emergency stop (panic) action. Cancels the current response,
    /// reverts to the latest checkpoint, and resets conversation context.
    /// </summary>
    /// <param name="selectedEndpointPath">The currently selected endpoint path.</param>
    void HandlePanic(string selectedEndpointPath);

    /// <summary>
    /// Handles reverting to a specific checkpoint. Cancels the current response,
    /// restores state from the checkpoint, and resets conversation context.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier to revert to.</param>
    /// <param name="selectedEndpointPath">The currently selected endpoint path.</param>
    void HandleCheckpointRevert(string checkpointId, string selectedEndpointPath);

    /// <summary>
    /// Initializes or clears endpoint-specific state (e.g., state manager for shared state endpoint).
    /// </summary>
    /// <param name="endpointPath">The endpoint path to initialize for.</param>
    void InitializeEndpointState(string endpointPath);

    /// <summary>
    /// Builds a dictionary of tool name → invocation count from the <see cref="IObservabilityService"/> steps.
    /// Used by the <c>MemoryInspector</c> component.
    /// </summary>
    /// <returns>A read-only dictionary of tool invocation counts, or <see langword="null"/> if no tools have been invoked.</returns>
    IReadOnlyDictionary<string, int>? GetToolInvocationSummary();
}
