using System.ComponentModel;

namespace AGUIDojoServer.Tools;

/// <summary>
/// AI tool that generates tabular data for the <c>show_data_grid</c> generative UI pattern.
/// </summary>
/// <remarks>
/// This tool demonstrates the tool-based generative UI pattern where the AI calls a tool
/// whose result is rendered as a rich UI component (a data grid / table) on the client side.
/// The client's <c>DataGridDisplay</c> component renders the returned <see cref="DataGridResult"/>.
/// </remarks>
internal static class DataGridTool
{
    /// <summary>
    /// The simulated delay in milliseconds to demonstrate tool call progress in the UI.
    /// </summary>
    private const int SimulatedDelayMs = 1000;

    /// <summary>
    /// Generates sample tabular data based on the requested topic.
    /// </summary>
    /// <param name="topic">The topic or category of data to generate (e.g., "products", "employees", "cities").</param>
    /// <param name="maxRows">The maximum number of rows to include. Defaults to 5.</param>
    /// <returns>A <see cref="DataGridResult"/> containing columns and rows of sample data.</returns>
    [Description("Show tabular data in a rich data grid. Use this when the user asks for structured data, comparisons, lists, or tables.")]
    public static async Task<DataGridResult> ShowDataGridAsync(
        [Description("The topic or category of data to display (e.g., 'products', 'employees', 'cities').")] string topic,
        [Description("The maximum number of rows to include (default: 5).")] int maxRows = 5)
    {
        // Add artificial delay to demonstrate tool call progress in the UI
        await Task.Delay(SimulatedDelayMs);

        // Clamp rows to a reasonable range
        maxRows = Math.Clamp(maxRows, 1, 20);

        return topic.ToUpperInvariant() switch
        {
            var t when t.Contains("PRODUCT", StringComparison.Ordinal) => GenerateProductData(maxRows),
            var t when t.Contains("EMPLOYEE", StringComparison.Ordinal) || t.Contains("TEAM", StringComparison.Ordinal) || t.Contains("STAFF", StringComparison.Ordinal) => GenerateEmployeeData(maxRows),
            var t when t.Contains("CITY", StringComparison.Ordinal) || t.Contains("CITIES", StringComparison.Ordinal) => GenerateCityData(maxRows),
            var t when t.Contains("ORDER", StringComparison.Ordinal) || t.Contains("SALES", StringComparison.Ordinal) => GenerateOrderData(maxRows),
            _ => GenerateProductData(maxRows) // Default to products
        };
    }

    private static DataGridResult GenerateProductData(int maxRows)
    {
        List<Dictionary<string, string>> rows =
        [
            new() { ["Name"] = "Wireless Headphones", ["Category"] = "Electronics", ["Price"] = "$79.99", ["Stock"] = "234", ["Rating"] = "4.5" },
            new() { ["Name"] = "Ergonomic Keyboard", ["Category"] = "Electronics", ["Price"] = "$129.99", ["Stock"] = "89", ["Rating"] = "4.7" },
            new() { ["Name"] = "Standing Desk Mat", ["Category"] = "Office", ["Price"] = "$39.99", ["Stock"] = "456", ["Rating"] = "4.2" },
            new() { ["Name"] = "USB-C Hub", ["Category"] = "Electronics", ["Price"] = "$49.99", ["Stock"] = "312", ["Rating"] = "4.4" },
            new() { ["Name"] = "Desk Lamp LED", ["Category"] = "Office", ["Price"] = "$34.99", ["Stock"] = "178", ["Rating"] = "4.6" },
            new() { ["Name"] = "Webcam HD Pro", ["Category"] = "Electronics", ["Price"] = "$89.99", ["Stock"] = "67", ["Rating"] = "4.3" },
            new() { ["Name"] = "Notebook Journal", ["Category"] = "Office", ["Price"] = "$12.99", ["Stock"] = "523", ["Rating"] = "4.8" },
            new() { ["Name"] = "Cable Organizer", ["Category"] = "Accessories", ["Price"] = "$15.99", ["Stock"] = "890", ["Rating"] = "4.1" },
        ];

        return new DataGridResult(
            Title: "Product Inventory",
            Columns: ["Name", "Category", "Price", "Stock", "Rating"],
            Rows: rows.Take(maxRows).ToList());
    }

