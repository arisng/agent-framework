// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

#pragma warning disable CA1724 // Type name conflicts with namespace — standard Fluxor convention

namespace AGUIDojoClient.Store.ArtifactState;

/// <summary>
/// Represents the Fluxor state for artifact tracking, including recipe (shared state)
/// and document (predictive state) data managed by the AG-UI protocol.
/// </summary>
/// <remarks>
/// This state is populated by <c>SetRecipeAction</c> and <c>SetDocumentAction</c>
/// dispatched from the SSE streaming loop in <c>Chat.razor</c> when the agent sends
/// recipe snapshots or document state updates.
/// </remarks>
/// <param name="CurrentRecipe">The current recipe from the shared state endpoint, or <see langword="null"/> if no recipe is active.</param>
/// <param name="CurrentDocumentState">The current document state from the predictive state endpoint, or <see langword="null"/> if no document is active.</param>
/// <param name="IsDocumentPreview">
/// Whether the document is currently in preview mode. Defaults to <see langword="true"/>
/// when a new document is first received, and can be toggled by the user.
/// </param>
/// <param name="HasInteractiveArtifact">
/// Whether an interactive shared artifact (requiring canvas-pane display) is currently active.
/// <see langword="true"/> when <see cref="CurrentRecipe"/> or <see cref="CurrentDocumentState"/> is not <see langword="null"/>.
/// Used by <c>DualPaneLayout</c> to conditionally render the canvas-pane.
/// </param>
public record ArtifactState(
    Recipe? CurrentRecipe,
    DocumentState? CurrentDocumentState,
    bool IsDocumentPreview,
    bool HasInteractiveArtifact);
