// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoServer.Tools;

/// <summary>
/// Represents chart data returned by the <c>show_chart</c> AI tool.
/// </summary>
/// <param name="Title">A human-readable title for the chart.</param>
/// <param name="ChartType">The type of chart (e.g., "bar", "line", "pie", "area").</param>
/// <param name="Labels">The ordered list of data point labels (e.g., x-axis categories).</param>
/// <param name="Datasets">
/// The chart datasets, where each dataset has a name and corresponding values aligned with <paramref name="Labels"/>.
/// </param>
public sealed record ChartResult(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("chartType")] string ChartType,
    [property: JsonPropertyName("labels")] List<string> Labels,
    [property: JsonPropertyName("datasets")] List<ChartDataset> Datasets);

/// <summary>
/// Represents a single dataset within a chart.
/// </summary>
/// <param name="Name">The display name for this dataset (used in legends).</param>
/// <param name="Values">The numeric values aligned with the parent chart's labels.</param>
public sealed record ChartDataset(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("values")] List<double> Values);
