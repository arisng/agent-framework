// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Models;

/// <summary>
/// Risk level classification for agent actions.
/// Used to visually indicate the severity of an action requiring approval.
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Low risk — routine operations with no significant side effects.
    /// </summary>
    Low,

    /// <summary>
    /// Medium risk — operations that modify state but are easily reversible.
    /// </summary>
    Medium,

    /// <summary>
    /// High risk — operations that may have significant consequences.
    /// </summary>
    High,

    /// <summary>
    /// Critical risk — irreversible operations requiring immediate attention.
    /// </summary>
    Critical
}

/// <summary>
/// Status of an approval request in the governance queue.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Approval is pending user decision.
    /// </summary>
    Pending,

    /// <summary>
    /// Action has been approved by the user.
    /// </summary>
    Approved,

    /// <summary>
    /// Action has been rejected by the user.
    /// </summary>
    Rejected
}

/// <summary>
/// Represents an item in the non-modal approval queue.
/// Used by governance components to display pending agent actions for human review.
/// </summary>
public sealed class ApprovalItem
{
    /// <summary>
    /// Unique identifier for the approval item.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Name of the function/tool the agent wants to execute.
    /// </summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// Arguments the agent intends to pass to the function.
    /// </summary>
    public IDictionary<string, object?>? Arguments { get; set; }

    /// <summary>
    /// Assessed risk level of this action.
    /// </summary>
    public RiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Timestamp when the approval request was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Current status of the approval request.
    /// </summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
}
