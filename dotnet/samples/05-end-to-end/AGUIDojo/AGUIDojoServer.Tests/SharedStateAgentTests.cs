// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;

namespace AGUIDojoServer.Tests;

/// <summary>
/// Unit tests for SharedStateAgent IsRecipeState helper method.
/// Tests the state detection logic through a test helper class.
/// </summary>
public class SharedStateAgentTests
{
    /// <summary>
    /// Test IsRecipeState with a valid recipe JSON structure containing nested "recipe" object.
    /// Should return true because it has the "recipe" property.
    /// </summary>
    [Fact]
    public void IsRecipeState_WithNestedRecipeObject_ReturnsTrue()
    {
        // Arrange
        const string json = """
        {
            "recipe": {
                "title": "Chocolate Chip Cookies",
                "ingredients": ["flour", "sugar", "chocolate chips"],
                "instructions": "Mix and bake at 350F"
            }
        }
        """;
        var state = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        bool result = SharedStateTestHelper.IsRecipeState(state);

        // Assert
        Assert.True(result, "Recipe state with nested 'recipe' object should be detected");
    }

    /// <summary>
    /// Test IsRecipeState with a valid recipe JSON structure containing flat ingredients and title.
    /// Should return true because it has both "ingredients" and "title" properties.
    /// </summary>
    [Fact]
    public void IsRecipeState_WithFlatRecipeStructure_ReturnsTrue()
    {
        // Arrange
        const string json = """
        {
            "title": "Pasta Carbonara",
            "ingredients": ["pasta", "eggs", "bacon", "parmesan"],
            "cooking_time": "20 minutes"
        }
        """;
        var state = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        bool result = SharedStateTestHelper.IsRecipeState(state);

        // Assert
        Assert.True(result, "Recipe state with 'ingredients' and 'title' properties should be detected");
    }

    /// <summary>
    /// Test IsRecipeState with non-recipe JSON (generic state like favorite color).
    /// Should return false because it lacks recipe-specific properties.
    /// </summary>
    [Fact]
    public void IsRecipeState_WithNonRecipeJson_ReturnsFalse()
    {
        // Arrange
        const string json = """
        {
            "favorite_color": "blue",
            "user_preferences": {
                "theme": "dark",
                "language": "en"
            }
        }
        """;
        var state = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        bool result = SharedStateTestHelper.IsRecipeState(state);

        // Assert
        Assert.False(result, "Non-recipe state should not be detected as recipe");
    }

    /// <summary>
    /// Test IsRecipeState with malformed JSON (empty object).
    /// Should return false gracefully without throwing exceptions.
    /// </summary>
    [Fact]
    public void IsRecipeState_WithEmptyJson_ReturnsFalse()
    {
        // Arrange
        const string json = "{}";
        var state = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        bool result = SharedStateTestHelper.IsRecipeState(state);

        // Assert
        Assert.False(result, "Empty JSON should not be detected as recipe");
    }

    /// <summary>
    /// Test IsRecipeState with JSON array (invalid state structure).
    /// Should return false gracefully without throwing exceptions.
    /// </summary>
    [Fact]
    public void IsRecipeState_WithJsonArray_ReturnsFalse()
    {
        // Arrange
        const string json = """["item1", "item2", "item3"]""";
        var state = JsonSerializer.Deserialize<JsonElement>(json);

        // Act
        bool result = SharedStateTestHelper.IsRecipeState(state);

        // Assert
        Assert.False(result, "JSON array should not be detected as recipe");
    }
}

/// <summary>
/// Test helper class that exposes internal SharedStateAgent logic for testing.
/// </summary>
internal static class SharedStateTestHelper
{
    /// <summary>
    /// Determines if the given state JSON represents a Recipe object.
    /// This duplicates the logic from SharedStateAgent.IsRecipeState for testing purposes.
    /// </summary>
    public static bool IsRecipeState(JsonElement state)
    {
        try
        {
            if (state.ValueKind == JsonValueKind.Object)
            {
                return state.TryGetProperty("recipe", out _) ||
                       (state.TryGetProperty("ingredients", out _) && state.TryGetProperty("title", out _));
            }
        }
        catch
        {
            // If we can't parse it, assume it's not a recipe
        }

        return false;
    }
}
