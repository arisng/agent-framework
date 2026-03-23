namespace AGUIDojoClient.Models;

/// <summary>
/// Records an approval decision for the audit trail.
/// Captures what was decided, by whom (human or auto), and the context.
/// </summary>
public sealed class AuditEntry
{
    /// <summary>Unique identifier for this audit entry.</summary>
    public required string Id { get; init; }

    /// <summary>The approval ID from the server request.</summary>
    public required string ApprovalId { get; init; }

    /// <summary>Name of the function that required approval.</summary>
    public required string FunctionName { get; init; }

    /// <summary>Risk level assessed for this function call.</summary>
    public RiskLevel RiskLevel { get; init; }

    /// <summary>The autonomy level that was active when the decision was made.</summary>
    public AutonomyLevel AutonomyLevel { get; init; }

    /// <summary>Whether the call was approved or rejected.</summary>
    public bool WasApproved { get; init; }

    /// <summary>Whether the decision was made automatically (true) or by a human (false).</summary>
    public bool WasAutoDecided { get; init; }

    /// <summary>When the decision was made.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Session that owns this audit entry.</summary>
    public required string SessionId { get; init; }
}
