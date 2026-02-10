// Copyright (c) Microsoft. All rights reserved.

using Fluxor;

namespace AGUIDojoClient.Store.ArtifactState;

/// <summary>
/// Contains pure reducer methods for <see cref="ArtifactState"/> transitions.
/// Each method produces a new immutable state record in response to a dispatched action.
/// </summary>
public static class ArtifactReducers
{
    /// <summary>
    /// Handles <see cref="SetRecipeAction"/> by replacing the current recipe in state.
    /// Also sets <see cref="ArtifactState.HasInteractiveArtifact"/> to <see langword="true"/> since recipe is an interactive shared artifact.
    /// </summary>
    /// <param name="state">The current artifact state.</param>
    /// <param name="action">The action containing the new recipe snapshot.</param>
    /// <returns>A new <see cref="ArtifactState"/> with the updated recipe.</returns>
    [ReducerMethod]
    public static ArtifactState ReduceSetRecipeAction(ArtifactState state, SetRecipeAction action) =>
        state with { CurrentRecipe = action.Recipe, HasInteractiveArtifact = true };

    /// <summary>
    /// Handles <see cref="SetDocumentAction"/> by replacing the current document state.
    /// Also sets <see cref="ArtifactState.HasInteractiveArtifact"/> to <see langword="true"/> since document is an interactive shared artifact.
    /// </summary>
    /// <param name="state">The current artifact state.</param>
    /// <param name="action">The action containing the new document state snapshot.</param>
    /// <returns>A new <see cref="ArtifactState"/> with the updated document state.</returns>
    [ReducerMethod]
    public static ArtifactState ReduceSetDocumentAction(ArtifactState state, SetDocumentAction action) =>
        state with { CurrentDocumentState = action.DocumentState, HasInteractiveArtifact = true };

    /// <summary>
    /// Handles <see cref="SetDocumentPreviewAction"/> by toggling the document preview flag.
    /// </summary>
    /// <param name="state">The current artifact state.</param>
    /// <param name="action">The action containing the desired preview mode.</param>
    /// <returns>A new <see cref="ArtifactState"/> with the updated preview flag.</returns>
    [ReducerMethod]
    public static ArtifactState ReduceSetDocumentPreviewAction(ArtifactState state, SetDocumentPreviewAction action) =>
        state with { IsDocumentPreview = action.IsPreview };

    /// <summary>
    /// Handles <see cref="ClearArtifactsAction"/> by resetting all artifact state to defaults.
    /// Sets <see cref="ArtifactState.HasInteractiveArtifact"/> to <see langword="false"/> since no artifacts remain active.
    /// </summary>
    /// <param name="state">The current artifact state (unused).</param>
    /// <param name="action">The clear action.</param>
    /// <returns>A new <see cref="ArtifactState"/> with all fields set to their initial values.</returns>
    [ReducerMethod]
    public static ArtifactState ReduceClearArtifactsAction(ArtifactState state, ClearArtifactsAction action) =>
        new(CurrentRecipe: null, CurrentDocumentState: null, IsDocumentPreview: true, HasInteractiveArtifact: false);
}
