using System.Collections.Immutable;
using AGUIDojoClient.Models;
using Fluxor;
using Microsoft.Extensions.AI;

namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Reducers for session-scoped chat state.
/// </summary>
public static class SessionReducers
{
    private static SessionManagerState UpdateSession(
        SessionManagerState state,
        string sessionId,
        Func<SessionEntry, SessionEntry> updateEntry)
    {
        if (!state.Sessions.TryGetValue(sessionId, out SessionEntry? entry))
        {
            return state;
        }

        SessionEntry updatedEntry = updateEntry(entry);
        ImmutableDictionary<string, SessionEntry> sessions = updatedEntry.Metadata.Status == SessionStatus.Archived
            ? state.Sessions.Remove(sessionId)
            : state.Sessions.SetItem(sessionId, updatedEntry);

        IReadOnlyList<SessionEntry> orderedSessions = SessionSelectors.GetOrderedSessions(state with { Sessions = sessions });
        SessionManagerState updatedState = state with
        {
            Sessions = sessions,
            ActiveSessionId = state.ActiveSessionId == sessionId && !sessions.ContainsKey(sessionId)
                ? (orderedSessions.Count > 0 ? orderedSessions[0].Metadata.Id : null)
                : state.ActiveSessionId,
        };

        return EnsureActiveSession(updatedState);
    }

    private static SessionManagerState UpdateSessionState(
        SessionManagerState state,
        string sessionId,
        Func<SessionState, SessionState> mutateState,
        Func<SessionMetadata, SessionMetadata>? mutateMetadata = null)
    {
        return UpdateSession(
            state,
            sessionId,
            entry => entry with
            {
                State = mutateState(entry.State),
                Metadata = mutateMetadata is null ? entry.Metadata : mutateMetadata(entry.Metadata),
            });
    }

    private static SessionManagerState EnsureActiveSession(SessionManagerState state)
    {
        if (state.ActiveSessionId is not null && state.Sessions.ContainsKey(state.ActiveSessionId))
        {
            return state;
        }

        if (state.Sessions.Count > 0)
        {
            IReadOnlyList<SessionEntry> orderedSessions = SessionSelectors.GetOrderedSessions(state);
            return state with { ActiveSessionId = orderedSessions[0].Metadata.Id };
        }

        return SessionManagerState.CreateInitial();
    }

    private static DateTimeOffset ResolveTimestamp(DateTimeOffset? occurredAt) => occurredAt ?? DateTimeOffset.UtcNow;

    private static SessionStatus ResolveForegroundIdleStatus(SessionMetadata metadata) => metadata.Status switch
    {
        SessionStatus.Created => SessionStatus.Created,
        SessionStatus.Error => SessionStatus.Error,
        _ => SessionStatus.Active,
    };

    private static SessionStatus ResolveMessageStatus(SessionManagerState state, string sessionId, SessionMetadata metadata, ChatMessage message)
    {
        if (message.Role == ChatRole.System)
        {
            return metadata.Status;
        }

        if (message.Role == ChatRole.Assistant && state.ActiveSessionId != sessionId)
        {
            return SessionStatus.Background;
        }

        if (message.Role == ChatRole.User && metadata.Status is SessionStatus.Created or SessionStatus.Completed or SessionStatus.Error)
        {
            return state.ActiveSessionId == sessionId ? SessionStatus.Active : metadata.Status;
        }

        if (message.Role == ChatRole.Assistant && metadata.Status == SessionStatus.Created)
        {
            return SessionStatus.Active;
        }

        return metadata.Status;
    }

    private static bool HasVisibleArtifacts(ImmutableHashSet<ArtifactType> tabs) => tabs.Count > 0;

    private static ArtifactType ResolveActiveArtifactOrFallback(ArtifactType activeType, ImmutableHashSet<ArtifactType> tabs)
    {
        if (activeType != ArtifactType.None && tabs.Contains(activeType))
        {
            return activeType;
        }

        return tabs.Count > 0 ? tabs.First() : ArtifactType.None;
    }

