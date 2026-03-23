using System.Text.Json;
using AGUIDojoClient.Components.Governance;
using AGUIDojoClient.Models;
using AGUIDojoClient.Tests.Infrastructure;

namespace AGUIDojoClient.Tests.Components.Governance;

public sealed class ApprovalQueueTests : AGUIDojoClientComponentTestBase
{
    [Fact]
    public void Render_WithArguments_ShowsFunctionNameRiskAndFormattedValues()
    {
        Dictionary<string, object?> arguments = new()
        {
            ["city"] = CreateJsonElement("\"Seattle\""),
            ["enabled"] = CreateJsonElement("true"),
            ["note"] = null,
        };

        IRenderedComponent<ApprovalQueue> component = RenderApprovalQueue(arguments, _ => { });

        Assert.Contains("Approval Required", component.Markup);
        Assert.Contains("search_documents", component.Markup);
        Assert.Contains("Seattle", component.Markup);
        Assert.Contains("true", component.Markup);
        Assert.Contains("(null)", component.Markup);
        Assert.Contains("High", component.Markup);
    }

    [Fact]
    public void ApproveClick_InvokesDecisionAndPreventsDuplicateSubmissions()
    {
        List<bool> decisions = [];
        IRenderedComponent<ApprovalQueue> component = RenderApprovalQueue(new Dictionary<string, object?>(), approved => decisions.Add(approved));

        var approveButton = component.FindAll("button").Single(button => button.TextContent.Contains("Approve", StringComparison.Ordinal));
        approveButton.Click();
        approveButton.Click();

        Assert.Equal([true], decisions);
    }

    [Fact]
    public void RejectClick_InvokesDecisionWithFalse()
    {
        List<bool> decisions = [];
        IRenderedComponent<ApprovalQueue> component = RenderApprovalQueue(new Dictionary<string, object?>(), approved => decisions.Add(approved));

        component.FindAll("button").Single(button => button.TextContent.Contains("Reject", StringComparison.Ordinal)).Click();

        Assert.Equal([false], decisions);
    }

    private IRenderedComponent<ApprovalQueue> RenderApprovalQueue(IDictionary<string, object?> arguments, Action<bool> onDecision)
    {
        return Render<ApprovalQueue>(parameters => parameters
            .Add(queue => queue.FunctionName, "search_documents")
            .Add(queue => queue.Arguments, arguments)
            .Add(queue => queue.RiskLevel, RiskLevel.High)
            .Add(queue => queue.OnDecision, EventCallback.Factory.Create<bool>(this, onDecision)));
    }

    private static JsonElement CreateJsonElement(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