    private static DataGridResult GenerateEmployeeData(int maxRows)
    {
        List<Dictionary<string, string>> rows =
        [
            new() { ["Name"] = "Alice Chen", ["Department"] = "Engineering", ["Role"] = "Senior Dev", ["Location"] = "Seattle", ["Years"] = "5" },
            new() { ["Name"] = "Bob Martinez", ["Department"] = "Design", ["Role"] = "UX Lead", ["Location"] = "New York", ["Years"] = "3" },
            new() { ["Name"] = "Carol Williams", ["Department"] = "Engineering", ["Role"] = "Staff Engineer", ["Location"] = "Remote", ["Years"] = "8" },
            new() { ["Name"] = "David Kim", ["Department"] = "Product", ["Role"] = "PM", ["Location"] = "San Francisco", ["Years"] = "4" },
            new() { ["Name"] = "Eva Johansson", ["Department"] = "Engineering", ["Role"] = "Dev", ["Location"] = "Stockholm", ["Years"] = "2" },
            new() { ["Name"] = "Frank Nakamura", ["Department"] = "QA", ["Role"] = "QA Lead", ["Location"] = "Tokyo", ["Years"] = "6" },
            new() { ["Name"] = "Grace Liu", ["Department"] = "Engineering", ["Role"] = "Principal Dev", ["Location"] = "Seattle", ["Years"] = "10" },
            new() { ["Name"] = "Henry Patel", ["Department"] = "Data Science", ["Role"] = "ML Engineer", ["Location"] = "Bangalore", ["Years"] = "3" },
        ];

        return new DataGridResult(
            Title: "Team Directory",
            Columns: ["Name", "Department", "Role", "Location", "Years"],
            Rows: rows.Take(maxRows).ToList());
    }

    private static DataGridResult GenerateCityData(int maxRows)
    {
        List<Dictionary<string, string>> rows =
        [
            new() { ["City"] = "Tokyo", ["Country"] = "Japan", ["Population"] = "13.96M", ["Area"] = "2,194 km²", ["Timezone"] = "JST" },
            new() { ["City"] = "New York", ["Country"] = "USA", ["Population"] = "8.34M", ["Area"] = "783 km²", ["Timezone"] = "EST" },
            new() { ["City"] = "London", ["Country"] = "UK", ["Population"] = "8.98M", ["Area"] = "1,572 km²", ["Timezone"] = "GMT" },
            new() { ["City"] = "Paris", ["Country"] = "France", ["Population"] = "2.16M", ["Area"] = "105 km²", ["Timezone"] = "CET" },
            new() { ["City"] = "Sydney", ["Country"] = "Australia", ["Population"] = "5.31M", ["Area"] = "12,368 km²", ["Timezone"] = "AEST" },
            new() { ["City"] = "São Paulo", ["Country"] = "Brazil", ["Population"] = "12.33M", ["Area"] = "1,521 km²", ["Timezone"] = "BRT" },
        ];

        return new DataGridResult(
            Title: "World Cities",
            Columns: ["City", "Country", "Population", "Area", "Timezone"],
            Rows: rows.Take(maxRows).ToList());
    }

    private static DataGridResult GenerateOrderData(int maxRows)
    {
        List<Dictionary<string, string>> rows =
        [
            new() { ["Order ID"] = "ORD-1042", ["Customer"] = "Acme Corp", ["Total"] = "$2,450.00", ["Status"] = "Shipped", ["Date"] = "2026-02-01" },
            new() { ["Order ID"] = "ORD-1043", ["Customer"] = "TechStart Inc", ["Total"] = "$890.50", ["Status"] = "Processing", ["Date"] = "2026-02-03" },
            new() { ["Order ID"] = "ORD-1044", ["Customer"] = "Global Solutions", ["Total"] = "$5,200.00", ["Status"] = "Delivered", ["Date"] = "2026-01-28" },
            new() { ["Order ID"] = "ORD-1045", ["Customer"] = "Vertex Labs", ["Total"] = "$1,375.25", ["Status"] = "Shipped", ["Date"] = "2026-02-05" },
            new() { ["Order ID"] = "ORD-1046", ["Customer"] = "Nova Design", ["Total"] = "$620.00", ["Status"] = "Pending", ["Date"] = "2026-02-07" },
            new() { ["Order ID"] = "ORD-1047", ["Customer"] = "Pinnacle Co", ["Total"] = "$3,100.75", ["Status"] = "Processing", ["Date"] = "2026-02-06" },
        ];

        return new DataGridResult(
            Title: "Recent Orders",
            Columns: ["Order ID", "Customer", "Total", "Status", "Date"],
            Rows: rows.Take(maxRows).ToList());
    }
}
