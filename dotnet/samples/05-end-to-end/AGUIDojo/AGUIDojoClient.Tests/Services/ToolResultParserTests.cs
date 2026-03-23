using System.Text.Json;
using AGUIDojoClient.Models;
using AGUIDojoClient.Services;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Tests.Services;

public sealed class ToolResultParserTests
{
    [Fact]
    public void TryParseToolResult_ParsesStructuredMermaidPayload()
    {
        FunctionResultContent result = new(
            callId: "diagram-1",
            result: JsonSerializer.SerializeToElement(new
            {
                title = "Order Flow",
                definition = "flowchart TD\nA[Start] --> B[Done]"
            }));

        MermaidResult? parsed = ToolResultParser.TryParseToolResult("show_mermaid", result) as MermaidResult;

        Assert.NotNull(parsed);
        Assert.Equal("Order Flow", parsed.Title);
        Assert.Contains("flowchart TD", parsed.Definition, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParseToolResult_ParsesRawMermaidSource()
    {
        FunctionResultContent result = new(
            callId: "diagram-2",
            result: "sequenceDiagram\nAlice->>Bob: Hello");

        MermaidResult? parsed = ToolResultParser.TryParseToolResult("render_mermaid", result) as MermaidResult;

        Assert.NotNull(parsed);
        Assert.Equal("Diagram", parsed.Title);
        Assert.Contains("sequenceDiagram", parsed.Definition, StringComparison.Ordinal);
    }
}
