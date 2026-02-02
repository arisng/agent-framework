// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Text.Json;
using AGUIDojoClient.Models;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Manages shared application state with the AG-UI server.
/// Handles bidirectional state synchronization for the /shared_state endpoint.
/// </summary>
public sealed class StateManager : IStateManager
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public Recipe CurrentRecipe { get; private set; } = new();

    /// <inheritdoc />
    public bool HasActiveState { get; private set; }

    /// <inheritdoc />
    public event EventHandler<RecipeChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public void Initialize()
    {
        this.CurrentRecipe = CreateDefaultRecipe();
        this.HasActiveState = true;
        this.OnStateChanged(this.CurrentRecipe);
    }

    /// <inheritdoc />
    public void UpdateRecipe(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        this.CurrentRecipe = recipe;
        this.HasActiveState = true;
        this.OnStateChanged(this.CurrentRecipe);
    }

    /// <inheritdoc />
    public void UpdateFromServerSnapshot(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        this.CurrentRecipe = recipe;
        this.HasActiveState = true;
        this.OnStateChanged(this.CurrentRecipe);
    }

    /// <inheritdoc />
    public void Clear()
    {
        this.CurrentRecipe = new Recipe();
        this.HasActiveState = false;
        this.OnStateChanged(this.CurrentRecipe);
    }

    /// <inheritdoc />
    public DataContent CreateStateContent()
    {
        string json = JsonSerializer.Serialize(this.CurrentRecipe, s_jsonOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return new DataContent(bytes, "application/json");
    }

    /// <inheritdoc />
    public bool TryExtractRecipeSnapshot(DataContent dataContent, out Recipe? recipe)
    {
        recipe = null;

        if (dataContent.MediaType != "application/json" || dataContent.Data.Length == 0)
        {
            return false;
        }

        try
        {
            string json = Encoding.UTF8.GetString(dataContent.Data.ToArray());
            using JsonDocument doc = JsonDocument.Parse(json);

            // Check if this is a Recipe snapshot (has "ingredients" or "title") and not a Plan (has "steps")
            // We exclude Plan snapshots which have a "steps" property
            if (doc.RootElement.TryGetProperty("steps", out _))
            {
                return false;
            }

            // Check if it looks like a Recipe (has recipe-specific fields)
            bool hasRecipeFields = doc.RootElement.TryGetProperty("title", out _) ||
                                   doc.RootElement.TryGetProperty("ingredients", out _) ||
                                   doc.RootElement.TryGetProperty("instructions", out _) ||
                                   doc.RootElement.TryGetProperty("skill_level", out _) ||
                                   doc.RootElement.TryGetProperty("cooking_time", out _);

            // Also check if it's a RecipeResponse wrapper (from server)
            if (doc.RootElement.TryGetProperty("recipe", out JsonElement recipeElement))
            {
                recipe = JsonSerializer.Deserialize<Recipe>(recipeElement.GetRawText(), s_jsonOptions);
                return recipe is not null;
            }

            if (hasRecipeFields)
            {
                recipe = JsonSerializer.Deserialize<Recipe>(json, s_jsonOptions);
                return recipe is not null;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a default recipe template for users to customize.
    /// </summary>
    private static Recipe CreateDefaultRecipe()
    {
        return new Recipe
        {
            Title = string.Empty,
            SkillLevel = "Beginner",
            CookingTime = "30 minutes",
            SpecialPreferences = [],
            Ingredients =
            [
                new Ingredient { Icon = "🍅", Name = "Tomatoes", Amount = "2 cups" },
                new Ingredient { Icon = "🧅", Name = "Onion", Amount = "1 medium" },
                new Ingredient { Icon = "🧄", Name = "Garlic", Amount = "3 cloves" }
            ],
            Instructions = []
        };
    }

    private void OnStateChanged(Recipe recipe)
    {
        StateChanged?.Invoke(this, new RecipeChangedEventArgs(recipe));
    }
}
