// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Store.ArtifactState;

/// <summary>
/// Enumerates the types of artifacts that can be displayed in the Canvas Pane.
/// Used by <see cref="ArtifactState.ActiveArtifactType"/> to track the currently selected tab
/// and by <see cref="ArtifactState.VisibleTabs"/> to determine which tabs to render.
/// </summary>
public enum ArtifactType
{
    /// <summary>No artifact is active. Canvas pane shows empty state.</summary>
    None,

    /// <summary>Diff preview showing before/after state comparison (Plan, Recipe, or Document changes).</summary>
    DiffPreview,

    /// <summary>Data grid showing tabular data from the <c>show_data_grid</c> tool.</summary>
    DataGrid,

    /// <summary>Recipe editor for shared state endpoint interaction.</summary>
    RecipeEditor,

    /// <summary>Document preview/editor for predictive state updates endpoint.</summary>
    DocumentPreview
}
