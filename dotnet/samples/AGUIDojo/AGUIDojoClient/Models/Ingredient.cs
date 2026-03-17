// Copyright (c) Microsoft. All rights reserved.

using System.Globalization;
using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents an ingredient in a recipe.
/// Used by the /shared_state endpoint for recipe state management.
/// </summary>
public sealed class Ingredient
{
    /// <summary>
    /// Default fallback emoji icon.
    /// </summary>
    public const string DefaultIcon = "🥗";

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

    /// <summary>
    /// Gets the icon with fallback to a default emoji if the current icon is invalid.
    /// </summary>
    /// <returns>A valid emoji icon, or the default fallback if invalid.</returns>
    public string GetSafeIcon()
    {
        return IsValidEmoji(this.Icon) ? this.Icon : DefaultIcon;
    }

    /// <summary>
    /// Checks if the given string is a valid emoji.
    /// Returns false for empty strings, numeric values, and plain ASCII text.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns>True if the string appears to be a valid emoji.</returns>
    public static bool IsValidEmoji(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Reject pure numeric values like "525"
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        // Check if the string contains emoji characters
        // Emojis are typically in these Unicode ranges:
        // - U+1F300 to U+1F9FF (Miscellaneous Symbols and Pictographs, Emoticons, etc.)
        // - U+2600 to U+26FF (Miscellaneous Symbols)
        // - U+2700 to U+27BF (Dingbats)
        // - U+FE00 to U+FE0F (Variation Selectors)
        // - U+1F000 to U+1FFFF (Supplemental Symbols and Pictographs)
        foreach (var rune in value.EnumerateRunes())
        {
            int codePoint = rune.Value;

            // Check common emoji ranges
            if ((codePoint >= 0x1F300 && codePoint <= 0x1F9FF) ||  // Emoji symbols
                (codePoint >= 0x1F600 && codePoint <= 0x1F64F) ||  // Emoticons
                (codePoint >= 0x1F680 && codePoint <= 0x1F6FF) ||  // Transport and Map
                (codePoint >= 0x2600 && codePoint <= 0x26FF) ||    // Misc symbols
                (codePoint >= 0x2700 && codePoint <= 0x27BF) ||    // Dingbats
                (codePoint >= 0x1F1E0 && codePoint <= 0x1F1FF) ||  // Flags
                (codePoint >= 0x1F900 && codePoint <= 0x1F9FF) ||  // Supplemental Symbols
                (codePoint >= 0x1FA00 && codePoint <= 0x1FA6F) ||  // Chess, symbols
                (codePoint >= 0x1FA70 && codePoint <= 0x1FAFF))    // Symbols extended
            {
                return true;
            }
        }

        return false;
    }
}
