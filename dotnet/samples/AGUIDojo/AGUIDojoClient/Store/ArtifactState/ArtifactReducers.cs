// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Immutable;
using AGUIDojoClient.Models;
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
    /// Adds <see cref="ArtifactType.RecipeEditor"/> to visible tabs and auto-activates it if no tab is active.
    /// </summary>
    [ReducerMethod]
    public static ArtifactState ReduceSetRecipeAction(ArtifactState state, SetRecipeAction action)
    {
        var tabs = state.VisibleTabs.Add(ArtifactType.RecipeEditor);
        var activeType = state.ActiveArtifactType == ArtifactType.None
            ? ArtifactType.RecipeEditor
            : state.ActiveArtifactType;

        return state with
        {
            CurrentRecipe = action.Recipe,
            HasInteractiveArtifact = true,
            VisibleTabs = tabs,
            ActiveArtifactType = activeType
        };
    }

    /// <summary>
    /// Handles <see cref="SetDocumentAction"/> by replacing the current document state.
    /// Adds <see cref="ArtifactType.DocumentPreview"/> to visible tabs and auto-activates it if no tab is active.
    /// </summary>
    [ReducerMethod]
    public static ArtifactState ReduceSetDocumentAction(ArtifactState state, SetDocumentAction action)
    {
        var tabs = state.VisibleTabs.Add(ArtifactType.DocumentPreview);
        var activeType = state.ActiveArtifactType == ArtifactType.None
            ? ArtifactType.DocumentPreview
            : state.ActiveArtifactType;

        return state with
        {
            CurrentDocumentState = action.DocumentState,
            HasInteractiveArtifact = true,
            VisibleTabs = tabs,
            ActiveArtifactType = activeType
        };
    }

    /// <summary>
    /// Handles <see cref="SetDocumentPreviewAction"/> by toggling the document preview flag.
    /// </summary>
    [ReducerMethod]
    public static ArtifactState ReduceSetDocumentPreviewAction(ArtifactState state, SetDocumentPreviewAction action) =>
        state with { IsDocumentPreview = action.IsPreview };

    /// <summary>
    /// Handles <see cref="ClearArtifactsAction"/> by resetting all artifact state to defaults.
    /// Clears all data, visible tabs, and resets the active tab to <see cref="ArtifactType.None"/>.
    /// </summary>
    [ReducerMethod]
    public static ArtifactState ReduceClearArtifactsAction(ArtifactState state, ClearArtifactsAction action) =>
        new(
            CurrentRecipe: null,
            CurrentDocumentState: null,
            IsDocumentPreview: true,
            HasInteractiveArtifact: false,
            ActiveArtifactType: ArtifactType.None,
            DiffPreview: null,
            CurrentDataGrid: null,
            VisibleTabs: ImmutableHashSet<ArtifactType>.Empty);

    /// <summary>
    /// Handles <see cref="SetDiffPreviewArtifactAction"/> by storing diff preview data
    /// and adding <see cref="ArtifactType.DiffPreview"/> to visible tabs.
    /// Auto-activates the DiffPreview tab if no tab is currently active.
    /// </summary>
    [ReducerMethod]
    public static ArtifactState ReduceSetDiffPreviewArtifactAction(ArtifactState state, SetDiffPreviewArtifactAction action)
    {
        var diffData = new DiffPreviewData(action.Before, action.After, action.Title);
        var tabs = state.VisibleTabs.Add(ArtifactType.DiffPreview);
        var activeType = state.ActiveArtifactType == ArtifactType.None
            ? ArtifactType.DiffPreview
            : state.ActiveArtifactType;

        return state with
        {
            DiffPreview = diffData,
            HasInteractiveArtifact = true,
            VisibleTabs = tabs,
            ActiveArtifactType = activeType
        };
    }

    /// <summary>
    /// Handles <see cref="SetDataGridArtifactAction"/> by storing data grid result
    /// and adding <see cref="ArtifactType.DataGrid"/> to visible tabs.
    /// Auto-activates the DataGrid tab since it's an interactable artifact that requires user attention.
    /// </summary>
    [ReducerMethod]
    public static ArtifactState ReduceSetDataGridArtifactAction(ArtifactState state, SetDataGridArtifactAction action)
    {
        var tabs = state.VisibleTabs.Add(ArtifactType.DataGrid);

        return state with
        {
            CurrentDataGrid = action.DataGrid,
            HasInteractiveArtifact = true,
            VisibleTabs = tabs,
            ActiveArtifactType = ArtifactType.DataGrid // Always switch to DataGrid — it's interactable
        };
    }

    /// <summary>
    /// Handles <see cref="SetActiveArtifactAction"/> by switching the active tab.
    /// Only allows switching to tabs that exist in <see cref="ArtifactState.VisibleTabs"/>.
    /// </summary>
    [ReducerMethod]
    public static ArtifactState ReduceSetActiveArtifactAction(ArtifactState state, SetActiveArtifactAction action)
    {
        // Only allow switching to visible tabs (or None)
        if (action.ArtifactType != ArtifactType.None && !state.VisibleTabs.Contains(action.ArtifactType))
        {
            return state;
        }

        return state with { ActiveArtifactType = action.ArtifactType };
    }
}
