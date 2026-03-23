using System.ComponentModel;

namespace AGUIDojoServer.Tools;

/// <summary>
/// AI tool that generates chart data for the <c>show_chart</c> generative UI pattern.
/// </summary>
/// <remarks>
/// This tool demonstrates the tool-based generative UI pattern where the AI calls a tool
/// whose result is rendered as a rich UI component (a chart) on the client side.
/// The client's <c>ChartDisplay</c> component renders the returned <see cref="ChartResult"/>.
/// </remarks>
internal static class ChartTool
{
    /// <summary>
    /// The simulated delay in milliseconds to demonstrate tool call progress in the UI.
    /// </summary>
    private const int SimulatedDelayMs = 1000;

    /// <summary>
    /// Generates sample chart data based on the requested topic and chart type.
    /// </summary>
    /// <param name="topic">The topic or category of data to chart (e.g., "revenue", "temperature", "population").</param>
    /// <param name="chartType">The type of chart to generate: "bar", "line", "pie", or "area". Defaults to "bar".</param>
    /// <returns>A <see cref="ChartResult"/> containing labels, datasets, and chart metadata.</returns>
    [Description("Show data as a chart visualization. Use this when the user asks for trends, comparisons, distributions, or visual data representations.")]
    public static async Task<ChartResult> ShowChartAsync(
        [Description("The topic or category of data to visualize (e.g., 'revenue', 'temperature', 'population').")] string topic,
        [Description("The type of chart: 'bar', 'line', 'pie', or 'area'. Defaults to 'bar'.")] string chartType = "bar")
    {
        // Add artificial delay to demonstrate tool call progress in the UI
        await Task.Delay(SimulatedDelayMs);

        // Normalize chart type
        chartType = chartType.ToUpperInvariant() switch
        {
            "LINE" => "line",
            "PIE" => "pie",
            "AREA" => "area",
            _ => "bar"
        };

        return topic.ToUpperInvariant() switch
        {
            var t when t.Contains("REVENUE", StringComparison.Ordinal) || t.Contains("SALES", StringComparison.Ordinal) => GenerateRevenueData(chartType),
            var t when t.Contains("TEMPERATURE", StringComparison.Ordinal) || t.Contains("WEATHER", StringComparison.Ordinal) => GenerateTemperatureData(chartType),
            var t when t.Contains("POPULATION", StringComparison.Ordinal) || t.Contains("GROWTH", StringComparison.Ordinal) => GeneratePopulationData(chartType),
            var t when t.Contains("MARKET", StringComparison.Ordinal) || t.Contains("SHARE", StringComparison.Ordinal) => GenerateMarketShareData("pie"),
            _ => GenerateRevenueData(chartType) // Default to revenue
        };
    }

    private static ChartResult GenerateRevenueData(string chartType)
    {
        return new ChartResult(
            Title: "Quarterly Revenue",
            ChartType: chartType,
            Labels: ["Q1 2025", "Q2 2025", "Q3 2025", "Q4 2025", "Q1 2026"],
            Datasets:
            [
                new ChartDataset("Product A", [120000, 145000, 162000, 178000, 195000]),
                new ChartDataset("Product B", [85000, 92000, 88000, 105000, 115000]),
                new ChartDataset("Product C", [45000, 52000, 68000, 72000, 80000]),
            ]);
    }

    private static ChartResult GenerateTemperatureData(string chartType)
    {
        return new ChartResult(
            Title: "Monthly Average Temperature (°C)",
            ChartType: chartType == "pie" ? "line" : chartType, // Pie doesn't make sense for temperature
            Labels: ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
            Datasets:
            [
                new ChartDataset("Seattle", [5, 6, 9, 12, 16, 19, 22, 22, 18, 13, 8, 5]),
                new ChartDataset("New York", [-1, 1, 6, 12, 18, 23, 26, 25, 21, 14, 8, 2]),
            ]);
    }

    private static ChartResult GeneratePopulationData(string chartType)
    {
        return new ChartResult(
            Title: "City Population Growth (millions)",
            ChartType: chartType,
            Labels: ["2020", "2021", "2022", "2023", "2024", "2025"],
            Datasets:
            [
                new ChartDataset("Tokyo", [13.96, 13.96, 13.99, 14.01, 14.03, 14.05]),
                new ChartDataset("New York", [8.34, 8.31, 8.34, 8.26, 8.28, 8.30]),
                new ChartDataset("London", [8.98, 8.80, 8.87, 8.95, 9.00, 9.05]),
            ]);
    }

    private static ChartResult GenerateMarketShareData(string chartType)
    {
        return new ChartResult(
            Title: "Browser Market Share (%)",
            ChartType: chartType,
            Labels: ["Chrome", "Safari", "Edge", "Firefox", "Opera", "Other"],
            Datasets:
            [
                new ChartDataset("Market Share", [64.7, 18.6, 5.3, 3.1, 2.8, 5.5]),
            ]);
    }
}
