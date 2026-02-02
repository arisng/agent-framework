// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.RegularExpressions;
using AGUIDojoClient.Models;

namespace AGUIDojoClient.Services;

/// <summary>
/// Service for applying JSON Patch operations to Plan models.
/// Implements a minimal subset of RFC 6902 for Plan/Step state updates.
/// </summary>
public interface IJsonPatchApplier
{
    /// <summary>
    /// Applies a list of JSON Patch operations to a Plan model.
    /// </summary>
    /// <param name="plan">The plan to modify.</param>
    /// <param name="operations">The patch operations to apply.</param>
    void ApplyPatch(Plan plan, IEnumerable<JsonPatchOperation> operations);
}

/// <summary>
/// Implementation of JSON Patch applier for Plan models.
/// Supports 'replace' operations on step descriptions and statuses.
/// </summary>
public sealed class JsonPatchApplier : IJsonPatchApplier
{
    // Pattern to match /steps/{index}/property paths
    private static readonly Regex StepPathPattern = new(@"^/steps/(\d+)/(\w+)$", RegexOptions.Compiled);

    /// <inheritdoc/>
    public void ApplyPatch(Plan plan, IEnumerable<JsonPatchOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(operations);

        foreach (var operation in operations)
        {
            ApplyOperation(plan, operation);
        }
    }

    /// <summary>
    /// Applies a single JSON Patch operation to the plan.
    /// </summary>
    private static void ApplyOperation(Plan plan, JsonPatchOperation operation)
    {
        if (!string.Equals(operation.Op, "replace", StringComparison.OrdinalIgnoreCase))
        {
            // Currently only 'replace' operations are supported for Plan updates
            return;
        }

        var match = StepPathPattern.Match(operation.Path);
        if (!match.Success)
        {
            // Path doesn't match expected format for step updates
            return;
        }

        if (!int.TryParse(match.Groups[1].Value, out int stepIndex))
        {
            return;
        }

        if (stepIndex < 0 || stepIndex >= plan.Steps.Count)
        {
            // Step index out of range
            return;
        }

        string propertyName = match.Groups[2].Value;
        Step step = plan.Steps[stepIndex];

        if (string.Equals(propertyName, "description", StringComparison.OrdinalIgnoreCase))
        {
            step.Description = ExtractStringValue(operation.Value) ?? step.Description;
        }
        else if (string.Equals(propertyName, "status", StringComparison.OrdinalIgnoreCase))
        {
            string? statusValue = ExtractStringValue(operation.Value);
            if (statusValue is not null)
            {
                step.Status = ParseStepStatus(statusValue);
            }
        }
    }

    /// <summary>
    /// Extracts a string value from a JSON Patch value property.
    /// </summary>
    private static string? ExtractStringValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string stringValue)
        {
            return stringValue;
        }

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind == JsonValueKind.String
                ? jsonElement.GetString()
                : jsonElement.ToString();
        }

        return value.ToString();
    }

    /// <summary>
    /// Parses a status string to StepStatus enum.
    /// </summary>
    private static StepStatus ParseStepStatus(string statusValue)
    {
        return string.Equals(statusValue, "completed", StringComparison.OrdinalIgnoreCase)
            ? StepStatus.Completed
            : StepStatus.Pending;
    }
}
