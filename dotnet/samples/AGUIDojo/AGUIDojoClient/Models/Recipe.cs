// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents a recipe with ingredients and instructions.
/// Used by the /shared_state endpoint for bidirectional state sync.
/// </summary>
public sealed class Recipe
{
    /// <summary>
    /// Title of the recipe.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Skill level required (e.g., "Beginner", "Intermediate", "Advanced").
    /// </summary>
    [JsonPropertyName("skill_level")]
    public string SkillLevel { get; set; } = string.Empty;

    /// <summary>
    /// Estimated cooking time.
    /// </summary>
    [JsonPropertyName("cooking_time")]
    public string CookingTime { get; set; } = string.Empty;

    /// <summary>
    /// Special dietary preferences or notes.
    /// </summary>
    [JsonPropertyName("special_preferences")]
    public List<string> SpecialPreferences { get; set; } = [];

    /// <summary>
    /// List of ingredients needed for the recipe.
    /// </summary>
    [JsonPropertyName("ingredients")]
    public List<Ingredient> Ingredients { get; set; } = [];

    /// <summary>
    /// Step-by-step cooking instructions.
    /// </summary>
    [JsonPropertyName("instructions")]
    public List<string> Instructions { get; set; } = [];
}
