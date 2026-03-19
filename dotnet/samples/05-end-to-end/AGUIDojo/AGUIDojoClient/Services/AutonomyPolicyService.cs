// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

namespace AGUIDojoClient.Services;

/// <summary>
/// Applies the governance policy for when an approval can be auto-decided.
/// </summary>
public interface IAutonomyPolicyService
{
    /// <summary>
    /// Determines whether a tool approval should be resolved automatically.
    /// </summary>
    bool ShouldAutoDecide(AutonomyLevel autonomy, RiskLevel risk);
}

/// <summary>
/// Default autonomy policy used by the chat streaming loop.
/// </summary>
public sealed class AutonomyPolicyService : IAutonomyPolicyService
{
    /// <inheritdoc />
    public bool ShouldAutoDecide(AutonomyLevel autonomy, RiskLevel risk) => autonomy switch
    {
        AutonomyLevel.Suggest => false,
        AutonomyLevel.AutoReview => risk <= RiskLevel.Low,
        AutonomyLevel.FullAuto => risk < RiskLevel.Critical,
        _ => false,
    };
}
