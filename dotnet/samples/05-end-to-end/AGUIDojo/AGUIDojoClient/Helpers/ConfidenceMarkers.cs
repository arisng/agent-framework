using System.Linq;
using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace AGUIDojoClient.Helpers;

/// <summary>
/// Parses and formats agent confidence metadata emitted by the model.
/// </summary>
public static class ConfidenceMarkers
{
    /// <summary>
    /// The additional-properties key used to persist a response confidence score.
    /// </summary>
    public const string ConfidenceScoreKey = "confidence_score";

    private const string ConfidenceTagPrefix = "<!-- confidence:";
    private const string ConfidenceTagSuffix = "-->";

    /// <summary>
    /// Removes a leading confidence comment from raw model text and extracts the score.
    /// </summary>
    /// <param name="text">The raw model text, potentially prefixed with a confidence comment.</param>
    /// <param name="confidenceScore">When this method returns, the parsed confidence score or null.</param>
    /// <returns>The visible text with the confidence comment removed.</returns>
    public static string StripLeadingConfidenceComment(string text, out double? confidenceScore)
    {
        confidenceScore = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string workingText = text.TrimStart();
        if (!workingText.StartsWith(ConfidenceTagPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        int tagEndIndex = workingText.IndexOf(ConfidenceTagSuffix, StringComparison.Ordinal);
        if (tagEndIndex < 0)
        {
            return string.Empty;
        }

        string scoreText = workingText[ConfidenceTagPrefix.Length..tagEndIndex].Trim();
        if (double.TryParse(scoreText, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedScore))
        {
            confidenceScore = Math.Clamp(parsedScore, 0d, 1d);
        }

        string visibleText = workingText[(tagEndIndex + ConfidenceTagSuffix.Length)..];
        return visibleText.TrimStart();
    }

    /// <summary>
    /// Reads a persisted confidence score from message metadata.
    /// </summary>
    /// <param name="additionalProperties">The message additional-properties bag.</param>
    /// <param name="confidenceScore">When this method returns, the parsed confidence score.</param>
    /// <returns><see langword="true"/> when a valid confidence score is present; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetConfidenceScore(object? additionalProperties, out double confidenceScore)
    {
        confidenceScore = default;

        object? rawValue = TryGetPropertyValue(additionalProperties, ConfidenceScoreKey);
        return TryParseConfidenceValue(rawValue, out confidenceScore);
    }

    /// <summary>
    /// Gets the compact label rendered in the UI for a confidence score.
    /// </summary>
    /// <param name="confidenceScore">A normalized score between 0 and 1.</param>
    /// <returns>A human-friendly label.</returns>
    public static string GetConfidenceLabel(double confidenceScore)
        => confidenceScore < 0.55d
            ? "Review recommended"
            : $"{Math.Round(confidenceScore * 100d):0}%";

    /// <summary>
    /// Gets the visual tone used for a confidence score.
    /// </summary>
    /// <param name="confidenceScore">A normalized score between 0 and 1.</param>
    /// <returns>The tone name used by CSS.</returns>
    public static string GetConfidenceTone(double confidenceScore)
        => confidenceScore < 0.55d
            ? "low"
            : confidenceScore < 0.8d
                ? "medium"
                : "high";

    /// <summary>
    /// Gets the accessibility label for a confidence score.
    /// </summary>
    /// <param name="confidenceScore">A normalized score between 0 and 1.</param>
    /// <returns>An aria-label friendly description.</returns>
    public static string GetConfidenceDescription(double confidenceScore)
        => confidenceScore < 0.55d
            ? $"Review recommended. Confidence {Math.Round(confidenceScore * 100d):0}%."
            : $"Confidence {Math.Round(confidenceScore * 100d):0}%";

    /// <summary>
    /// Estimates a fallback confidence score from the visible assistant text.
    /// </summary>
    /// <param name="text">The assistant text to evaluate.</param>
    /// <returns>A normalized confidence score between 0 and 1.</returns>
    public static double EstimateConfidenceScore(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0.55d;
        }

        string normalized = text.ToUpperInvariant();
        double score = 0.78d;

        if (text.Length < 40)
        {
            score += 0.08d;
        }
        else if (text.Length > 240)
        {
            score -= 0.08d;
        }

        string[] uncertaintyMarkers =
        [
            "MAYBE",
            "MIGHT",
            "NOT SURE",
            "I THINK",
            "I BELIEVE",
            "COULD BE",
            "PROBABLY",
            "POSSIBLY",
            "UNCERTAIN",
            "UNSURE",
            "APPROXIMATELY",
            "ROUGHLY",
        ];

        if (uncertaintyMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal)))
        {
            score -= 0.28d;
        }

        string[] errorMarkers =
        [
            "ERROR",
            "UNABLE",
            "CANNOT",
            "CAN'T",
            "FAILED",
            "FAILURE",
        ];

        if (errorMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal)))
        {
            score -= 0.22d;
        }

        if (normalized.EndsWith('?'))
        {
            score -= 0.05d;
        }

        return Math.Clamp(score, 0.15d, 0.95d);
    }

    private static object? TryGetPropertyValue(object? additionalProperties, string key)
    {
        return additionalProperties switch
        {
            IReadOnlyDictionary<string, object?> readOnly when readOnly.TryGetValue(key, out object? value) => value,
            IDictionary<string, object?> mutable when mutable.TryGetValue(key, out object? value) => value,
            IDictionary nonGeneric when nonGeneric.Contains(key) => nonGeneric[key],
            IEnumerable<KeyValuePair<string, object?>> pairs => pairs.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.Ordinal)).Value,
            _ => null,
        };
    }

    private static bool TryParseConfidenceValue(object? value, out double confidenceScore)
    {
        switch (value)
        {
            case double doubleValue:
                confidenceScore = Math.Clamp(doubleValue, 0d, 1d);
                return true;
            case float floatValue:
                confidenceScore = Math.Clamp(floatValue, 0f, 1f);
                return true;
            case decimal decimalValue:
                confidenceScore = Math.Clamp((double)decimalValue, 0d, 1d);
                return true;
            case int intValue:
                confidenceScore = Math.Clamp(intValue, 0, 1);
                return true;
            case string stringValue when double.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedStringValue):
                confidenceScore = Math.Clamp(parsedStringValue, 0d, 1d);
                return true;
            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetDouble(out double parsedJsonValue):
                confidenceScore = Math.Clamp(parsedJsonValue, 0d, 1d);
                return true;
            default:
                confidenceScore = default;
                return false;
        }
    }
}
