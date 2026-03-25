using System.Collections.Immutable;
using System.Text.Json;
using AGUIDojoClient.Models;
using AGUIDojoClient.Services;

namespace AGUIDojoClient.Store.SessionManager;

internal static class SessionWorkspaceProjection
{
    public static SessionState ApplyState(SessionState state, ServerSessionWorkspace? workspace)
    {
        if (workspace is null)
        {
            return state;
        }

        ServerSessionWorkspaceSnapshot? snapshot = workspace.Snapshot;
        PendingApproval? pendingApproval = MapPendingApproval(workspace.Approvals);
        ImmutableList<AuditEntry> auditTrail = workspace.AuditEvents
            .Select(MapAuditEntry)
            .Where(entry => entry is not null)
            .Cast<AuditEntry>()
            .ToImmutableList();

        ImmutableHashSet<ArtifactType> visibleTabs = ImmutableHashSet<ArtifactType>.Empty;
        if (snapshot?.CurrentRecipe is not null)
        {
            visibleTabs = visibleTabs.Add(ArtifactType.RecipeEditor);
        }

        if (snapshot?.CurrentDocument is not null)
        {
            visibleTabs = visibleTabs.Add(ArtifactType.DocumentPreview);
        }

        if (snapshot?.CurrentDataGrid is not null)
        {
            visibleTabs = visibleTabs.Add(ArtifactType.DataGrid);
        }

        DiffPreviewData? diffPreview = CreateDiffPreview(snapshot);
        if (diffPreview is not null)
        {
            visibleTabs = visibleTabs.Add(ArtifactType.DiffPreview);
        }

        if (auditTrail.Count > 0)
        {
            visibleTabs = visibleTabs.Add(ArtifactType.AuditTrail);
        }

        return state with
        {
            PendingApproval = pendingApproval,
            Plan = snapshot?.CurrentPlan,
            CurrentRecipe = snapshot?.CurrentRecipe,
            CurrentDocumentState = snapshot?.CurrentDocument,
            IsDocumentPreview = snapshot?.CurrentDocument is not null ? snapshot.IsDocumentPreview : true,
            DiffPreview = diffPreview,
            CurrentDataGrid = snapshot?.CurrentDataGrid,
            AuditTrail = auditTrail,
            VisibleTabs = visibleTabs,
            HasInteractiveArtifact = visibleTabs.Count > 0,
            ActiveArtifactType = ResolveActiveArtifact(state.ActiveArtifactType, visibleTabs),
            ToolArtifacts = ImmutableList<ToolArtifactState>.Empty,
            ActiveToolArtifactId = null,
            PendingUndo = null,
        };
    }

    public static SessionMetadata ApplyMetadata(SessionMetadata metadata, ServerSessionWorkspace? workspace)
    {
        return metadata with
        {
            HasPendingApproval = workspace?.Approvals.Any(IsPendingApproval) == true,
        };
    }

    public static ServerSessionWorkspaceImportRequest? CreateImportRequest(SessionState state)
    {
        string? previousDocumentText = ExtractPreviousDocumentText(state);
        ServerSessionWorkspaceImportSnapshot? snapshot = null;
        if (state.Plan is not null ||
            state.CurrentRecipe is not null ||
            state.CurrentDocumentState is not null ||
            state.CurrentDataGrid is not null ||
            previousDocumentText is not null)
        {
            snapshot = new ServerSessionWorkspaceImportSnapshot
            {
                CurrentPlan = state.Plan,
                CurrentRecipe = state.CurrentRecipe,
                CurrentDocument = state.CurrentDocumentState,
                PreviousDocumentText = previousDocumentText,
                IsDocumentPreview = state.CurrentDocumentState is not null && state.IsDocumentPreview,
                CurrentDataGrid = state.CurrentDataGrid,
            };
        }

        ServerSessionPendingApprovalImport? pendingApproval = null;
        if (state.PendingApproval is not null)
        {
            pendingApproval = new ServerSessionPendingApprovalImport
            {
                ApprovalId = state.PendingApproval.ApprovalId,
                FunctionName = state.PendingApproval.FunctionName,
                Message = state.PendingApproval.Message,
                FunctionArgumentsJson = state.PendingApproval.FunctionArguments?.GetRawText(),
                OriginalCallId = state.PendingApproval.OriginalCallId,
                RequestedAt = DateTimeOffset.UtcNow,
            };
        }

        List<ServerSessionAuditImportEntry> auditEntries = state.AuditTrail
            .Select(entry => new ServerSessionAuditImportEntry
            {
                Id = entry.Id,
                EventType = "ApprovalResolved",
                Title = entry.WasApproved ? $"Approval approved: {entry.FunctionName}" : $"Approval rejected: {entry.FunctionName}",
                Summary = entry.WasApproved ? "The approval request was approved." : "The approval request was rejected.",
                OccurredAt = new DateTimeOffset(entry.Timestamp, TimeSpan.Zero),
                ApprovalId = entry.ApprovalId,
                FunctionName = entry.FunctionName,
                RiskLevel = entry.RiskLevel.ToString(),
                AutonomyLevel = entry.AutonomyLevel.ToString(),
                WasApproved = entry.WasApproved,
                WasAutoDecided = entry.WasAutoDecided,
            })
            .ToList();

        if (snapshot is null && pendingApproval is null && auditEntries.Count == 0)
        {
            return null;
        }

        return new ServerSessionWorkspaceImportRequest
        {
            Snapshot = snapshot,
            PendingApproval = pendingApproval,
            AuditEntries = auditEntries,
        };
    }

