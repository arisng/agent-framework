// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Fluxor actions for session-scoped chat state.
/// </summary>
public static class SessionActions
{
    public sealed record CreateSessionAction(string SessionId, string? Title = null, string EndpointPath = SessionMetadata.DefaultEndpointPath, bool MakeActive = true, DateTimeOffset? CreatedAt = null);

    public sealed record SetActiveSessionAction(string SessionId);

    public sealed record ArchiveSessionAction(string SessionId);

    public sealed record SetSessionTitleAction(string SessionId, string Title);

    public sealed record SetEndpointAction(string SessionId, string EndpointPath);

    public sealed record SetSessionStatusAction(string SessionId, SessionStatus Status);

    public sealed record AddMessageAction(string SessionId, ChatMessage Message, DateTimeOffset? OccurredAt = null);

    public sealed record UpdateResponseMessageAction(string SessionId, ChatMessage? ResponseMessage);

    public sealed record SetConversationIdAction(string SessionId, string? ConversationId);

    public sealed record SetPendingApprovalAction(string SessionId, PendingApproval? PendingApproval);

    public sealed record StartUndoGracePeriodAction(
        string SessionId,
        string CheckpointId,
        string CheckpointLabel,
        string Summary,
        DateTimeOffset StartedAt,
        DateTimeOffset ExpiresAt);

    public sealed record ClearUndoGracePeriodAction(string SessionId, string? CheckpointId = null);

    public sealed record ClearMessagesAction(string SessionId, DateTimeOffset? OccurredAt = null);

    public sealed record TrimMessagesAction(string SessionId, int KeepCount);

    public sealed record SetRunningAction(string SessionId, bool IsRunning);

    public sealed record SetAuthorNameAction(string SessionId, string? AuthorName);

    public sealed record SetPlanAction(string SessionId, Plan Plan);

    public sealed record ApplyPlanDeltaAction(string SessionId, IEnumerable<JsonPatchOperation> Operations);

    public sealed record ClearPlanAction(string SessionId);

    public sealed record SetPlanDiffAction(string SessionId, DiffState? Diff);

    public sealed record SetRecipeAction(string SessionId, Recipe Recipe);

    public sealed record SetDocumentAction(string SessionId, DocumentState DocumentState);

    public sealed record SetDocumentPreviewAction(string SessionId, bool IsPreview);

    public sealed record ClearArtifactsAction(string SessionId);

    public sealed record SetDiffPreviewArtifactAction(string SessionId, object? Before, object? After, string Title);

    public sealed record SetDataGridArtifactAction(string SessionId, DataGridResult DataGrid);

    public sealed record SetActiveArtifactAction(string SessionId, ArtifactType ArtifactType);

    public sealed record EditAndRegenerateAction(string SessionId, int MessageIndex, string NewText);

    public sealed record SwitchBranchAction(string SessionId, string NodeId, string TargetSiblingId);

    /// <summary>
    /// Triggers hydration of session state from browser storage.
    /// Dispatched once from <c>Chat.razor.OnAfterRenderAsync(firstRender: true)</c>.
    /// </summary>
    public sealed record HydrateFromStorageAction;

    /// <summary>
    /// Hydrates session state from browser persistence on app start.
    /// Replaces the default initial state with previously persisted sessions.
    /// </summary>
    public sealed record HydrateSessionsAction(
        IReadOnlyDictionary<string, SessionEntry> Sessions,
        string? ActiveSessionId);

    public sealed record SetAutonomyLevelAction(AutonomyLevel Level);

    public sealed record AddAuditEntryAction(string SessionId, AuditEntry Entry);
}
