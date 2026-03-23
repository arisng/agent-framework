using System.Text.Json.Serialization;

namespace AGUIDojoClient.Models;

/// <summary>
/// Represents chart data returned by the <c>show_chart</c> tool
/// for rendering in the <see cref="Components.GenerativeUI.ChartDisplay"/> component.
/// </summary>
public sealed class ChartResult
{
    /// <summary>
    /// A human-readable title for the chart.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The type of chart (e.g., "bar", "line", "pie", "area").
    /// </summary>
    [JsonPropertyName("chartType")]
    public string ChartType { get; init; } = "bar";

    /// <summary>
    /// The ordered list of data point labels (e.g., x-axis categories).
    /// </summary>
    [JsonPropertyName("labels")]
    public List<string> Labels { get; init; } = [];

    /// <summary>
    /// The chart datasets, where each dataset has a name and corresponding values aligned with <see cref="Labels"/>.
    /// </summary>
    [JsonPropertyName("datasets")]
    public List<ChartDataset> Datasets { get; init; } = [];
}

/// <summary>
/// Represents a single dataset within a chart.
/// </summary>
public sealed class ChartDataset
{
    /// <summary>
    /// The display name for this dataset (used in legends).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The numeric values aligned with the parent chart's labels.
    /// </summary>
    [JsonPropertyName("values")]
    public List<double> Values { get; init; } = [];
}
