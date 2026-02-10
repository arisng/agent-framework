// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

namespace AGUIDojoClient.Store.ArtifactState;

/// <summary>
/// Dispatched when a recipe snapshot is received from the shared state AG-UI endpoint.
/// Replaces the current recipe in the artifact state.
/// </summary>
/// <param name="Recipe">The recipe snapshot to set as the current recipe.</param>
public record SetRecipeAction(Recipe Recipe);

/// <summary>
/// Dispatched when a document state snapshot is received from the predictive state AG-UI endpoint.
/// Replaces the current document state in the artifact state.
/// </summary>
/// <param name="DocumentState">The document state snapshot to set.</param>
public record SetDocumentAction(DocumentState DocumentState);

/// <summary>
/// Dispatched when the user toggles document preview mode on or off.
/// </summary>
/// <param name="IsPreview">Whether the document should be displayed in preview mode.</param>
public record SetDocumentPreviewAction(bool IsPreview);

/// <summary>
/// Dispatched to reset all artifact state (recipe, document, diff, data grid, tabs) to defaults.
/// Typically used when starting a new conversation or switching endpoints.
/// </summary>
public record ClearArtifactsAction;

/// <summary>
/// Dispatched when diff preview data is generated from a state change (Plan, Recipe, or Document transitions).
/// Sets the before/after data for the DiffPreview component in the Canvas Pane.
/// </summary>
/// <param name="Before">The state object before the change.</param>
/// <param name="After">The state object after the change.</param>
/// <param name="Title">A descriptor for this diff (e.g., "Plan State Change").</param>
public record SetDiffPreviewArtifactAction(object? Before, object? After, string Title);

/// <summary>
/// Dispatched when a <c>show_data_grid</c> tool result arrives from the AG-UI stream.
/// Sets the data grid result for display in the Canvas Pane DataGrid tab.
/// </summary>
/// <param name="DataGrid">The parsed <see cref="DataGridResult"/> from the tool result.</param>
public record SetDataGridArtifactAction(DataGridResult DataGrid);

/// <summary>
/// Dispatched when the user switches the active tab in the Canvas Pane.
/// Changes which artifact is currently displayed without discarding data.
/// </summary>
/// <param name="ArtifactType">The artifact type (tab) to activate.</param>
public record SetActiveArtifactAction(ArtifactType ArtifactType);
