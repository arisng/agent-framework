using System.Text.Json;
using AGUIDojoClient.Models;
using AGUIDojoClient.Shared;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Services;

/// <summary>
/// Parses typed tool results and provides shared display metadata for artifact rendering.
/// </summary>
public static class ToolResultParser
{
    /// <summary>
    /// Attempts to parse a <see cref="FunctionResultContent"/> into a typed tool result model.
    /// </summary>
    public static object? TryParseToolResult(string toolName, FunctionResultContent frc)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        JsonSerializerOptions options = JsonDefaults.Options;

        if (IsMermaidTool(toolName))
        {
            return TryDeserializeMermaidResult(frc.Result, options);
        }

        JsonElement? jsonElement = TryGetJsonElement(frc.Result);
        if (jsonElement is null)
        {
            return null;
        }

        return NormalizeToolName(toolName) switch
        {
            "GET_WEATHER" => TryDeserializeWeatherInfo(jsonElement.Value, options),
            "SHOW_DATA_GRID" => TryDeserializeDataGridResult(jsonElement.Value, options),
            "SHOW_CHART" => TryDeserializeChartResult(jsonElement.Value, options),
            "SHOW_FORM" => TryDeserializeDynamicFormResult(jsonElement.Value, options),
            _ => null,
        };
    }

    /// <summary>
    /// Gets a user-facing title for a parsed tool artifact.
    /// </summary>
    public static string GetArtifactTitle(string toolName, object parsedData) => parsedData switch
    {
        WeatherInfo => "Weather",
        DataGridResult dataGrid when !string.IsNullOrWhiteSpace(dataGrid.Title) => dataGrid.Title,
        ChartResult chart when !string.IsNullOrWhiteSpace(chart.Title) => chart.Title,
        DynamicFormResult form when !string.IsNullOrWhiteSpace(form.Title) => form.Title,
        MermaidResult diagram when !string.IsNullOrWhiteSpace(diagram.Title) => diagram.Title,
        _ => GetToolDisplayName(toolName),
    };

    /// <summary>
    /// Gets a stable display label for a tool name.
    /// </summary>
    public static string GetToolDisplayName(string? toolName) => NormalizeToolName(toolName) switch
    {
        "GET_WEATHER" => "Weather",
        "SHOW_DATA_GRID" => "Data Table",
        "SHOW_CHART" => "Chart",
        "SHOW_FORM" => "Form",
        "SHOW_MERMAID" or "SHOW_DIAGRAM" or "RENDER_MERMAID" => "Diagram",
        _ => "Artifact",
    };

    /// <summary>
    /// Gets the Lucide icon name for a tool artifact.
    /// </summary>
    public static string GetToolIcon(string? toolName) => NormalizeToolName(toolName) switch
    {
        "GET_WEATHER" => "cloud-sun",
        "SHOW_DATA_GRID" => "table-2",
        "SHOW_CHART" => "bar-chart-3",
        "SHOW_FORM" => "clipboard-list",
        "SHOW_MERMAID" or "SHOW_DIAGRAM" or "RENDER_MERMAID" => "workflow",
        _ => "app-window",
    };

    /// <summary>
    /// Gets a value indicating whether the tool represents a Mermaid diagram.
    /// </summary>
    public static bool IsMermaidTool(string toolName) =>
        toolName.Equals("show_mermaid", StringComparison.OrdinalIgnoreCase)
        || toolName.Equals("show_diagram", StringComparison.OrdinalIgnoreCase)
        || toolName.Equals("render_mermaid", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeToolName(string? toolName) => string.IsNullOrWhiteSpace(toolName)
        ? string.Empty
        : toolName.ToUpperInvariant();

    private static JsonElement? TryGetJsonElement(object? result)
    {
        return result switch
        {
            JsonElement jsonElement => jsonElement,
            string text when LooksLikeJson(text) => JsonDocument.Parse(text).RootElement.Clone(),
            _ => null,
        };
    }

    private static bool LooksLikeJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        char firstChar = value.TrimStart()[0];
        return firstChar is '{' or '[' or '"' || char.IsDigit(firstChar) || firstChar is '-' or 't' or 'f' or 'n';
    }

    private static WeatherInfo? TryDeserializeWeatherInfo(JsonElement json, JsonSerializerOptions options)
    {
        if (json.TryGetProperty("temperature", out _) &&
            json.TryGetProperty("conditions", out _))
        {
            return json.Deserialize<WeatherInfo>(options);
        }

        return null;
    }

    private static DataGridResult? TryDeserializeDataGridResult(JsonElement json, JsonSerializerOptions options)
    {
        if (json.TryGetProperty("columns", out _) &&
            json.TryGetProperty("rows", out _))
        {
            return json.Deserialize<DataGridResult>(options);
        }

        return null;
    }

    private static DynamicFormResult? TryDeserializeDynamicFormResult(JsonElement json, JsonSerializerOptions options)
    {
        if (json.TryGetProperty("fields", out _) &&
            json.TryGetProperty("title", out _))
        {
            return json.Deserialize<DynamicFormResult>(options);
        }

        return null;
    }

    private static ChartResult? TryDeserializeChartResult(JsonElement json, JsonSerializerOptions options)
    {
        if (json.TryGetProperty("labels", out _) &&
            json.TryGetProperty("datasets", out _))
        {
            return json.Deserialize<ChartResult>(options);
        }

        return null;
    }

    private static MermaidResult? TryDeserializeMermaidResult(object? rawResult, JsonSerializerOptions options)
    {
        if (rawResult is string text && !LooksLikeJson(text))
        {
            string trimmed = text.Trim();
            return string.IsNullOrWhiteSpace(trimmed)
                ? null
                : new MermaidResult { Definition = trimmed };
        }

        JsonElement? jsonElement = TryGetJsonElement(rawResult);
        if (jsonElement is null)
        {
            return null;
        }

        if (jsonElement.Value.ValueKind == JsonValueKind.String)
        {
            string? stringDefinition = jsonElement.Value.GetString();
            return string.IsNullOrWhiteSpace(stringDefinition)
                ? null
                : new MermaidResult { Definition = stringDefinition };
        }

        if (jsonElement.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonElement json = jsonElement.Value;
        string? definition =
            TryGetString(json, "definition")
            ?? TryGetString(json, "diagram")
            ?? TryGetString(json, "code")
            ?? TryGetString(json, "source");

        if (string.IsNullOrWhiteSpace(definition))
        {
            return null;
        }

        MermaidResult? deserialized = json.Deserialize<MermaidResult>(options);
        if (deserialized is not null && !string.IsNullOrWhiteSpace(deserialized.Definition))
        {
            return deserialized;
        }

        return new MermaidResult
        {
            Title = TryGetString(json, "title") ?? "Diagram",
            Description = TryGetString(json, "description"),
            Definition = definition,
        };
    }

    private static string? TryGetString(JsonElement json, string propertyName)
    {
        if (!json.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}
