using System.Text.Json.Serialization;

namespace AGUIDojoServer.Tools;

/// <summary>
/// Represents a dynamic form definition returned by the <c>show_form</c> AI tool.
/// </summary>
/// <param name="Title">A human-readable title for the form.</param>
/// <param name="Description">An optional description or instructions for the form.</param>
/// <param name="Fields">The ordered list of form field definitions.</param>
/// <param name="SubmitLabel">The label for the submit button (defaults to "Submit").</param>
public sealed record DynamicFormResult(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("fields")] List<FormFieldDefinition> Fields,
    [property: JsonPropertyName("submitLabel")] string SubmitLabel = "Submit");

/// <summary>
/// Represents a single field definition within a dynamic form.
/// </summary>
/// <param name="Name">The unique field identifier / name attribute.</param>
/// <param name="Label">The human-readable label displayed above the field.</param>
/// <param name="Type">The field type (text, email, number, textarea, select, checkbox).</param>
/// <param name="Placeholder">Optional placeholder text for the input.</param>
/// <param name="Required">Whether the field is required.</param>
/// <param name="DefaultValue">Optional default value for the field.</param>
/// <param name="Options">Optional list of options for select-type fields.</param>
public sealed record FormFieldDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("placeholder")] string? Placeholder = null,
    [property: JsonPropertyName("required")] bool Required = false,
    [property: JsonPropertyName("defaultValue")] string? DefaultValue = null,
    [property: JsonPropertyName("options")] List<string>? Options = null);
