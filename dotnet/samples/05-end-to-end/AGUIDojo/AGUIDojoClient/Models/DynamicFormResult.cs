// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents a dynamic form definition returned by the <c>show_form</c> tool
/// for rendering in the <see cref="Components.GenerativeUI.DynamicFormDisplay"/> component.
/// </summary>
public sealed class DynamicFormResult
{
    /// <summary>
    /// A human-readable title for the form.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// An optional description or instructions for the form.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// The ordered list of form field definitions.
    /// </summary>
    [JsonPropertyName("fields")]
    public List<FormFieldDefinition> Fields { get; init; } = [];

    /// <summary>
    /// The label for the submit button.
    /// </summary>
    [JsonPropertyName("submitLabel")]
    public string SubmitLabel { get; init; } = "Submit";
}

/// <summary>
/// Represents a single field definition within a dynamic form.
/// </summary>
public sealed class FormFieldDefinition
{
    /// <summary>
    /// The unique field identifier / name attribute.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The human-readable label displayed above the field.
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// The field type (text, email, number, textarea, select, checkbox).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    /// <summary>
    /// Optional placeholder text for the input.
    /// </summary>
    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }

    /// <summary>
    /// Whether the field is required.
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; init; }

    /// <summary>
    /// Optional default value for the field.
    /// </summary>
    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Optional list of options for select-type fields.
    /// </summary>
    [JsonPropertyName("options")]
    public List<string>? Options { get; init; }
}
