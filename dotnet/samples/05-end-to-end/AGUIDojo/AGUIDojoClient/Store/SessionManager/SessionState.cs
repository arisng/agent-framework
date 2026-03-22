// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>Represents before and after values for a plan diff preview.</summary>
public sealed record DiffState(object? Before, object? After, string Title = "State Diff");

/// <summary>
/// Represents a time-limited undo affordance for the latest checkpointed change.
/// </summary>
public sealed record PendingUndoState
{
    /// <summary>Gets the checkpoint identifier that will be restored when the user undoes the change.</summary>
    public required string CheckpointId { get; init; }

    /// <summary>Gets the label of the checkpoint that will be restored.</summary>
    public required string CheckpointLabel { get; init; }

    /// <summary>Gets the user-facing summary of the applied change.</summary>
    public required string Summary { get; init; }

    /// <summary>Gets when the grace period started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets when the grace period expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Enumerates the artifact tabs that can be shown for a session.</summary>
public enum ArtifactType
{
    /// <summary>No artifact is active.</summary>
    None,

    /// <summary>A diff preview artifact is active.</summary>
    DiffPreview,

    /// <summary>A data grid artifact is active.</summary>
    DataGrid,

    /// <summary>A recipe editor artifact is active.</summary>
    RecipeEditor,

    /// <summary>A document preview artifact is active.</summary>
    DocumentPreview,

    /// <summary>An audit trail artifact is active.</summary>
    AuditTrail,
}

/// <summary>
/// Holds all state for a single chat session.
/// </summary>
[SuppressMessage("Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Justification = "Fluxor state naming convention.")]
public sealed record SessionState
{
    /// <summary>Gets the conversation tree backing the session's message history.</summary>
    public ConversationTree Tree { get; init; } = new();

    /// <summary>Gets the active branch messages as a flat list (computed from the tree).</summary>
    public ImmutableList<ChatMessage> Messages => this.Tree.GetActiveBranchMessages().ToImmutableList();

    /// <summary>Gets the in-progress assistant message for the session.</summary>
    public ChatMessage? CurrentResponseMessage { get; init; }

    /// <summary>Gets the AG-UI conversation identifier.</summary>
    public string? ConversationId { get; init; }

    /// <summary>Gets the pending approval request, if any.</summary>
    public PendingApproval? PendingApproval { get; init; }

    /// <summary>Gets a value indicating whether the session is currently streaming.</summary>
    public bool IsRunning { get; init; }

    /// <summary>Gets the current assistant author name.</summary>
    public string? CurrentAuthorName { get; init; }

    /// <summary>Gets the active plan for the session.</summary>
    public Plan? Plan { get; init; }

    /// <summary>Gets the current plan diff preview state.</summary>
    public DiffState? PlanDiff { get; init; }

    /// <summary>Gets the current recipe artifact.</summary>
    public Recipe? CurrentRecipe { get; init; }

    /// <summary>Gets the current document artifact.</summary>
    public DocumentState? CurrentDocumentState { get; init; }

    /// <summary>Gets a value indicating whether the current document is still previewing.</summary>
    public bool IsDocumentPreview { get; init; } = true;

    /// <summary>Gets a value indicating whether the session currently has interactive artifacts.</summary>
    public bool HasInteractiveArtifact { get; init; }

    /// <summary>Gets the currently selected artifact tab.</summary>
    public ArtifactType ActiveArtifactType { get; init; } = ArtifactType.None;

    /// <summary>Gets the diff preview artifact.</summary>
    public DiffPreviewData? DiffPreview { get; init; }

    /// <summary>Gets the current data grid artifact.</summary>
    public DataGridResult? CurrentDataGrid { get; init; }

    /// <summary>Gets the visible artifact tabs.</summary>
    public ImmutableHashSet<ArtifactType> VisibleTabs { get; init; } = ImmutableHashSet<ArtifactType>.Empty;

    /// <summary>Gets the audit trail entries for this session.</summary>
    public ImmutableList<AuditEntry> AuditTrail { get; init; } = ImmutableList<AuditEntry>.Empty;

    /// <summary>Gets the active undo grace period, if any.</summary>
    public PendingUndoState? PendingUndo { get; init; }
}