    private static SessionMetadata UpdateActivity(
        SessionMetadata metadata,
        DateTimeOffset? occurredAt = null,
        int unreadCountDelta = 0,
        bool? hasPendingApproval = null,
        SessionStatus? status = null,
        string? title = null,
        string? endpointPath = null,
        int? unreadCount = null)
    {
        return metadata with
        {
            LastActivityAt = ResolveTimestamp(occurredAt),
            UnreadCount = unreadCount ?? Math.Max(0, metadata.UnreadCount + unreadCountDelta),
            HasPendingApproval = hasPendingApproval ?? metadata.HasPendingApproval,
            Status = status ?? metadata.Status,
            Title = title ?? metadata.Title,
            EndpointPath = endpointPath ?? metadata.EndpointPath,
        };
    }

    private static ImmutableDictionary<string, SessionEntry> EnforceCapacity(ImmutableDictionary<string, SessionEntry> sessions)
    {
        if (sessions.Count < SessionManagerState.MaxActiveSessions)
        {
            return sessions;
        }

        KeyValuePair<string, SessionEntry>? evictionCandidate = sessions
            .Where(pair => pair.Value.Metadata.Status == SessionStatus.Completed)
            .OrderBy(pair => pair.Value.Metadata.LastActivityAt)
            .Cast<KeyValuePair<string, SessionEntry>?>()
            .FirstOrDefault();

        if (evictionCandidate is null)
        {
            evictionCandidate = sessions.OrderBy(pair => pair.Value.Metadata.CreatedAt).Cast<KeyValuePair<string, SessionEntry>?>().FirstOrDefault();
        }

        return evictionCandidate is null ? sessions : sessions.Remove(evictionCandidate.Value.Key);
    }

    [ReducerMethod]
    public static SessionManagerState OnCreateSession(SessionManagerState state, SessionActions.CreateSessionAction action)
    {
        if (state.Sessions.ContainsKey(action.SessionId))
        {
            return action.MakeActive ? state with { ActiveSessionId = action.SessionId } : state;
        }

        SessionEntry entry = SessionManagerState.CreateSessionEntry(
            action.SessionId,
            action.Title,
            action.EndpointPath,
            action.CreatedAt,
            action.AguiThreadId,
            preferredModelId: action.PreferredModelId);
        ImmutableDictionary<string, SessionEntry> sessions = EnforceCapacity(state.Sessions).Add(action.SessionId, entry);
        return EnsureActiveSession(state with
        {
            Sessions = sessions,
            ActiveSessionId = action.MakeActive ? action.SessionId : state.ActiveSessionId,
        });
    }

    [ReducerMethod]
    public static SessionManagerState OnSetActiveSession(SessionManagerState state, SessionActions.SetActiveSessionAction action)
    {
        if (!state.Sessions.ContainsKey(action.SessionId))
        {
            return state;
        }

        SessionManagerState updated = state with { ActiveSessionId = action.SessionId };
        foreach (string sessionId in state.Sessions.Keys)
        {
            updated = UpdateSession(
                updated,
                sessionId,
                entry => entry with
                {
                    Metadata = entry.Metadata with
                    {
                        Status = sessionId == action.SessionId
                            ? (entry.State.IsRunning ? SessionStatus.Streaming : ResolveForegroundIdleStatus(entry.Metadata))
                            : (entry.State.IsRunning ? SessionStatus.Background : entry.Metadata.Status),
                        UnreadCount = sessionId == action.SessionId ? 0 : entry.Metadata.UnreadCount,
                    },
                });
        }

        return updated;
    }

    [ReducerMethod]
    public static SessionManagerState OnArchiveSession(SessionManagerState state, SessionActions.ArchiveSessionAction action) =>
        UpdateSession(state, action.SessionId, entry => entry with { Metadata = entry.Metadata with { Status = SessionStatus.Archived } });

    [ReducerMethod]
    public static SessionManagerState OnSetSessionTitle(SessionManagerState state, SessionActions.SetSessionTitleAction action) =>
        UpdateSession(state, action.SessionId, entry => entry with { Metadata = entry.Metadata with { Title = action.Title } });

