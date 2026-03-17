// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIDojoServer.Tools;

/// <summary>
/// Represents tabular data returned by the <c>show_data_grid</c> AI tool.
/// </summary>
/// <param name="Title">A human-readable title for the data grid.</param>
/// <param name="Columns">The ordered list of column header names.</param>
/// <param name="Rows">
/// The row data as a list of dictionaries, where each dictionary maps column names to cell values.
/// </param>
public sealed record DataGridResult(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("columns")] List<string> Columns,
    [property: JsonPropertyName("rows")] List<Dictionary<string, string>> Rows);