    private static PendingApproval? MapPendingApproval(IEnumerable<ServerSessionApprovalRecord> approvals)
    {
        ServerSessionApprovalRecord? pendingApproval = approvals
            .Where(IsPendingApproval)
            .OrderByDescending(approval => approval.RequestedAt)
            .FirstOrDefault();

        if (pendingApproval is null || string.IsNullOrWhiteSpace(pendingApproval.OriginalCallId))
        {
            return null;
        }

        return new PendingApproval
        {
            ApprovalId = pendingApproval.ApprovalId,
            FunctionName = pendingApproval.FunctionName,
            FunctionArguments = ParseJsonElement(pendingApproval.FunctionArgumentsJson),
            Message = string.IsNullOrWhiteSpace(pendingApproval.Message)
                ? $"Approve execution of '{pendingApproval.FunctionName}'?"
                : pendingApproval.Message,
            OriginalCallId = pendingApproval.OriginalCallId,
        };
    }

    private static bool IsPendingApproval(ServerSessionApprovalRecord approval)
        => string.Equals(approval.Status, "Pending", StringComparison.OrdinalIgnoreCase);

    private static AuditEntry? MapAuditEntry(ServerSessionAuditEvent auditEvent)
    {
        if (string.IsNullOrWhiteSpace(auditEvent.ApprovalId) ||
            string.IsNullOrWhiteSpace(auditEvent.FunctionName) ||
            auditEvent.WasApproved is null)
        {
            return null;
        }

        return new AuditEntry
        {
            Id = auditEvent.Id,
            ApprovalId = auditEvent.ApprovalId,
            FunctionName = auditEvent.FunctionName,
            RiskLevel = ParseEnum(auditEvent.RiskLevel, RiskLevel.Medium),
            AutonomyLevel = ParseEnum(auditEvent.AutonomyLevel, AutonomyLevel.Suggest),
            WasApproved = auditEvent.WasApproved.Value,
            WasAutoDecided = auditEvent.WasAutoDecided ?? false,
            Timestamp = auditEvent.OccurredAt.UtcDateTime,
            SessionId = string.Empty,
        };
    }

    private static DiffPreviewData? CreateDiffPreview(ServerSessionWorkspaceSnapshot? snapshot)
    {
        if (snapshot?.CurrentDocument is null || string.IsNullOrWhiteSpace(snapshot.PreviousDocumentText))
        {
            return null;
        }

        return new DiffPreviewData(snapshot.PreviousDocumentText, snapshot.CurrentDocument.Document, "Document Changes");
    }

    private static string? ExtractPreviousDocumentText(SessionState state)
    {
        if (state.CurrentDocumentState is null || state.DiffPreview is null)
        {
            return null;
        }

        return state.DiffPreview.Before switch
        {
            string text => text,
            DocumentState document => document.Document,
            _ => null,
        };
    }

    private static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;

    private static ArtifactType ResolveActiveArtifact(ArtifactType current, ImmutableHashSet<ArtifactType> visibleTabs)
    {
        if (current != ArtifactType.None && visibleTabs.Contains(current))
        {
            return current;
        }

        ArtifactType[] preferredOrder =
        [
            ArtifactType.DocumentPreview,
            ArtifactType.DataGrid,
            ArtifactType.RecipeEditor,
            ArtifactType.DiffPreview,
            ArtifactType.AuditTrail,
        ];

        return preferredOrder.FirstOrDefault(visibleTabs.Contains, ArtifactType.None);
    }
}
