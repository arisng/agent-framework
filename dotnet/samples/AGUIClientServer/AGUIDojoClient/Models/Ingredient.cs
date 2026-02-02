// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents an ingredient in a recipe.
/// Used by the /shared_state endpoint for recipe state management.
/// </summary>
public sealed class Ingredient
{
    /// <summary>
    /// Emoji icon representing the ingredient.
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Name of the ingredient.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Amount of the ingredient needed.
    /// </summary>
    [JsonPropertyName("amount")]
    public string Amount { get; set; } = string.Empty;
}