    [ReducerMethod]
    public static SessionManagerState OnSetEndpoint(SessionManagerState state, SessionActions.SetEndpointAction action) =>
        UpdateSession(state, action.SessionId, entry => entry with { Metadata = entry.Metadata with { EndpointPath = action.EndpointPath } });

    [ReducerMethod]
    public static SessionManagerState OnSetSessionStatus(SessionManagerState state, SessionActions.SetSessionStatusAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => session,
            metadata => UpdateActivity(metadata, status: action.Status));

    [ReducerMethod]
    public static SessionManagerState OnAddMessage(SessionManagerState state, SessionActions.AddMessageAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => session with { Tree = session.Tree.AddMessage(action.Message) },
            metadata => UpdateActivity(
                metadata,
                action.OccurredAt,
                unreadCountDelta: action.Message.Role == ChatRole.Assistant && state.ActiveSessionId != action.SessionId ? 1 : 0,
                status: ResolveMessageStatus(state, action.SessionId, metadata, action.Message)));

    [ReducerMethod]
    public static SessionManagerState OnUpdateResponseMessage(SessionManagerState state, SessionActions.UpdateResponseMessageAction action) =>
        UpdateSessionState(state, action.SessionId, session => session with { CurrentResponseMessage = action.ResponseMessage });

    [ReducerMethod]
    public static SessionManagerState OnSetConversationId(SessionManagerState state, SessionActions.SetConversationIdAction action) =>
        UpdateSessionState(state, action.SessionId, session => session with { ConversationId = action.ConversationId });

    [ReducerMethod]
    public static SessionManagerState OnSetSessionCorrelation(SessionManagerState state, SessionActions.SetSessionCorrelationAction action) =>
        UpdateSession(
            state,
            action.SessionId,
            entry => entry with
            {
                Metadata = entry.Metadata with
                {
                    AguiThreadId = string.IsNullOrWhiteSpace(action.AguiThreadId) ? entry.Metadata.AguiThreadId : action.AguiThreadId,
                    ServerSessionId = string.IsNullOrWhiteSpace(action.ServerSessionId) ? entry.Metadata.ServerSessionId : action.ServerSessionId,
                },
            });

    [ReducerMethod]
    public static SessionManagerState OnSetPreferredModel(SessionManagerState state, SessionActions.SetPreferredModelAction action) =>
        UpdateSession(
            state,
            action.SessionId,
            entry => entry with
            {
                Metadata = entry.Metadata with
                {
                    PreferredModelId = string.IsNullOrWhiteSpace(action.PreferredModelId) ? null : action.PreferredModelId,
                },
            });

    [ReducerMethod]
    public static SessionManagerState OnSetPendingApproval(SessionManagerState state, SessionActions.SetPendingApprovalAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => session with { PendingApproval = action.PendingApproval },
            metadata => UpdateActivity(metadata, hasPendingApproval: action.PendingApproval is not null));

    [ReducerMethod]
    public static SessionManagerState OnStartUndoGracePeriod(SessionManagerState state, SessionActions.StartUndoGracePeriodAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => session with
            {
                PendingUndo = new PendingUndoState
                {
                    CheckpointId = action.CheckpointId,
                    CheckpointLabel = action.CheckpointLabel,
                    Summary = action.Summary,
                    StartedAt = action.StartedAt,
                    ExpiresAt = action.ExpiresAt,
                }
            });

    [ReducerMethod]
    public static SessionManagerState OnClearUndoGracePeriod(SessionManagerState state, SessionActions.ClearUndoGracePeriodAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => session.PendingUndo is null ||
                (action.CheckpointId is not null &&
                 !string.Equals(session.PendingUndo.CheckpointId, action.CheckpointId, StringComparison.Ordinal))
                ? session
                : session with { PendingUndo = null });

