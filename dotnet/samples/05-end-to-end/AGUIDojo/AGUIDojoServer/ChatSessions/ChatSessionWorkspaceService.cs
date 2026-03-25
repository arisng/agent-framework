using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AGUIDojoServer.AgenticUI;
using AGUIDojoServer.Data;
using AGUIDojoServer.HumanInTheLoop;
using AGUIDojoServer.PredictiveStateUpdates;
using AGUIDojoServer.SharedState;
using AGUIDojoServer.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer.ChatSessions;

internal sealed partial class ChatSessionWorkspaceService(ChatSessionsDbContext db)
{
    private const int MaxConcurrencyRetries = 2;

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task RefreshDerivedStateAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        for (int attempt = 0; ; attempt++)
        {
            db.ChangeTracker.Clear();

            try
            {
                await RefreshDerivedStateOnceAsync(sessionId, ct);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
            }
        }
    }

    public async Task<ChatSessionWorkspaceDto?> GetWorkspaceAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        bool exists = await db.ChatSessions.AnyAsync(item => item.Id == sessionId, ct);
        if (!exists)
        {
            return null;
        }

        ChatSessionWorkspaceSnapshotDto? snapshot = null;
        ChatSessionWorkspaceSnapshot? snapshotEntity = await db.ChatSessionWorkspaceSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.SessionId == sessionId, ct);

        if (snapshotEntity is not null)
        {
            snapshot = DeserializeSnapshot(snapshotEntity.SnapshotJson, snapshotEntity.UpdatedAt);
        }

        List<ChatSessionApprovalRecordDto> approvals = await db.ChatSessionApprovalRecords
            .AsNoTracking()
            .Where(item => item.SessionId == sessionId)
            .OrderBy(item => item.RequestedAt)
            .Select(item => new ChatSessionApprovalRecordDto
            {
                ApprovalId = item.ApprovalId,
                FunctionName = item.FunctionName,
                Message = item.Message,
                FunctionArgumentsJson = item.FunctionArgumentsJson,
                OriginalCallId = item.OriginalCallId,
                Status = item.Status.ToString(),
                RequestedAt = item.RequestedAt,
                ResolvedAt = item.ResolvedAt,
                RequestNodeId = item.RequestNodeId,
                ResponseNodeId = item.ResponseNodeId,
                ResolutionSource = item.ResolutionSource,
            })
            .ToListAsync(ct);

        List<ChatSessionAuditEvent> auditEntities = await db.ChatSessionAuditEvents
            .AsNoTracking()
            .Where(item => item.SessionId == sessionId)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .ToListAsync(ct);

        return new ChatSessionWorkspaceDto
        {
            Snapshot = snapshot,
            Approvals = approvals,
            AuditEvents = auditEntities.ConvertAll(MapAuditEvent),
        };
    }

    public async Task<ChatSessionWorkspaceSummary> GetWorkspaceSummaryAsync(string sessionId, CancellationToken ct = default)
    {
        ChatSessionWorkspaceDto? workspace = await GetWorkspaceAsync(sessionId, ct);
        if (workspace is null)
        {
            return new ChatSessionWorkspaceSummary(0, 0, 0, 0, 0, null, null);
        }

        int artifactCount = 0;
        int fileReferenceCount = 0;
        string? latestEffectiveModelId = null;
        DateTimeOffset? latestCompactionAt = null;

        if (workspace.Snapshot is { } snapshot)
        {
            if (snapshot.CurrentPlan is not null)
            {
                artifactCount++;
            }

            if (snapshot.CurrentRecipe is not null)
            {
                artifactCount++;
            }

            if (snapshot.CurrentDocument is not null)
            {
                artifactCount++;
            }

            if (snapshot.CurrentDataGrid is not null)
            {
                artifactCount++;
            }

            fileReferenceCount = snapshot.FileReferences.Count;
        }

        foreach (ChatSessionAuditEventDto auditEvent in workspace.AuditEvents)
        {
            if (!string.IsNullOrWhiteSpace(auditEvent.EffectiveModelId))
            {
                latestEffectiveModelId = auditEvent.EffectiveModelId;
            }

            if (string.Equals(auditEvent.EventType, ChatAuditEventType.CompactionCheckpoint.ToString(), StringComparison.Ordinal))
            {
                latestCompactionAt = auditEvent.OccurredAt;
            }
        }

        return new ChatSessionWorkspaceSummary(
            workspace.Approvals.Count,
            workspace.Approvals.Count(item => string.Equals(item.Status, ChatApprovalStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase)),
            workspace.AuditEvents.Count,
            artifactCount,
            fileReferenceCount,
            latestEffectiveModelId,
            latestCompactionAt);
    }

    public async Task ImportWorkspaceAsync(string sessionId, ChatSessionWorkspaceImportRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(request);

        for (int attempt = 0; ; attempt++)
        {
            db.ChangeTracker.Clear();

            try
            {
                await ImportWorkspaceOnceAsync(sessionId, request, ct);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
            }
        }
    }

    private async Task RefreshDerivedStateOnceAsync(string sessionId, CancellationToken ct)
    {
        ChatSession? session = await db.ChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sessionId, ct);

        if (session is null)
        {
            return;
        }

        List<ChatConversationNode> allNodes = await db.ChatConversationNodes
            .AsNoTracking()
            .Where(item => item.SessionId == sessionId)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.SiblingOrder)
            .ToListAsync(ct);

        WorkspaceDerivationResult derived = DeriveWorkspace(session, allNodes);

        await UpsertSnapshotAsync(sessionId, derived.Snapshot, WorkspaceSnapshotSources.Derived, ct);
        await UpsertApprovalRecordsAsync(sessionId, derived.Approvals, ct);
        await UpsertAuditEventsAsync(sessionId, derived.DerivedAuditEvents, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task ImportWorkspaceOnceAsync(string sessionId, ChatSessionWorkspaceImportRequest request, CancellationToken ct)
    {
        bool exists = await db.ChatSessions.AnyAsync(item => item.Id == sessionId, ct);
        if (!exists)
        {
            throw new InvalidOperationException($"Chat session '{sessionId}' was not found.");
        }

        if (request.Snapshot is not null)
        {
            ChatSessionWorkspaceSnapshot? existingSnapshotEntity = await db.ChatSessionWorkspaceSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.SessionId == sessionId, ct);
            ChatSessionWorkspaceSnapshotDto? existingSnapshot = existingSnapshotEntity is not null
                ? DeserializeSnapshot(existingSnapshotEntity.SnapshotJson, existingSnapshotEntity.UpdatedAt)
                : null;

            ChatSessionWorkspaceSnapshotDto snapshot = new()
            {
                CurrentPlan = request.Snapshot.CurrentPlan ?? existingSnapshot?.CurrentPlan,
                CurrentRecipe = request.Snapshot.CurrentRecipe ?? existingSnapshot?.CurrentRecipe,
                CurrentDocument = request.Snapshot.CurrentDocument ?? existingSnapshot?.CurrentDocument,
                PreviousDocumentText = request.Snapshot.PreviousDocumentText ?? existingSnapshot?.PreviousDocumentText,
                IsDocumentPreview = request.Snapshot.CurrentDocument is not null
                    ? request.Snapshot.IsDocumentPreview
                    : existingSnapshot?.IsDocumentPreview ?? request.Snapshot.IsDocumentPreview,
                CurrentDataGrid = request.Snapshot.CurrentDataGrid ?? existingSnapshot?.CurrentDataGrid,
                FileReferences = request.Snapshot.FileReferences.Count > 0
                    ? request.Snapshot.FileReferences
                    : existingSnapshot?.FileReferences ?? [],
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            await UpsertSnapshotAsync(sessionId, snapshot, WorkspaceSnapshotSources.BrowserImport, ct);
        }

        if (request.PendingApproval is not null)
        {
            ChatSessionApprovalRecord? approval = await FindApprovalRecordAsync(
                sessionId,
                request.PendingApproval.ApprovalId,
                ct);

            if (approval is null)
            {
                db.ChatSessionApprovalRecords.Add(new ChatSessionApprovalRecord
                {
                    SessionId = sessionId,
                    ApprovalId = request.PendingApproval.ApprovalId,
                    FunctionName = request.PendingApproval.FunctionName,
                    Message = request.PendingApproval.Message,
                    FunctionArgumentsJson = request.PendingApproval.FunctionArgumentsJson,
                    OriginalCallId = request.PendingApproval.OriginalCallId,
                    RequestedAt = request.PendingApproval.RequestedAt,
                    Status = ChatApprovalStatus.Pending,
                    ResolutionSource = WorkspaceSnapshotSources.BrowserImport,
                });
            }
            else if (approval.Status == ChatApprovalStatus.Pending)
            {
                approval.FunctionName = request.PendingApproval.FunctionName;
                approval.Message = request.PendingApproval.Message;
                approval.FunctionArgumentsJson = request.PendingApproval.FunctionArgumentsJson;
                approval.OriginalCallId = request.PendingApproval.OriginalCallId;
                approval.RequestedAt = request.PendingApproval.RequestedAt;
                approval.ResolutionSource = WorkspaceSnapshotSources.BrowserImport;
                approval.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            }
        }

        foreach (ChatSessionAuditImportEntryDto auditEntry in request.AuditEntries)
        {
            ChatAuditEventType eventType = ParseAuditEventType(auditEntry.EventType);
            await UpsertImportedAuditEventAsync(sessionId, auditEntry, eventType, ct);

            if (string.Equals(auditEntry.EventType, ChatAuditEventType.ApprovalResolved.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(auditEntry.ApprovalId) &&
                !string.IsNullOrWhiteSpace(auditEntry.FunctionName) &&
                auditEntry.WasApproved.HasValue)
            {
                ChatSessionApprovalRecord? approval = await FindApprovalRecordAsync(
                    sessionId,
                    auditEntry.ApprovalId,
                    ct);

                if (approval is null)
                {
                    approval = new ChatSessionApprovalRecord
                    {
                        SessionId = sessionId,
                        ApprovalId = auditEntry.ApprovalId,
                        FunctionName = auditEntry.FunctionName,
                        RequestedAt = auditEntry.OccurredAt,
                    };
                    db.ChatSessionApprovalRecords.Add(approval);
                }

                approval.Status = auditEntry.WasApproved.Value ? ChatApprovalStatus.Approved : ChatApprovalStatus.Rejected;
                approval.ResolvedAt = auditEntry.OccurredAt;
                approval.ResolutionSource = auditEntry.WasAutoDecided == true ? "auto" : WorkspaceSnapshotSources.BrowserImport;
                approval.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<ChatSessionApprovalRecord?> FindApprovalRecordAsync(
        string sessionId,
        string approvalId,
        CancellationToken ct)
    {
        ChatSessionApprovalRecord? trackedApproval = db.ChatSessionApprovalRecords.Local
            .FirstOrDefault(item => item.SessionId == sessionId && item.ApprovalId == approvalId);

        return trackedApproval ?? await db.ChatSessionApprovalRecords.FirstOrDefaultAsync(
            item => item.SessionId == sessionId && item.ApprovalId == approvalId,
            ct);
    }

    public async Task ClearWorkspaceAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        ChatSessionWorkspaceSnapshot? snapshot = await db.ChatSessionWorkspaceSnapshots.FirstOrDefaultAsync(item => item.SessionId == sessionId, ct);
        if (snapshot is not null)
        {
            db.ChatSessionWorkspaceSnapshots.Remove(snapshot);
        }

        List<ChatSessionApprovalRecord> approvals = await db.ChatSessionApprovalRecords
            .Where(item => item.SessionId == sessionId)
            .ToListAsync(ct);
        if (approvals.Count > 0)
        {
            db.ChatSessionApprovalRecords.RemoveRange(approvals);
        }

        List<ChatSessionAuditEvent> auditEvents = await db.ChatSessionAuditEvents
            .Where(item => item.SessionId == sessionId)
            .ToListAsync(ct);
        if (auditEvents.Count > 0)
        {
            db.ChatSessionAuditEvents.RemoveRange(auditEvents);
        }

        await db.SaveChangesAsync(ct);
    }

    private static WorkspaceDerivationResult DeriveWorkspace(ChatSession session, IReadOnlyList<ChatConversationNode> allNodes)
    {
        Dictionary<string, ChatConversationNode> nodesById = allNodes.ToDictionary(item => item.NodeId, StringComparer.Ordinal);
        List<ChatConversationNode> activeBranchNodes = BuildActiveBranch(session.ActiveLeafMessageId, nodesById);

        Plan? currentPlan = null;
        Recipe? currentRecipe = null;
        DocumentState? currentDocument = null;
        string? previousDocumentText = null;
        DataGridResult? currentDataGrid = null;
        List<ChatSessionFileReferenceDto> fileReferences = [];
        Dictionary<string, string> activeBranchCallNames = new(StringComparer.Ordinal);

        foreach (ChatConversationNode node in activeBranchNodes)
        {
            if (string.Equals(node.Role, ChatRole.User.Value, StringComparison.OrdinalIgnoreCase))
            {
                fileReferences.AddRange(ParseFileReferences(node));
            }

            foreach (AIContent content in DeserializeContents(node.ContentJson))
            {
                if (content is FunctionCallContent functionCall &&
                    !string.IsNullOrWhiteSpace(functionCall.CallId) &&
                    !string.IsNullOrWhiteSpace(functionCall.Name))
                {
                    activeBranchCallNames[functionCall.CallId] = functionCall.Name;
                }
                else if (content is DataContent dataContent)
                {
                    if (TryParsePlanSnapshot(dataContent, out Plan? plan) && plan is not null)
                    {
                        currentPlan = Clone(plan);
                    }
                    else if (TryParsePlanPatch(dataContent, out List<JsonPatchOperation>? patchOperations) && currentPlan is not null && patchOperations is not null)
                    {
                        ApplyPlanPatch(currentPlan, patchOperations);
                    }
                    else if (TryParseRecipeSnapshot(dataContent, out Recipe? recipe))
                    {
                        currentRecipe = recipe;
                    }
                    else if (TryParseDocumentSnapshot(dataContent, out DocumentState? documentState))
                    {
                        if (!string.IsNullOrWhiteSpace(currentDocument?.Document))
                        {
                            previousDocumentText = currentDocument.Document;
                        }

                        currentDocument = documentState;
                    }
                }
                else if (content is FunctionResultContent functionResult &&
                         !string.IsNullOrWhiteSpace(functionResult.CallId) &&
                         activeBranchCallNames.TryGetValue(functionResult.CallId, out string? toolName) &&
                         string.Equals(toolName, "show_data_grid", StringComparison.OrdinalIgnoreCase) &&
                         TryParseDataGridResult(functionResult, out DataGridResult? dataGrid))
                {
                    currentDataGrid = dataGrid;
                }
            }
        }

        ChatSessionWorkspaceSnapshotDto snapshot = new()
        {
            CurrentPlan = currentPlan,
            CurrentRecipe = currentRecipe,
            CurrentDocument = currentDocument,
            PreviousDocumentText = previousDocumentText,
            IsDocumentPreview = false,
            CurrentDataGrid = currentDataGrid,
            FileReferences = fileReferences,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        Dictionary<string, DerivedApprovalRecord> approvals = new(StringComparer.Ordinal);
        Dictionary<string, string> toolNamesByCallId = new(StringComparer.Ordinal);
        List<ChatSessionAuditEvent> derivedAuditEvents = [];

        foreach (ChatConversationNode node in allNodes.OrderBy(item => item.CreatedAt).ThenBy(item => item.SiblingOrder))
        {
            foreach (AIContent content in DeserializeContents(node.ContentJson))
            {
                if (content is FunctionCallContent functionCall)
                {
                    if (!string.IsNullOrWhiteSpace(functionCall.CallId) && !string.IsNullOrWhiteSpace(functionCall.Name))
                    {
                        toolNamesByCallId[functionCall.CallId] = functionCall.Name;
                    }

                    if (string.Equals(functionCall.Name, "request_approval", StringComparison.OrdinalIgnoreCase) &&
                        TryParseApprovalRequest(functionCall, out ApprovalRequest? approvalRequest) &&
                        approvalRequest is not null)
                    {
                        approvals[approvalRequest.ApprovalId] = new DerivedApprovalRecord(
                            approvalRequest.ApprovalId,
                            approvalRequest.FunctionName,
                            approvalRequest.Message,
                            approvalRequest.FunctionArguments?.GetRawText(),
                            functionCall.CallId,
                            ChatApprovalStatus.Pending,
                            node.CreatedAt,
                            null,
                            node.NodeId,
                            null,
                            null);

                        derivedAuditEvents.Add(new ChatSessionAuditEvent
                        {
                            Id = BuildDerivedAuditEventId(session.Id, "approval-request", approvalRequest.ApprovalId),
                            SessionId = session.Id,
                            EventType = ChatAuditEventType.ApprovalRequested,
                            Title = $"Approval requested for {approvalRequest.FunctionName}",
                            Summary = approvalRequest.Message,
                            OccurredAt = node.CreatedAt,
                            RelatedNodeId = node.NodeId,
                            CorrelationId = approvalRequest.ApprovalId,
                            DataJson = JsonSerializer.Serialize(new
                            {
                                approvalId = approvalRequest.ApprovalId,
                                functionName = approvalRequest.FunctionName,
                            }, s_jsonOptions),
                        });
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(functionCall.CallId) && !string.IsNullOrWhiteSpace(functionCall.Name))
                    {
                        derivedAuditEvents.Add(new ChatSessionAuditEvent
                        {
                            Id = BuildDerivedAuditEventId(session.Id, "tool-call", functionCall.CallId),
                            SessionId = session.Id,
                            EventType = ChatAuditEventType.ToolCall,
                            Title = $"Tool call: {functionCall.Name}",
                            Summary = "The assistant emitted a tool call.",
                            OccurredAt = node.CreatedAt,
                            RelatedNodeId = node.NodeId,
                            CorrelationId = functionCall.CallId,
                            DataJson = JsonSerializer.Serialize(new
                            {
                                functionName = functionCall.Name,
                            }, s_jsonOptions),
                        });
                    }
                }
                else if (content is FunctionResultContent functionResult && !string.IsNullOrWhiteSpace(functionResult.CallId))
                {
                    if (approvals.TryGetValue(functionResult.CallId, out DerivedApprovalRecord? approval) &&
                        TryParseApprovalResponse(functionResult, out ApprovalResponse? approvalResponse) &&
                        approvalResponse is not null)
                    {
                        approvals[approval.ApprovalId] = approval with
                        {
                            Status = approvalResponse.Approved ? ChatApprovalStatus.Approved : ChatApprovalStatus.Rejected,
                            ResolvedAt = node.CreatedAt,
                            ResponseNodeId = node.NodeId,
                            ResolutionSource = "conversation-derived",
                        };

                        derivedAuditEvents.Add(new ChatSessionAuditEvent
                        {
                            Id = BuildDerivedAuditEventId(session.Id, "approval-result", approval.ApprovalId),
                            SessionId = session.Id,
                            EventType = ChatAuditEventType.ApprovalResolved,
                            Title = $"Approval {(approvalResponse.Approved ? "approved" : "rejected")}: {approval.FunctionName}",
                            Summary = approvalResponse.Approved ? "The approval request was approved." : "The approval request was rejected.",
                            OccurredAt = node.CreatedAt,
                            RelatedNodeId = node.NodeId,
                            CorrelationId = approval.ApprovalId,
                            DataJson = JsonSerializer.Serialize(new
                            {
                                approvalId = approval.ApprovalId,
                                functionName = approval.FunctionName,
                                wasApproved = approvalResponse.Approved,
                            }, s_jsonOptions),
                        });
                        continue;
                    }

                    if (toolNamesByCallId.TryGetValue(functionResult.CallId, out string? toolName))
                    {
                        derivedAuditEvents.Add(new ChatSessionAuditEvent
                        {
                            Id = BuildDerivedAuditEventId(session.Id, "tool-result", functionResult.CallId),
                            SessionId = session.Id,
                            EventType = ChatAuditEventType.ToolResult,
                            Title = $"Tool result: {toolName}",
                            Summary = "The tool returned a result.",
                            OccurredAt = node.CreatedAt,
                            RelatedNodeId = node.NodeId,
                            CorrelationId = functionResult.CallId,
                            DataJson = JsonSerializer.Serialize(new
                            {
                                functionName = toolName,
                            }, s_jsonOptions),
                        });
                    }
                }
            }
        }

        return new WorkspaceDerivationResult(snapshot, approvals.Values.ToList(), derivedAuditEvents);
    }

    private async Task UpsertSnapshotAsync(string sessionId, ChatSessionWorkspaceSnapshotDto snapshot, string source, CancellationToken ct)
    {
        string serializedSnapshot = JsonSerializer.Serialize(snapshot, s_jsonOptions);
        ChatSessionWorkspaceSnapshot? existing = await db.ChatSessionWorkspaceSnapshots.FirstOrDefaultAsync(item => item.SessionId == sessionId, ct);
        if (existing is null)
        {
            db.ChatSessionWorkspaceSnapshots.Add(new ChatSessionWorkspaceSnapshot
            {
                SessionId = sessionId,
                SnapshotJson = serializedSnapshot,
                UpdatedAt = snapshot.UpdatedAt,
                Source = source,
            });
            return;
        }

        existing.SnapshotJson = serializedSnapshot;
        existing.UpdatedAt = snapshot.UpdatedAt;
        existing.Source = source;
        existing.ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    private async Task UpsertApprovalRecordsAsync(string sessionId, IReadOnlyCollection<DerivedApprovalRecord> approvals, CancellationToken ct)
    {
        Dictionary<string, ChatSessionApprovalRecord> existingById = await db.ChatSessionApprovalRecords
            .Where(item => item.SessionId == sessionId)
            .ToDictionaryAsync(item => item.ApprovalId, StringComparer.Ordinal, ct);

        foreach (DerivedApprovalRecord approval in approvals)
        {
            if (!existingById.TryGetValue(approval.ApprovalId, out ChatSessionApprovalRecord? existing))
            {
                db.ChatSessionApprovalRecords.Add(new ChatSessionApprovalRecord
                {
                    SessionId = sessionId,
                    ApprovalId = approval.ApprovalId,
                    FunctionName = approval.FunctionName,
                    Message = approval.Message,
                    FunctionArgumentsJson = approval.FunctionArgumentsJson,
                    OriginalCallId = approval.OriginalCallId,
                    Status = approval.Status,
                    RequestedAt = approval.RequestedAt,
                    ResolvedAt = approval.ResolvedAt,
                    RequestNodeId = approval.RequestNodeId,
                    ResponseNodeId = approval.ResponseNodeId,
                    ResolutionSource = approval.ResolutionSource,
                });
                continue;
            }

            existing.FunctionName = approval.FunctionName;
            existing.Message = approval.Message;
            existing.FunctionArgumentsJson = approval.FunctionArgumentsJson;
            existing.OriginalCallId = approval.OriginalCallId ?? existing.OriginalCallId;
            existing.Status = approval.Status;
            existing.RequestedAt = approval.RequestedAt;
            existing.ResolvedAt = approval.ResolvedAt ?? existing.ResolvedAt;
            existing.RequestNodeId = approval.RequestNodeId ?? existing.RequestNodeId;
            existing.ResponseNodeId = approval.ResponseNodeId ?? existing.ResponseNodeId;
            existing.ResolutionSource = approval.ResolutionSource ?? existing.ResolutionSource;
            existing.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        }
    }

    private async Task UpsertAuditEventsAsync(string sessionId, IReadOnlyCollection<ChatSessionAuditEvent> events, CancellationToken ct)
    {
        Dictionary<string, ChatSessionAuditEvent> existingById = await db.ChatSessionAuditEvents
            .Where(item => item.SessionId == sessionId)
            .ToDictionaryAsync(item => item.Id, StringComparer.Ordinal, ct);

        foreach (ChatSessionAuditEvent auditEvent in events)
        {
            if (!existingById.TryGetValue(auditEvent.Id, out ChatSessionAuditEvent? existing))
            {
                db.ChatSessionAuditEvents.Add(auditEvent);
                existingById[auditEvent.Id] = auditEvent;
                continue;
            }

            existing.EventType = auditEvent.EventType;
            existing.Title = auditEvent.Title;
            existing.Summary = auditEvent.Summary;
            existing.DataJson = auditEvent.DataJson;
            existing.OccurredAt = auditEvent.OccurredAt;
            existing.RelatedNodeId = auditEvent.RelatedNodeId;
            existing.CorrelationId = auditEvent.CorrelationId;
        }
    }

    private async Task UpsertImportedAuditEventAsync(
        string sessionId,
        ChatSessionAuditImportEntryDto auditEntry,
        ChatAuditEventType eventType,
        CancellationToken ct)
    {
        ChatSessionAuditEvent? existing = db.ChatSessionAuditEvents.Local
            .FirstOrDefault(item => item.Id == auditEntry.Id);

        existing ??= await db.ChatSessionAuditEvents.FirstOrDefaultAsync(item => item.Id == auditEntry.Id, ct);
        string title = string.IsNullOrWhiteSpace(auditEntry.Title)
            ? eventType switch
            {
                ChatAuditEventType.ApprovalResolved => $"Approval {(auditEntry.WasApproved == true ? "approved" : "rejected")}: {auditEntry.FunctionName}",
                ChatAuditEventType.ApprovalRequested => $"Approval requested for {auditEntry.FunctionName}",
                _ => auditEntry.EventType,
            }
            : auditEntry.Title;

        string? dataJson = JsonSerializer.Serialize(new
        {
            auditEntry.ApprovalId,
            auditEntry.FunctionName,
            auditEntry.RiskLevel,
            auditEntry.AutonomyLevel,
            auditEntry.WasApproved,
            auditEntry.WasAutoDecided,
        }, s_jsonOptions);

        if (existing is null)
        {
            db.ChatSessionAuditEvents.Add(new ChatSessionAuditEvent
            {
                Id = auditEntry.Id,
                SessionId = sessionId,
                EventType = eventType,
                Title = title,
                Summary = auditEntry.Summary,
                DataJson = dataJson,
                OccurredAt = auditEntry.OccurredAt,
                CorrelationId = auditEntry.ApprovalId,
            });
            return;
        }

        existing.EventType = eventType;
        existing.Title = title;
        existing.Summary = auditEntry.Summary;
        existing.DataJson = dataJson;
        existing.OccurredAt = auditEntry.OccurredAt;
        existing.CorrelationId = auditEntry.ApprovalId ?? existing.CorrelationId;
    }

    private static ChatSessionWorkspaceSnapshotDto? DeserializeSnapshot(string json, DateTimeOffset updatedAt)
    {
        ChatSessionWorkspaceSnapshotDto? snapshot = JsonSerializer.Deserialize<ChatSessionWorkspaceSnapshotDto>(json, s_jsonOptions);
        if (snapshot is null)
        {
            return null;
        }

        return new ChatSessionWorkspaceSnapshotDto
        {
            CurrentPlan = snapshot.CurrentPlan,
            CurrentRecipe = snapshot.CurrentRecipe,
            CurrentDocument = snapshot.CurrentDocument,
            PreviousDocumentText = snapshot.PreviousDocumentText,
            IsDocumentPreview = snapshot.IsDocumentPreview,
            CurrentDataGrid = snapshot.CurrentDataGrid,
            FileReferences = snapshot.FileReferences,
            UpdatedAt = updatedAt,
        };
    }

    private static ChatSessionAuditEventDto MapAuditEvent(ChatSessionAuditEvent auditEvent)
    {
        JsonElement? data = ParseJson(auditEvent.DataJson);
        return new ChatSessionAuditEventDto
        {
            Id = auditEvent.Id,
            EventType = auditEvent.EventType.ToString(),
            Title = auditEvent.Title,
            Summary = auditEvent.Summary,
            OccurredAt = auditEvent.OccurredAt,
            ApprovalId = TryGetString(data, "approvalId"),
            FunctionName = TryGetString(data, "functionName"),
            RiskLevel = TryGetString(data, "riskLevel"),
            AutonomyLevel = TryGetString(data, "autonomyLevel"),
            WasApproved = TryGetBoolean(data, "wasApproved"),
            WasAutoDecided = TryGetBoolean(data, "wasAutoDecided"),
            PreferredModelId = TryGetString(data, "preferredModelId"),
            EffectiveModelId = TryGetString(data, "effectiveModelId"),
            RoutingReason = TryGetString(data, "routingReason"),
            InputMessageCount = TryGetInt32(data, "inputMessageCount"),
            OutputMessageCount = TryGetInt32(data, "outputMessageCount"),
            WasCompacted = TryGetBoolean(data, "wasCompacted"),
        };
    }

    private static ChatAuditEventType ParseAuditEventType(string? rawValue)
        => Enum.TryParse<ChatAuditEventType>(rawValue, ignoreCase: true, out ChatAuditEventType parsed)
            ? parsed
            : ChatAuditEventType.WorkspaceImport;

    private static string BuildDerivedAuditEventId(string sessionId, string category, string suffix)
        => $"derived-{sessionId}-{category}-{suffix}";

    private static List<ChatConversationNode> BuildActiveBranch(string? activeLeafId, Dictionary<string, ChatConversationNode> nodesById)
    {
        if (string.IsNullOrWhiteSpace(activeLeafId) || !nodesById.TryGetValue(activeLeafId, out ChatConversationNode? current))
        {
            return [];
        }

        List<ChatConversationNode> result = [];
        while (current is not null)
        {
            result.Add(current);
            current = current.ParentNodeId is not null && nodesById.TryGetValue(current.ParentNodeId, out ChatConversationNode? parent)
                ? parent
                : null;
        }

        result.Reverse();
        return result;
    }

    private static List<AIContent> DeserializeContents(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return [];
        }

        try
        {
            List<StoredContentPayload>? payloads = JsonSerializer.Deserialize<List<StoredContentPayload>>(contentJson, s_jsonOptions);
            if (payloads is null)
            {
                return [];
            }

            List<AIContent> results = [];
            foreach (StoredContentPayload payload in payloads)
            {
                AIContent? content = RestoreContent(payload);
                if (content is not null)
                {
                    results.Add(content);
                }
            }

            return DeduplicateFunctionResultContents(results);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static AIContent? RestoreContent(StoredContentPayload payload)
    {
        string rawJson = payload.Value.GetRawText();
        return payload.Type switch
        {
            nameof(TextContent) => JsonSerializer.Deserialize<TextContent>(rawJson, s_jsonOptions),
            nameof(DataContent) => JsonSerializer.Deserialize<DataContent>(rawJson, s_jsonOptions),
            nameof(FunctionCallContent) => JsonSerializer.Deserialize<FunctionCallContent>(rawJson, s_jsonOptions),
            nameof(FunctionResultContent) => JsonSerializer.Deserialize<FunctionResultContent>(rawJson, s_jsonOptions),
            _ => null,
        };
    }

    private static List<AIContent> DeduplicateFunctionResultContents(IEnumerable<AIContent> contents)
    {
        List<AIContent> normalized = [];
        HashSet<string> seenFunctionResultCallIds = new(StringComparer.Ordinal);

        foreach (AIContent content in contents)
        {
            if (content is FunctionResultContent functionResult &&
                !string.IsNullOrWhiteSpace(functionResult.CallId) &&
                !seenFunctionResultCallIds.Add(functionResult.CallId))
            {
                continue;
            }

            normalized.Add(content);
        }

        return normalized;
    }

    private static bool TryParseApprovalRequest(FunctionCallContent content, out ApprovalRequest? approvalRequest)
    {
        approvalRequest = null;
        if (!string.Equals(content.Name, "request_approval", StringComparison.OrdinalIgnoreCase) ||
            content.Arguments?.TryGetValue("request", out object? requestValue) != true)
        {
            return false;
        }

        try
        {
            approvalRequest = requestValue switch
            {
                JsonElement element => element.Deserialize<ApprovalRequest>(s_jsonOptions),
                ApprovalRequest direct => direct,
                _ => JsonSerializer.Deserialize<ApprovalRequest>(JsonSerializer.Serialize(requestValue, s_jsonOptions), s_jsonOptions),
            };
        }
        catch (JsonException)
        {
            approvalRequest = null;
        }

        return approvalRequest is not null;
    }

    private static bool TryParseApprovalResponse(FunctionResultContent content, out ApprovalResponse? approvalResponse)
    {
        approvalResponse = null;
        try
        {
            approvalResponse = content.Result switch
            {
                JsonElement element => element.Deserialize<ApprovalResponse>(s_jsonOptions),
                string text => JsonSerializer.Deserialize<ApprovalResponse>(text, s_jsonOptions),
                ApprovalResponse direct => direct,
                _ => JsonSerializer.Deserialize<ApprovalResponse>(JsonSerializer.Serialize(content.Result, s_jsonOptions), s_jsonOptions),
            };
        }
        catch (JsonException)
        {
            approvalResponse = null;
        }

        return approvalResponse is not null;
    }

    private static bool TryParsePlanSnapshot(DataContent content, out Plan? plan)
    {
        plan = null;
        if (content.MediaType != "application/json" || !TryGetEnvelope(content, out string? type, out JsonElement payload) ||
            !string.Equals(type, TypedDataEnvelopeTypes.PlanSnapshot, StringComparison.Ordinal))
        {
            return false;
        }

        plan = JsonSerializer.Deserialize<Plan>(payload.GetRawText(), s_jsonOptions);
        return plan is not null;
    }

    private static bool TryParsePlanPatch(DataContent content, out List<JsonPatchOperation>? operations)
    {
        operations = null;
        if (!string.Equals(content.MediaType, "application/json-patch+json", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            operations = JsonSerializer.Deserialize<List<JsonPatchOperation>>(content.Data.Span, s_jsonOptions);
        }
        catch (JsonException)
        {
            operations = null;
        }

        return operations is not null;
    }

    private static bool TryParseRecipeSnapshot(DataContent content, out Recipe? recipe)
    {
        recipe = null;
        if (content.MediaType != "application/json" || !TryGetEnvelope(content, out string? type, out JsonElement payload) ||
            !string.Equals(type, TypedDataEnvelopeTypes.RecipeSnapshot, StringComparison.Ordinal))
        {
            return false;
        }

        recipe = JsonSerializer.Deserialize<Recipe>(payload.GetRawText(), s_jsonOptions);
        return recipe is not null;
    }

    private static bool TryParseDocumentSnapshot(DataContent content, out DocumentState? documentState)
    {
        documentState = null;
        if (content.MediaType != "application/json" || !TryGetEnvelope(content, out string? type, out JsonElement payload) ||
            !string.Equals(type, TypedDataEnvelopeTypes.DocumentPreview, StringComparison.Ordinal))
        {
            return false;
        }

        documentState = JsonSerializer.Deserialize<DocumentState>(payload.GetRawText(), s_jsonOptions);
        return documentState is not null;
    }

    private static bool TryParseDataGridResult(FunctionResultContent content, out DataGridResult? dataGrid)
    {
        dataGrid = null;

        JsonElement jsonElement = content.Result switch
        {
            JsonElement element => element,
            string text => JsonDocument.Parse(text).RootElement.Clone(),
            _ => default,
        };

        if (jsonElement.ValueKind != JsonValueKind.Object ||
            !jsonElement.TryGetProperty("columns", out _) ||
            !jsonElement.TryGetProperty("rows", out _))
        {
            return false;
        }

        dataGrid = jsonElement.Deserialize<DataGridResult>(s_jsonOptions);
        return dataGrid is not null;
    }

    private static bool TryGetEnvelope(DataContent content, out string? type, out JsonElement payload)
    {
        type = null;
        payload = default;

        try
        {
            using JsonDocument document = JsonDocument.Parse(content.Data.ToArray());
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("$type", out JsonElement typeElement) ||
                typeElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("data", out JsonElement dataElement))
            {
                return false;
            }

            type = typeElement.GetString();
            payload = dataElement.Clone();
            return !string.IsNullOrWhiteSpace(type);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ApplyPlanPatch(Plan plan, IReadOnlyCollection<JsonPatchOperation> operations)
    {
        foreach (JsonPatchOperation operation in operations)
        {
            if (!string.Equals(operation.Op, "replace", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(operation.Path))
            {
                continue;
            }

            string[] segments = operation.Path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length != 3 ||
                !string.Equals(segments[0], "steps", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(segments[1], out int stepIndex) ||
                stepIndex < 0 ||
                stepIndex >= plan.Steps.Count)
            {
                continue;
            }

            Step step = plan.Steps[stepIndex];
            if (string.Equals(segments[2], "description", StringComparison.OrdinalIgnoreCase) && operation.Value is not null)
            {
                step.Description = operation.Value switch
                {
                    JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString() ?? step.Description,
                    _ => operation.Value.ToString() ?? step.Description,
                };
            }
            else if (string.Equals(segments[2], "status", StringComparison.OrdinalIgnoreCase) && operation.Value is not null)
            {
                string? rawStatus = operation.Value switch
                {
                    JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
                    _ => operation.Value.ToString(),
                };

                if (Enum.TryParse<StepStatus>(rawStatus, ignoreCase: true, out StepStatus parsedStatus))
                {
                    step.Status = parsedStatus;
                }
            }
        }
    }

    private static Plan Clone(Plan plan)
    {
        string json = JsonSerializer.Serialize(plan, s_jsonOptions);
        return JsonSerializer.Deserialize<Plan>(json, s_jsonOptions) ?? new Plan { Steps = [] };
    }

    private static List<ChatSessionFileReferenceDto> ParseFileReferences(ChatConversationNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Text))
        {
            return [];
        }

        MatchCollection matches = AttachmentMarkerRegex().Matches(node.Text);
        if (matches.Count == 0)
        {
            return [];
        }

        List<ChatSessionFileReferenceDto> results = [];
        foreach (Match match in matches)
        {
            results.Add(new ChatSessionFileReferenceDto
            {
                AttachmentId = match.Groups[1].Value,
                FileName = match.Groups[2].Value,
                ContentType = match.Groups[3].Value,
                MessageNodeId = node.NodeId,
            });
        }

        return results;
    }

    private static JsonElement? ParseJson(string? json)
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

    private static string? TryGetString(JsonElement? json, string propertyName)
    {
        if (json is null || !json.Value.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool? TryGetBoolean(JsonElement? json, string propertyName)
    {
        if (json is null || !json.Value.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            return null;
        }

        return property.GetBoolean();
    }

    private static int? TryGetInt32(JsonElement? json, string propertyName)
    {
        if (json is null || !json.Value.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetInt32(out int value) ? value : null;
    }

    [GeneratedRegex(@"<!-- file:([a-f0-9]+):([^:]+):([^ ]+) -->", RegexOptions.Compiled)]
    private static partial Regex AttachmentMarkerRegex();

    private sealed record WorkspaceDerivationResult(
        ChatSessionWorkspaceSnapshotDto Snapshot,
        List<DerivedApprovalRecord> Approvals,
        List<ChatSessionAuditEvent> DerivedAuditEvents);

    private sealed record DerivedApprovalRecord(
        string ApprovalId,
        string FunctionName,
        string? Message,
        string? FunctionArgumentsJson,
        string? OriginalCallId,
        ChatApprovalStatus Status,
        DateTimeOffset RequestedAt,
        DateTimeOffset? ResolvedAt,
        string? RequestNodeId,
        string? ResponseNodeId,
        string? ResolutionSource);

    private sealed record StoredContentPayload(string Type, JsonElement Value);
}
