using AGUIDojoClient.Models;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Service interface for coordinating AG-UI SSE streaming, governance actions,
/// and session-scoped conversation lifecycle management.
/// </summary>
public interface IAgentStreamingService : IDisposable
{
    /// <summary>
    /// Raised when the notification collection changes.
    /// </summary>
    event EventHandler? NotificationsChanged;

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
    /// Gets the current session notifications in FIFO order.
    /// </summary>
    IReadOnlyList<SessionNotification> Notifications { get; }

    /// <summary>
    /// Registers the UI callbacks used for re-rendering and background-to-circuit marshalling.
    /// </summary>
    /// <param name="throttledStateChanged">Triggers a throttled render update.</param>
    /// <param name="stateChanged">Triggers an immediate render update.</param>
    /// <param name="invokeAsync">Marshals work onto the Blazor circuit synchronization context.</param>
    void SetUiCallbacks(Action throttledStateChanged, Action stateChanged, Func<Func<Task>, Task> invokeAsync);

    /// <summary>
    /// Gets a value indicating whether the specified session can start or queue another response.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns><see langword="true"/> when a response can start or be queued; otherwise, <see langword="false"/>.</returns>
    bool CanQueueResponse(string sessionId);

    /// <summary>
    /// Processes the next agent response for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>A task that completes when the response finishes if it starts immediately.</returns>
    Task ProcessAgentResponseAsync(string sessionId);

    /// <summary>
    /// Resolves a pending approval request for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="approved"><see langword="true"/> to approve; otherwise, <see langword="false"/>.</param>
    void ResolveApproval(string sessionId, bool approved);

    /// <summary>
    /// Cancels the in-flight or queued response for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    void CancelResponse(string sessionId);

    /// <summary>
    /// Resets the specified session back to its initial conversation state.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="systemPrompt">The system prompt message to re-add after clearing.</param>
    void ResetConversation(string sessionId, string systemPrompt);

    /// <summary>
    /// Handles the emergency stop action for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    void HandlePanic(string sessionId);

    /// <summary>
    /// Reverts the specified session to a checkpoint.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="checkpointId">The checkpoint identifier to revert to.</param>
    void HandleCheckpointRevert(string sessionId, string checkpointId);

    /// <summary>
    /// Synchronizes session-scoped client artifacts for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    void SyncSessionState(string sessionId);

    /// <summary>
    /// Builds a dictionary of tool name → invocation count from the <see cref="IObservabilityService"/> steps.
    /// Used by the <c>MemoryInspector</c> component.
    /// </summary>
    /// <returns>A read-only dictionary of tool invocation counts, or <see langword="null"/> if no tools have been invoked.</returns>
    IReadOnlyDictionary<string, int>? GetToolInvocationSummary();

    /// <summary>
    /// Dismisses the specified notification.
    /// </summary>
    /// <param name="notificationId">The notification identifier.</param>
    void DismissNotification(string notificationId);

    /// <summary>
    /// Gets the current SSE stream performance snapshot for the active session.
    /// Returns <see langword="null"/> when no stream is active or has completed.
    /// </summary>
    SseStreamSnapshot? CurrentStreamMetrics { get; }
}