    [ReducerMethod]
    public static SessionManagerState OnClearMessages(SessionManagerState state, SessionActions.ClearMessagesAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => session with
                {
                    Tree = new(),
                    CurrentResponseMessage = null,
                    ConversationId = null,
                    PendingApproval = null,
                    PendingUndo = null,
                    CurrentRecipe = null,
                    CurrentDocumentState = null,
                    IsDocumentPreview = true,
                    HasInteractiveArtifact = false,
                    ActiveArtifactType = ArtifactType.None,
                    DiffPreview = null,
                    CurrentDataGrid = null,
                    ToolArtifacts = ImmutableList<ToolArtifactState>.Empty,
                    ActiveToolArtifactId = null,
                    VisibleTabs = ImmutableHashSet<ArtifactType>.Empty,
                },
            metadata => UpdateActivity(
                metadata with
                {
                    AguiThreadId = SessionMetadata.CreateAguiThreadId(),
                    ServerSessionId = null,
                },
                action.OccurredAt,
                hasPendingApproval: false,
                status: SessionStatus.Created,
                unreadCount: 0));

    [ReducerMethod]
    public static SessionManagerState OnTrimMessages(SessionManagerState state, SessionActions.TrimMessagesAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => action.KeepCount >= session.Messages.Count
                ? session
                : session with { Tree = session.Tree.TruncateActiveBranch(action.KeepCount) });

    [ReducerMethod]
    public static SessionManagerState OnSetRunning(SessionManagerState state, SessionActions.SetRunningAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => session with { IsRunning = action.IsRunning },
            metadata => UpdateActivity(
                metadata,
                status: action.IsRunning
                    ? (state.ActiveSessionId == action.SessionId ? SessionStatus.Streaming : SessionStatus.Background)
                    : (metadata.Status == SessionStatus.Error ? SessionStatus.Error : SessionStatus.Completed)));

    [ReducerMethod]
    public static SessionManagerState OnSetAuthorName(SessionManagerState state, SessionActions.SetAuthorNameAction action) =>
        UpdateSessionState(state, action.SessionId, session => session with { CurrentAuthorName = action.AuthorName });

    [ReducerMethod]
    public static SessionManagerState OnSetPlan(SessionManagerState state, SessionActions.SetPlanAction action) =>
        UpdateSessionState(state, action.SessionId, session => session with { Plan = action.Plan });

    [ReducerMethod]
    public static SessionManagerState OnApplyPlanDelta(SessionManagerState state, SessionActions.ApplyPlanDeltaAction action) =>
        UpdateSessionState(state, action.SessionId, session => session with { Plan = session.Plan });

    [ReducerMethod]
    public static SessionManagerState OnClearPlan(SessionManagerState state, SessionActions.ClearPlanAction action) =>
        UpdateSessionState(state, action.SessionId, session => session with { Plan = null, PlanDiff = null });

    [ReducerMethod]
    public static SessionManagerState OnSetPlanDiff(SessionManagerState state, SessionActions.SetPlanDiffAction action) =>
        UpdateSessionState(state, action.SessionId, session => session with { PlanDiff = action.Diff });

