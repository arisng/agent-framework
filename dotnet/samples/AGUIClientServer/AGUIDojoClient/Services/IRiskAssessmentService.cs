// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

namespace AGUIDojoClient.Services;

/// <summary>
/// Interface for assessing risk levels of agent function calls.
/// Maps function names to <see cref="RiskLevel"/> values and provides
/// human-readable descriptions for governance UI components.
/// </summary>
/// <remarks>
/// This service is stateless and registered as a Singleton.
/// Risk mappings are deterministic based on function name pattern matching.
/// </remarks>
public interface IRiskAssessmentService
{
    /// <summary>
    /// Assesses the risk level of a function call based on its name.
    /// Uses pattern matching to classify known functions; unknown functions
    /// default to <see cref="RiskLevel.Medium"/>.
    /// </summary>
    /// <param name="functionName">The name of the function/tool the agent wants to execute.</param>
    /// <returns>The assessed <see cref="RiskLevel"/> for the function.</returns>
    RiskLevel AssessRisk(string functionName);

    /// <summary>
    /// Returns a human-readable description for a given risk level.
    /// Used by governance UI components (e.g., <c>RiskBadge</c>, <c>ApprovalQueue</c>)
    /// to display contextual risk information to the user.
    /// </summary>
    /// <param name="level">The risk level to describe.</param>
    /// <returns>A human-readable description of the risk level.</returns>
    string GetRiskDescription(RiskLevel level);
}
