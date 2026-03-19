// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;
using AGUIDojoClient.Services;

namespace AGUIDojoClient.Tests.Services;

public sealed class AutonomyPolicyServiceTests
{
    private static readonly AutonomyPolicyService Policy = new();

    [Theory]
    [InlineData(AutonomyLevel.Suggest, RiskLevel.Low, false)]
    [InlineData(AutonomyLevel.Suggest, RiskLevel.Medium, false)]
    [InlineData(AutonomyLevel.Suggest, RiskLevel.High, false)]
    [InlineData(AutonomyLevel.Suggest, RiskLevel.Critical, false)]
    [InlineData(AutonomyLevel.AutoReview, RiskLevel.Low, true)]
    [InlineData(AutonomyLevel.AutoReview, RiskLevel.Medium, false)]
    [InlineData(AutonomyLevel.AutoReview, RiskLevel.High, false)]
    [InlineData(AutonomyLevel.AutoReview, RiskLevel.Critical, false)]
    [InlineData(AutonomyLevel.FullAuto, RiskLevel.Low, true)]
    [InlineData(AutonomyLevel.FullAuto, RiskLevel.Medium, true)]
    [InlineData(AutonomyLevel.FullAuto, RiskLevel.High, true)]
    [InlineData(AutonomyLevel.FullAuto, RiskLevel.Critical, false)]
    public void ShouldAutoDecide_UsesExpectedPolicyMatrix(AutonomyLevel autonomyLevel, RiskLevel riskLevel, bool expected)
    {
        bool result = Policy.ShouldAutoDecide(autonomyLevel, riskLevel);

        Assert.Equal(expected, result);
    }
}
