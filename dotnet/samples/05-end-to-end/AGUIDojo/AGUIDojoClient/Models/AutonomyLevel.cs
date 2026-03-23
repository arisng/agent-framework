namespace AGUIDojoClient.Models;

/// <summary>
/// Defines the level of autonomy the agent has when executing tool calls.
/// Controls whether approval requests are presented to the user or auto-resolved.
/// </summary>
public enum AutonomyLevel
{
    /// <summary>
    /// All tool calls require explicit human approval. Default HITL behavior.
    /// </summary>
    Suggest,

    /// <summary>
    /// Low-risk tool calls are auto-approved with a notification toast.
    /// Medium and high-risk calls still require human approval.
    /// </summary>
    AutoReview,

    /// <summary>
    /// All tool calls are auto-approved except Critical risk.
    /// Decisions are logged to the audit trail silently.
    /// </summary>
    FullAuto,
}
