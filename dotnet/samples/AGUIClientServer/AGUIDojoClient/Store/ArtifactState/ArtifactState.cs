// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Immutable;
using AGUIDojoClient.Models;

#pragma warning disable CA1724 // Type name conflicts with namespace — standard Fluxor convention

namespace AGUIDojoClient.Store.ArtifactState;

/// <summary>
/// Represents the Fluxor state for artifact tracking, including recipe (shared state),
/// document (predictive state), diff preview, and data grid artifacts managed by the AG-UI protocol.
/// </summary>
/// <remarks>
/// This state is populated by various <c>Set*Action</c> dispatches from the SSE streaming loop
/// in <see cref="Services.AgentStreamingService"/> when the agent produces artifact data.
/// The <see cref="ActiveArtifactType"/> tracks which tab is selected in the Canvas Pane,
/// and <see cref="VisibleTabs"/> determines which tabs have content to display.
/// </remarks>
/// <param name="CurrentRecipe">The current recipe from the shared state endpoint, or <see langword="null"/> if no recipe is active.</param>
/// <param name="CurrentDocumentState">The current document state from the predictive state endpoint, or <see langword="null"/> if no document is active.</param>
/// <param name="IsDocumentPreview">
/// Whether the document is currently in preview mode. Defaults to <see langword="true"/>
/// when a new document is first received, and can be toggled by the user.
/// </param>
/// <param name="HasInteractiveArtifact">
/// Whether an interactive shared artifact (requiring canvas-pane display) is currently active.
/// <see langword="true"/> when any artifact (recipe, document, diff, or data grid) is not <see langword="null"/>.
/// Used by <c>DualPaneLayout</c> to conditionally render the canvas-pane.
/// </param>
/// <param name="ActiveArtifactType">The currently selected artifact tab in the Canvas Pane.</param>
/// <param name="DiffPreview">The current diff preview data (before/after state comparison), or <see langword="null"/> if no diff is active.</param>
/// <param name="CurrentDataGrid">The current data grid result from the <c>show_data_grid</c> tool, or <see langword="null"/> if no data grid is active.</param>
/// <param name="VisibleTabs">The set of artifact types that have content and should appear as tabs in the Canvas Pane.</param>
public record ArtifactState(
    Recipe? CurrentRecipe,
    DocumentState? CurrentDocumentState,
    bool IsDocumentPreview,
    bool HasInteractiveArtifact,
    ArtifactType ActiveArtifactType,
    DiffPreviewData? DiffPreview,
    DataGridResult? CurrentDataGrid,
    ImmutableHashSet<ArtifactType> VisibleTabs);
