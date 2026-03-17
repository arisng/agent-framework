// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>Represents before and after values for a plan diff preview.</summary>
public sealed record DiffState(object? Before, object? After, string Title = "State Diff");

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
}

/// <summary>
/// Holds all state for a single chat session.
/// </summary>
[SuppressMessage("Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Justification = "Fluxor state naming convention.")]
public sealed record SessionState
{
    /// <summary>Gets the chat messages for the session.</summary>
    public ImmutableList<ChatMessage> Messages { get; init; } = ImmutableList<ChatMessage>.Empty;

    /// <summary>Gets the in-progress assistant message for the session.</summary>
    public ChatMessage? CurrentResponseMessage { get; init; }

    /// <summary>Gets the AG-UI conversation identifier.</summary>
    public string? ConversationId { get; init; }

    /// <summary>Gets the count of messages already sent with stateful context.</summary>
    public int StatefulMessageCount { get; init; }

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
}
