// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Event arguments for recipe state changes.
/// </summary>
public sealed class RecipeChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the updated recipe.
    /// </summary>
    public Recipe Recipe { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipeChangedEventArgs"/> class.
    /// </summary>
    /// <param name="recipe">The updated recipe.</param>
    public RecipeChangedEventArgs(Recipe recipe)
    {
        this.Recipe = recipe;
    }
}

/// <summary>
/// Interface for managing shared application state with the AG-UI server.
/// Provides bidirectional state synchronization for the /shared_state endpoint.
/// </summary>
public interface IStateManager
{
    /// <summary>
    /// Gets the current recipe state being managed.
    /// </summary>
    Recipe CurrentRecipe { get; }

    /// <summary>
    /// Gets a value indicating whether there is active state that should be sent to the server.
    /// </summary>
    bool HasActiveState { get; }

    /// <summary>
    /// Event fired when the recipe state changes.
    /// </summary>
    event EventHandler<RecipeChangedEventArgs>? StateChanged;

    /// <summary>
    /// Initializes the state manager with a default recipe template.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the current recipe state.
    /// </summary>
    /// <param name="recipe">The updated recipe.</param>
    void UpdateRecipe(Recipe recipe);

    /// <summary>
    /// Updates the current recipe from a server state snapshot.
    /// </summary>
    /// <param name="recipe">The recipe received from the server.</param>
    void UpdateFromServerSnapshot(Recipe recipe);

    /// <summary>
    /// Clears the current recipe state.
    /// </summary>
    void Clear();

    /// <summary>
    /// Creates a DataContent instance containing the current state as JSON.
    /// This content should be added to the message stream for the server to receive.
    /// </summary>
    /// <returns>A DataContent with the serialized recipe state.</returns>
    DataContent CreateStateContent();

    /// <summary>
    /// Tries to extract a recipe from a DataContent state snapshot.
    /// Returns false if the content doesn't represent a recipe (e.g., it's a Plan snapshot).
    /// </summary>
    /// <param name="dataContent">The DataContent to parse.</param>
    /// <param name="recipe">The extracted recipe, or null if parsing failed.</param>
    /// <returns>True if a recipe was successfully extracted.</returns>
    bool TryExtractRecipeSnapshot(DataContent dataContent, out Recipe? recipe);
}