    [ReducerMethod]
    public static SessionManagerState OnSetRecipe(SessionManagerState state, SessionActions.SetRecipeAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                ImmutableHashSet<ArtifactType> tabs = session.VisibleTabs.Add(ArtifactType.RecipeEditor);
                ArtifactType activeType = session.ActiveArtifactType == ArtifactType.None ? ArtifactType.RecipeEditor : session.ActiveArtifactType;
                return session with
                {
                    CurrentRecipe = action.Recipe,
                    HasInteractiveArtifact = true,
                    VisibleTabs = tabs,
                    ActiveArtifactType = activeType,
                };
            });

    [ReducerMethod]
    public static SessionManagerState OnSetDocument(SessionManagerState state, SessionActions.SetDocumentAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                ImmutableHashSet<ArtifactType> tabs = session.VisibleTabs.Add(ArtifactType.DocumentPreview);
                ArtifactType activeType = session.ActiveArtifactType == ArtifactType.None ? ArtifactType.DocumentPreview : session.ActiveArtifactType;
                return session with
                {
                    CurrentDocumentState = action.DocumentState,
                    HasInteractiveArtifact = true,
                    VisibleTabs = tabs,
                    ActiveArtifactType = activeType,
                };
            });

    [ReducerMethod]
    public static SessionManagerState OnSetDocumentPreview(SessionManagerState state, SessionActions.SetDocumentPreviewAction action) =>
        UpdateSessionState(state, action.SessionId, session => session with { IsDocumentPreview = action.IsPreview });

    [ReducerMethod]
    public static SessionManagerState OnClearArtifacts(SessionManagerState state, SessionActions.ClearArtifactsAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => session with
            {
                CurrentRecipe = null,
                CurrentDocumentState = null,
                IsDocumentPreview = true,
                HasInteractiveArtifact = false,
                ActiveArtifactType = ArtifactType.None,
                DiffPreview = null,
                CurrentDataGrid = null,
                ToolArtifacts = ImmutableList<ToolArtifactState>.Empty,
                ActiveToolArtifactId = null,
                VisibleTabs = ImmutableHashSet<ArtifactType>.Empty,
            });

    [ReducerMethod]
    public static SessionManagerState OnSetDiffPreviewArtifact(SessionManagerState state, SessionActions.SetDiffPreviewArtifactAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                ImmutableHashSet<ArtifactType> tabs = session.VisibleTabs.Add(ArtifactType.DiffPreview);
                ArtifactType activeType = session.ActiveArtifactType == ArtifactType.None ? ArtifactType.DiffPreview : session.ActiveArtifactType;
                return session with
                {
                    DiffPreview = new DiffPreviewData(action.Before, action.After, action.Title),
                    HasInteractiveArtifact = true,
                    VisibleTabs = tabs,
                    ActiveArtifactType = activeType,
                };
            });

    [ReducerMethod]
    public static SessionManagerState OnSetDataGridArtifact(SessionManagerState state, SessionActions.SetDataGridArtifactAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session => session with
            {
                CurrentDataGrid = action.DataGrid,
                HasInteractiveArtifact = true,
                VisibleTabs = session.VisibleTabs.Add(ArtifactType.DataGrid),
                ActiveArtifactType = ArtifactType.DataGrid,
            });

    [ReducerMethod]
    public static SessionManagerState OnSetActiveArtifact(SessionManagerState state, SessionActions.SetActiveArtifactAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                if (action.ArtifactType != ArtifactType.None && !session.VisibleTabs.Contains(action.ArtifactType))
                {
                    return session;
                }

                return session with { ActiveArtifactType = action.ArtifactType };
            });

    [ReducerMethod]
    public static SessionManagerState OnUpsertToolArtifact(SessionManagerState state, SessionActions.UpsertToolArtifactAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                int existingIndex = session.ToolArtifacts.FindIndex(artifact => artifact.ArtifactId == action.Artifact.ArtifactId);
                ImmutableList<ToolArtifactState> artifacts = existingIndex >= 0
                    ? session.ToolArtifacts.SetItem(existingIndex, action.Artifact)
                    : session.ToolArtifacts.Add(action.Artifact);

                ImmutableHashSet<ArtifactType> tabs = session.VisibleTabs.Add(ArtifactType.ToolResult);

                return session with
                {
                    ToolArtifacts = artifacts,
                    ActiveToolArtifactId = action.MakeActive
                        ? action.Artifact.ArtifactId
                        : session.ActiveToolArtifactId ?? action.Artifact.ArtifactId,
                    VisibleTabs = tabs,
                    HasInteractiveArtifact = HasVisibleArtifacts(tabs),
                    ActiveArtifactType = action.MakeActive
                        ? ArtifactType.ToolResult
                        : ResolveActiveArtifactOrFallback(session.ActiveArtifactType, tabs),
                };
            });

    [ReducerMethod]
    public static SessionManagerState OnRemoveToolArtifact(SessionManagerState state, SessionActions.RemoveToolArtifactAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                ImmutableList<ToolArtifactState> artifacts = session.ToolArtifacts.RemoveAll(artifact => artifact.ArtifactId == action.ArtifactId);
                ImmutableHashSet<ArtifactType> tabs = artifacts.Count > 0
                    ? session.VisibleTabs
                    : session.VisibleTabs.Remove(ArtifactType.ToolResult);

                string? activeToolArtifactId = session.ActiveToolArtifactId == action.ArtifactId
                    ? artifacts.LastOrDefault()?.ArtifactId
                    : session.ActiveToolArtifactId;

                ArtifactType activeArtifactType = session.ActiveArtifactType == ArtifactType.ToolResult && artifacts.Count == 0
                    ? ResolveActiveArtifactOrFallback(ArtifactType.None, tabs)
                    : ResolveActiveArtifactOrFallback(session.ActiveArtifactType, tabs);

                return session with
                {
                    ToolArtifacts = artifacts,
                    ActiveToolArtifactId = activeToolArtifactId,
                    VisibleTabs = tabs,
                    HasInteractiveArtifact = HasVisibleArtifacts(tabs),
                    ActiveArtifactType = activeArtifactType,
                };
            });

    [ReducerMethod]
    public static SessionManagerState OnSetActiveToolArtifact(SessionManagerState state, SessionActions.SetActiveToolArtifactAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                if (!session.ToolArtifacts.Any(artifact => artifact.ArtifactId == action.ArtifactId))
                {
                    return session;
                }

                return session with
                {
                    ActiveToolArtifactId = action.ArtifactId,
                    ActiveArtifactType = ArtifactType.ToolResult,
                    VisibleTabs = session.VisibleTabs.Add(ArtifactType.ToolResult),
                    HasInteractiveArtifact = true,
                };
            });

    [ReducerMethod]
    public static SessionManagerState OnEditAndRegenerate(SessionManagerState state, SessionActions.EditAndRegenerateAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                string? nodeId = session.Tree.GetNodeIdAtIndex(action.MessageIndex);
                if (nodeId is null || !session.Tree.Nodes.TryGetValue(nodeId, out var node) || node.ParentId is null)
                {
                    return session;
                }

                var editedMessage = new ChatMessage(ChatRole.User, action.NewText);
                return session with
                {
                    Tree = session.Tree.BranchAt(node.ParentId, editedMessage),
                    ConversationId = null,
                };
            });

    [ReducerMethod]
    public static SessionManagerState OnSwitchBranch(SessionManagerState state, SessionActions.SwitchBranchAction action) =>
        UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                if (!session.Tree.Nodes.ContainsKey(action.TargetSiblingId))
                {
                    return session;
                }

                string leafId = session.Tree.FindLeafFromNode(action.TargetSiblingId);
                return session with { Tree = session.Tree.SwitchToLeaf(leafId) };
            });

    [ReducerMethod]
    public static SessionManagerState OnHydrateSessions(SessionManagerState state, SessionActions.HydrateSessionsAction action)
    {
        if (action.Sessions.Count == 0)
        {
            return state;
        }

        var sessions = action.Sessions.ToImmutableDictionary();

        // Determine active session: prefer persisted, fall back to first session
        string? activeId = action.ActiveSessionId;
        if (activeId is null || !sessions.ContainsKey(activeId))
        {
            activeId = sessions.Values
                .OrderByDescending(e => e.Metadata.LastActivityAt)
                .First().Metadata.Id;
        }

        return EnsureActiveSession(state with { Sessions = sessions, ActiveSessionId = activeId });
    }

    [ReducerMethod]
    public static SessionManagerState OnSetAutonomyLevel(SessionManagerState state, SessionActions.SetAutonomyLevelAction action)
        => state with { AutonomyLevel = action.Level };

    [ReducerMethod]
    public static SessionManagerState OnAddAuditEntry(SessionManagerState state, SessionActions.AddAuditEntryAction action)
        => UpdateSessionState(
            state,
            action.SessionId,
            session =>
            {
                var updatedTrail = session.AuditTrail.Add(action.Entry);
                var updatedTabs = session.VisibleTabs.Add(ArtifactType.AuditTrail);
                return session with
                {
                    AuditTrail = updatedTrail,
                    VisibleTabs = updatedTabs,
                    HasInteractiveArtifact = true,
                    ActiveArtifactType = session.ActiveArtifactType == ArtifactType.None
                        ? ArtifactType.AuditTrail
                        : session.ActiveArtifactType,
                };
            });
}
