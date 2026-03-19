// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;
using AGUIDojoClient.Services;

namespace AGUIDojoClient.Tests.Services;

public sealed class RiskAssessmentServiceTests
{
    private static readonly RiskAssessmentService Service = new();

    [Theory]
    [InlineData("request_approval", RiskLevel.High)]
    [InlineData("GET_WEATHER", RiskLevel.Low)]
    [InlineData("search", RiskLevel.Low)]
    [InlineData("search_documents", RiskLevel.Low)]
    [InlineData("custom_operation", RiskLevel.Medium)]
    public void AssessRisk_MapsFunctionNamesToExpectedLevels(string functionName, RiskLevel expected)
    {
        RiskLevel risk = Service.AssessRisk(functionName);

        Assert.Equal(expected, risk);
    }

    [Theory]
    [InlineData(RiskLevel.Low, "Routine operation")]
    [InlineData(RiskLevel.Medium, "modifies state")]
    [InlineData(RiskLevel.High, "significant consequences")]
    [InlineData(RiskLevel.Critical, "Irreversible operation")]
    public void GetRiskDescription_ReturnsHelpfulCopy(RiskLevel level, string expectedSnippet)
    {
        string description = Service.GetRiskDescription(level);

        Assert.Contains(expectedSnippet, description, StringComparison.OrdinalIgnoreCase);
    }
}
