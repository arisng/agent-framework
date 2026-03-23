using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents tabular data returned by the <c>show_data_grid</c> tool
/// for rendering in the <see cref="Components.GenerativeUI.DataGridDisplay"/> component.
/// </summary>
public sealed class DataGridResult
{
    /// <summary>
    /// A human-readable title for the data grid.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The ordered list of column header names.
    /// </summary>
    [JsonPropertyName("columns")]
    public List<string> Columns { get; init; } = [];

    /// <summary>
    /// The row data as a list of dictionaries, where each dictionary maps column names to cell values.
    /// </summary>
    [JsonPropertyName("rows")]
    public List<Dictionary<string, string>> Rows { get; init; } = [];
}
