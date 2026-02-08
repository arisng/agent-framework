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
/// Dispatched to reset all artifact state (recipe, document, preview flag) to defaults.
/// Typically used when starting a new conversation or switching endpoints.
/// </summary>
public record ClearArtifactsAction;
