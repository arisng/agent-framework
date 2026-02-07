// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoClient.Models;

namespace AGUIDojoClient.Services;

/// <summary>
/// Maps agent function names to risk levels using pattern matching.
/// Provides deterministic risk classification for governance UI components
/// such as <c>RiskBadge</c> and <c>ApprovalQueue</c>.
/// </summary>
/// <remarks>
/// <para>
/// This service is stateless and registered as a Singleton. Risk assessment
/// is based on function name pattern matching with the following rules:
/// </para>
/// <list type="bullet">
/// <item><description>"request_approval" → <see cref="RiskLevel.High"/></description></item>
/// <item><description>"get_weather" → <see cref="RiskLevel.Low"/></description></item>
/// <item><description>"search", "search_documents" → <see cref="RiskLevel.Low"/></description></item>
/// <item><description>Unknown functions → <see cref="RiskLevel.Medium"/></description></item>
/// </list>
/// </remarks>
public sealed class RiskAssessmentService : IRiskAssessmentService
{
    /// <inheritdoc />
    public RiskLevel AssessRisk(string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        if (string.Equals(functionName, "request_approval", StringComparison.OrdinalIgnoreCase))
        {
            return RiskLevel.High;
        }

        if (string.Equals(functionName, "get_weather", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "search", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(functionName, "search_documents", StringComparison.OrdinalIgnoreCase))
        {
            return RiskLevel.Low;
        }

        return RiskLevel.Medium;
    }

    /// <inheritdoc />
    public string GetRiskDescription(RiskLevel level)
    {
        return level switch
        {
            RiskLevel.Low => "Routine operation with no significant side effects.",
            RiskLevel.Medium => "Operation that modifies state but is likely reversible.",
            RiskLevel.High => "Operation with significant consequences that requires careful review.",
            RiskLevel.Critical => "Irreversible operation requiring immediate attention.",
            _ => "Unknown risk level."
        };
    }
}
