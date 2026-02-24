# Design Section 02: Multi-Session State Architecture (Fluxor)

> **Spec Section:** Client-Side State Architecture
> **Created:** 2026-02-24
> **Status:** Design
> **References:** Q-STATE-001, Q-STATE-002, Q-STATE-003, Q-STATE-004, Q-STATE-005, Q-STATE-006, Q-AGUI-005, Q-HIST-003, R5, R6, R7, ISS-006, ISS-013, ISS-015

---

## Table of Contents

1. [Current State (Before)](#1-current-state-before)
2. [Target State (After)](#2-target-state-after)
3. [SessionState Record](#3-sessionstate-record)
4. [SessionMetadata Model](#4-sessionmetadata-model)
5. [SessionManagerState Feature](#5-sessionmanagerstate-feature)
6. [Reducer Patterns](#6-reducer-patterns)
7. [Atomic Session Switching](#7-atomic-session-switching)
8. [SessionStreamingContext](#8-sessionstreamingcontext)
9. [AgentStreamingService Refactoring](#9-agentstreamingservice-refactoring)
10. [IStateManager Refactoring](#10-istatemanager-refactoring)
11. [Data Flow](#11-data-flow)
12. [State Shape Diagram](#12-state-shape-diagram)
13. [Selector Performance](#13-selector-performance)
14. [Migration Strategy](#14-migration-strategy)

---

## 1. Current State (Before)

The AGUIDojoClient uses **4 independent Fluxor stores**, all registered as global singletons within the Blazor circuit. There is **zero concept of sessions** — the entire UI models exactly one conversation at a time.

> **Ref:** R5 — "All 4 Fluxor stores are flat, global singletons. Fluxor does NOT natively support keyed stores or scoped features."

### 1.1 Current Store Inventory

| Store | State Record | Key Fields | Scope |
|-------|-------------|------------|-------|
| `AgentState` | `AgentState` | `SelectedEndpointPath`, `IsRunning`, `CurrentAuthorName` | Global per circuit |
| `ChatState` | `ChatState` | `Messages` (ImmutableList), `CurrentResponseMessage`, `ConversationId`, `StatefulMessageCount`, `PendingApproval` | Global per circuit |
| `PlanState` | `PlanState` | `Plan?`, `Diff?` | Global per circuit |
| `ArtifactState` | `ArtifactState` | `CurrentRecipe`, `CurrentDocumentState`, `IsDocumentPreview`, `DiffPreview`, `CurrentDataGrid`, `ActiveArtifactType`, `VisibleTabs` | Global per circuit |

### 1.2 Current Service State

Two services hold conversation-scoped state outside Fluxor:

**AgentStreamingService** (Scoped per circuit):
- `CancellationTokenSource? _currentResponseCancellation` — single cancellation per circuit
- `HashSet<string> _seenFunctionCallIds` — deduplication across conversation
- `HashSet<string> _seenFunctionResultCallIds` — deduplication
- `Dictionary<string, string> _functionCallIdToToolName` — tool name tracking
- `ChatOptions _chatOptions` — with `ConversationId`
- `TaskCompletionSource<bool>? _approvalTaskSource` — pending HITL approval
- `object? _lastDiffBefore/_lastDiffAfter` — diff preview state
- `ChatMessage? _streamingMessage` — current streaming message

> **Ref:** R6 — "ALL of this state is per-conversation. In a multi-session world, each session needs its own [instance of each field]."

**StateManager** (Scoped per circuit):
- `Recipe CurrentRecipe` — single global recipe
- `bool HasActiveState`

### 1.3 Problems with Current Architecture

| Problem | Impact | Reference |
|---------|--------|-----------|
| Single conversation only | Users cannot run parallel sessions, compare results, or continue previous conversations | ISS-015 |
| Global singleton stores | Switching conversations requires destroying and rebuilding ALL state | R5 |
| AgentStreamingService holds conversation state | Canceling one stream cancels the only stream; no background streaming | R6 |
| StateManager owns recipe state | Recipe belongs to one conversation context; switching loses it | Q-STATE-006 |
| No session identity | No `sessionId` concept; UI cannot list previous conversations | Q-STATE-004 |

---

## 2. Target State (After)

The architecture migrates from 4 independent global stores to a **single session-keyed store** using the dictionary pattern. This is the Fluxor adaptation of the well-established React/Redux "normalized state with entity keys" pattern.

> **Ref:** Q-STATE-001 — "Session-keyed dictionaries is the recommended approach. Fluxor does NOT natively support keyed stores or feature-level isolation."

### 2.1 Design Principles

1. **Single source of truth**: All per-session state lives in `SessionManagerState.Sessions[sessionId]`
2. **Immutability**: All state records use C# `record` types with `init` properties — Fluxor requirement
3. **Session-keyed actions**: Every action that mutates session data carries a `SessionId` parameter
4. **Atomic switching**: Session switch is a single action (`SetActiveSessionAction`) — all selectors re-derive from `ActiveSessionId`
5. **In-memory MVP**: State is lost on circuit disconnect — no persistence (matches current behavior per Q-STATE-005)
6. **Declarative derivation**: Components select data via `Sessions[ActiveSessionId]` — never cache session state locally

### 2.2 Before/After Comparison

| Aspect | Before (Current) | After (Multi-Session) |
|--------|-------------------|----------------------|
| Stores | 4 independent features | 1 unified `SessionManagerState` feature |
| Session concept | None | `SessionMetadata` + `SessionState` per session |
| Actions | Global (no session ID) | All carry `string SessionId` |
| Reducers | Operate on root state | Operate on `Sessions[action.SessionId]` |
| Switching | Destroy + rebuild | Atomic `ActiveSessionId` update |
| Background streaming | Impossible | Per-session `SessionStreamingContext` |
| StateManager | Holds global recipe | Stateless utility; recipe in `SessionState` |
| AgentStreamingService | Per-circuit singleton state | Manages `Dictionary<string, SessionStreamingContext>` |

---

## 3. SessionState Record

`SessionState` consolidates all per-session data from the 4 current stores into a single immutable record. This is the leaf value in the `Sessions` dictionary.

```csharp
namespace AGUIDojoClient.Store.SessionState;

/// <summary>
/// Immutable record containing all state for a single chat session.
/// Consolidates fields from AgentState, ChatState, PlanState, and ArtifactState
/// into a per-session context.
/// </summary>
public sealed record SessionState
{
    // ── Chat State (from ChatState) ──────────────────────────────
    public ImmutableList<ChatMessage> Messages { get; init; }
        = ImmutableList<ChatMessage>.Empty;
    public ChatMessage? CurrentResponseMessage { get; init; }
    public string? ConversationId { get; init; }
    public int StatefulMessageCount { get; init; }
    public PendingApproval? PendingApproval { get; init; }

    // ── Agent Run State (from AgentState) ───────────────────────
    public bool IsRunning { get; init; }
    public string? CurrentAuthorName { get; init; }

    // ── Plan State (from PlanState) ─────────────────────────────
    public Plan? Plan { get; init; }
    public DiffState? PlanDiff { get; init; }

    // ── Artifact State (from ArtifactState) ─────────────────────
    public Recipe? CurrentRecipe { get; init; }
    public DocumentState? CurrentDocumentState { get; init; }
    public bool IsDocumentPreview { get; init; } = true;
    public bool HasInteractiveArtifact { get; init; }
    public ArtifactType ActiveArtifactType { get; init; }
        = ArtifactType.None;
    public DiffPreviewData? DiffPreview { get; init; }
    public DataGridResult? CurrentDataGrid { get; init; }
    public ImmutableHashSet<ArtifactType> VisibleTabs { get; init; }
        = ImmutableHashSet<ArtifactType>.Empty;
}
```

### 3.1 Field Migration Map

| Original Store | Original Field | SessionState Field | Notes |
|---------------|---------------|-------------------|-------|
| `ChatState` | `Messages` | `Messages` | Direct move |
| `ChatState` | `CurrentResponseMessage` | `CurrentResponseMessage` | Direct move |
| `ChatState` | `ConversationId` | `ConversationId` | Direct move |
| `ChatState` | `StatefulMessageCount` | `StatefulMessageCount` | Per Q-HIST-003 |
| `ChatState` | `PendingApproval` | `PendingApproval` | Direct move |
| `AgentState` | `IsRunning` | `IsRunning` | Per-session streaming |
| `AgentState` | `CurrentAuthorName` | `CurrentAuthorName` | Per-session author |
| `AgentState` | `SelectedEndpointPath` | *Removed* | Unified endpoint — no longer needed (see §2 of design-section-01) |
| `PlanState` | `Plan` | `Plan` | Direct move |
| `PlanState` | `Diff` | `PlanDiff` | Renamed for clarity |
| `ArtifactState` | All fields | All artifact fields | Direct move |
| `StateManager` | `CurrentRecipe` | `CurrentRecipe` | Moved from service into session state (Q-STATE-006) |

### 3.2 What Stays Global

Not all state belongs in `SessionState`. These remain global:

| Field | Reason |
|-------|--------|
| `ActiveSessionId` | Determines which session is displayed — global by definition |
| `Sessions` dictionary | Container for all sessions — global by definition |
| Session ordering/sort | UI concern, not per-session |
| UI preferences (theme, sidebar collapsed) | User-level, not session-level |

---

## 4. SessionMetadata Model

`SessionMetadata` is the lightweight descriptor for each session, used by the session list sidebar and session search. It is stored alongside `SessionState` in the sessions dictionary.

```csharp
namespace AGUIDojoClient.Models;

/// <summary>
/// Lightweight metadata for a chat session. Displayed in the session
/// list sidebar without exposing the full session state.
/// </summary>
public sealed record SessionMetadata
{
    /// <summary>Unique session identifier (UUID v4).</summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display title. Starts as "New Chat", then auto-generated from first
    /// user message (truncated to 50 chars) or LLM-generated after first response.
    /// </summary>
    public string Title { get; init; } = "New Chat";

    /// <summary>Current lifecycle status of the session.</summary>
    public SessionStatus Status { get; init; } = SessionStatus.Created;

    /// <summary>When the session was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the session last received or sent a message (UTC).</summary>
    public DateTimeOffset LastActivityAt { get; init; }

    /// <summary>
    /// Count of unread messages since the user last viewed this session.
    /// Incremented when a background session receives assistant responses.
    /// Reset to 0 when the user switches to this session.
    /// </summary>
    public int UnreadCount { get; init; }

    /// <summary>
    /// Whether this session has a pending HITL approval request.
    /// Used to display urgent notification badges in the session list.
    /// </summary>
    public bool HasPendingApproval { get; init; }
}

/// <summary>
/// Lifecycle status of a chat session.
/// Transitions: Created → Active → Background → Completed → Destroyed
/// </summary>
public enum SessionStatus
{
    /// <summary>Session created but no messages sent yet.</summary>
    Created,

    /// <summary>Session is the active (foreground) session with ongoing conversation.</summary>
    Active,

    /// <summary>Session is streaming in background while user views another session.</summary>
    Background,

    /// <summary>Agent finished response. Session ready for further interaction.</summary>
    Completed,

    /// <summary>Session marked for cleanup. State will be freed.</summary>
    Destroyed
}
```

> **Ref:** Q-STATE-004 — "Proposed lifecycle: Created → Title → Active → Background → Completed → Destroyed"

---

## 5. SessionManagerState Feature

This is the **single Fluxor feature** that replaces all 4 existing features. It holds all sessions as a session-keyed `ImmutableDictionary`.

```csharp
namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Root Fluxor state for multi-session management.
/// Contains all session data keyed by session ID, plus the active session pointer.
/// Replaces AgentState, ChatState, PlanState, and ArtifactState features.
/// </summary>
public sealed record SessionManagerState
{
    /// <summary>
    /// All sessions keyed by session ID.
    /// Each entry contains both metadata and full session state.
    /// </summary>
    public ImmutableDictionary<string, SessionEntry> Sessions { get; init; }
        = ImmutableDictionary<string, SessionEntry>.Empty;

    /// <summary>
    /// The ID of the currently active (foreground) session, or null if no session exists.
    /// All component selectors derive displayed data from Sessions[ActiveSessionId].
    /// </summary>
    public string? ActiveSessionId { get; init; }

    /// <summary>
    /// Ordered list of session IDs for sidebar display (most recent first).
    /// Maintained separately from the dictionary to preserve explicit ordering.
    /// </summary>
    public ImmutableList<string> SessionOrder { get; init; }
        = ImmutableList<string>.Empty;
}

/// <summary>
/// Container for a session's metadata and full state, stored as a dictionary value.
/// </summary>
public sealed record SessionEntry
{
    /// <summary>Lightweight metadata (title, status, timestamps, unread).</summary>
    public required SessionMetadata Metadata { get; init; }

    /// <summary>Full session state (messages, plan, artifacts, etc.).</summary>
    public required SessionState State { get; init; }
}
```

### 5.1 Feature Registration

```csharp
namespace AGUIDojoClient.Store.SessionManager;

public class SessionManagerFeature : Feature<SessionManagerState>
{
    public override string GetName() => "SessionManager";
    protected override SessionManagerState GetInitialState() => new();
}
```

### 5.2 Active Session Convenience Selectors

Components need efficient access to the active session's state without verbose dictionary lookups. A static selector helper centralizes this:

```csharp
namespace AGUIDojoClient.Store.SessionManager;

/// <summary>
/// Convenience selectors that derive active session data from SessionManagerState.
/// Components use these instead of raw dictionary lookups.
/// </summary>
public static class SessionSelectors
{
    /// <summary>Gets the active session entry, or null if no session exists.</summary>
    public static SessionEntry? ActiveEntry(SessionManagerState state) =>
        state.ActiveSessionId is not null
        && state.Sessions.TryGetValue(state.ActiveSessionId, out var entry)
            ? entry : null;

    /// <summary>Gets the active session's state, or null.</summary>
    public static SessionState? ActiveSessionState(SessionManagerState state) =>
        ActiveEntry(state)?.State;

    /// <summary>Gets the active session's metadata, or null.</summary>
    public static SessionMetadata? ActiveMetadata(SessionManagerState state) =>
        ActiveEntry(state)?.Metadata;

    /// <summary>Gets the active session's messages, or empty list.</summary>
    public static ImmutableList<ChatMessage> ActiveMessages(SessionManagerState state) =>
        ActiveSessionState(state)?.Messages ?? ImmutableList<ChatMessage>.Empty;

    /// <summary>Gets whether the active session's agent is running.</summary>
    public static bool IsActiveSessionRunning(SessionManagerState state) =>
        ActiveSessionState(state)?.IsRunning ?? false;

    /// <summary>Gets all session metadata for sidebar display, ordered.</summary>
    public static IEnumerable<SessionMetadata> OrderedSessionMetadata(SessionManagerState state) =>
        state.SessionOrder
            .Select(id => state.Sessions.TryGetValue(id, out var e) ? e.Metadata : null)
            .Where(m => m is not null)!;
}
```

---

## 6. Reducer Patterns

### 6.1 Action Design: All Actions Carry SessionId

Every action that mutates session-specific state includes a `SessionId` parameter. This is the fundamental shift from the current global action model.

```csharp
namespace AGUIDojoClient.Store.SessionManager;

public static class SessionActions
{
    // ── Session Lifecycle ────────────────────────────────────────
    public sealed record CreateSessionAction(string SessionId, DateTimeOffset CreatedAt);
    public sealed record SetActiveSessionAction(string SessionId);
    public sealed record DestroySessionAction(string SessionId);
    public sealed record UpdateSessionTitleAction(string SessionId, string Title);
    public sealed record SetSessionStatusAction(string SessionId, SessionStatus Status);

    // ── Chat Actions (carry SessionId) ──────────────────────────
    public sealed record AddMessageAction(string SessionId, ChatMessage Message);
    public sealed record UpdateResponseMessageAction(string SessionId, ChatMessage? ResponseMessage);
    public sealed record SetConversationIdAction(string SessionId, string? ConversationId);
    public sealed record SetPendingApprovalAction(string SessionId, PendingApproval? PendingApproval);
    public sealed record ClearSessionMessagesAction(string SessionId, string SystemPrompt);
    public sealed record SetStatefulCountAction(string SessionId, int Count);
    public sealed record TrimMessagesAction(string SessionId, int KeepCount);

    // ── Agent Run Actions (carry SessionId) ─────────────────────
    public sealed record SetRunningAction(string SessionId, bool IsRunning);
    public sealed record SetAuthorNameAction(string SessionId, string? AuthorName);

    // ── Plan Actions (carry SessionId) ──────────────────────────
    public sealed record SetPlanAction(string SessionId, Plan Plan);
    public sealed record ApplyPlanDeltaAction(string SessionId, IEnumerable<JsonPatchOperation> Operations);
    public sealed record ClearPlanAction(string SessionId);
    public sealed record SetPlanDiffAction(string SessionId, DiffState? Diff);

    // ── Artifact Actions (carry SessionId) ──────────────────────
    public sealed record SetRecipeAction(string SessionId, Recipe Recipe);
    public sealed record SetDocumentAction(string SessionId, DocumentState DocumentState);
    public sealed record SetDocumentPreviewAction(string SessionId, bool IsPreview);
    public sealed record ClearArtifactsAction(string SessionId);
    public sealed record SetDiffPreviewAction(string SessionId, object? Before, object? After, string Title);
    public sealed record SetDataGridAction(string SessionId, DataGridResult DataGrid);
    public sealed record SetActiveArtifactAction(string SessionId, ArtifactType ArtifactType);

    // ── Notification Actions (carry SessionId) ──────────────────
    public sealed record IncrementUnreadAction(string SessionId);
    public sealed record ClearUnreadAction(string SessionId);
    public sealed record SetHasPendingApprovalAction(string SessionId, bool HasPending);
}
```

### 6.2 Reducer Pattern: Session-Scoped Mutation

All reducers follow a consistent pattern: extract the target session from the dictionary, produce a new `SessionState`, put it back.

```csharp
namespace AGUIDojoClient.Store.SessionManager;

public static class SessionReducers
{
    // ── Helper: Apply a mutation to a specific session's state ───
    private static SessionManagerState UpdateSession(
        SessionManagerState state,
        string sessionId,
        Func<SessionState, SessionState> mutateState)
    {
        if (!state.Sessions.TryGetValue(sessionId, out var entry))
            return state; // Session not found — no-op

        var newEntry = entry with { State = mutateState(entry.State) };
        return state with { Sessions = state.Sessions.SetItem(sessionId, newEntry) };
    }

    private static SessionManagerState UpdateMetadata(
        SessionManagerState state,
        string sessionId,
        Func<SessionMetadata, SessionMetadata> mutateMetadata)
    {
        if (!state.Sessions.TryGetValue(sessionId, out var entry))
            return state;

        var newEntry = entry with { Metadata = mutateMetadata(entry.Metadata) };
        return state with { Sessions = state.Sessions.SetItem(sessionId, newEntry) };
    }

    // ── Session Lifecycle ────────────────────────────────────────

    [ReducerMethod]
    public static SessionManagerState OnCreateSession(
        SessionManagerState state, SessionActions.CreateSessionAction action)
    {
        var metadata = new SessionMetadata
        {
            Id = action.SessionId,
            CreatedAt = action.CreatedAt,
            LastActivityAt = action.CreatedAt,
            Status = SessionStatus.Created
        };
        var sessionState = new SessionState();
        var entry = new SessionEntry { Metadata = metadata, State = sessionState };

        return state with
        {
            Sessions = state.Sessions.Add(action.SessionId, entry),
            SessionOrder = state.SessionOrder.Insert(0, action.SessionId),
            ActiveSessionId = action.SessionId
        };
    }

    [ReducerMethod]
    public static SessionManagerState OnSetActiveSession(
        SessionManagerState state, SessionActions.SetActiveSessionAction action)
    {
        if (!state.Sessions.ContainsKey(action.SessionId))
            return state;

        // Mark previous active session as Background if it was streaming
        var newState = state;
        if (state.ActiveSessionId is not null
            && state.Sessions.TryGetValue(state.ActiveSessionId, out var prevEntry)
            && prevEntry.State.IsRunning)
        {
            newState = UpdateMetadata(newState, state.ActiveSessionId,
                m => m with { Status = SessionStatus.Background });
        }

        // Set new active session, clear unread, set status to Active
        newState = UpdateMetadata(newState, action.SessionId,
            m => m with { Status = SessionStatus.Active, UnreadCount = 0 });

        return newState with { ActiveSessionId = action.SessionId };
    }

    [ReducerMethod]
    public static SessionManagerState OnDestroySession(
        SessionManagerState state, SessionActions.DestroySessionAction action)
    {
        var newSessions = state.Sessions.Remove(action.SessionId);
        var newOrder = state.SessionOrder.Remove(action.SessionId);

        // If destroying the active session, activate the next one
        var newActiveId = state.ActiveSessionId == action.SessionId
            ? newOrder.FirstOrDefault()
            : state.ActiveSessionId;

        return state with
        {
            Sessions = newSessions,
            SessionOrder = newOrder,
            ActiveSessionId = newActiveId
        };
    }

    // ── Chat Reducers (session-scoped) ──────────────────────────

    [ReducerMethod]
    public static SessionManagerState OnAddMessage(
        SessionManagerState state, SessionActions.AddMessageAction action) =>
        UpdateSession(state, action.SessionId,
            s => s with { Messages = s.Messages.Add(action.Message) });

    [ReducerMethod]
    public static SessionManagerState OnUpdateResponseMessage(
        SessionManagerState state, SessionActions.UpdateResponseMessageAction action) =>
        UpdateSession(state, action.SessionId,
            s => s with { CurrentResponseMessage = action.ResponseMessage });

    [ReducerMethod]
    public static SessionManagerState OnSetRunning(
        SessionManagerState state, SessionActions.SetRunningAction action) =>
        UpdateSession(state, action.SessionId,
            s => s with { IsRunning = action.IsRunning });

    [ReducerMethod]
    public static SessionManagerState OnSetPlan(
        SessionManagerState state, SessionActions.SetPlanAction action) =>
        UpdateSession(state, action.SessionId,
            s => s with { Plan = action.Plan });

    [ReducerMethod]
    public static SessionManagerState OnSetRecipe(
        SessionManagerState state, SessionActions.SetRecipeAction action) =>
        UpdateSession(state, action.SessionId,
            s => s with
            {
                CurrentRecipe = action.Recipe,
                HasInteractiveArtifact = true,
                VisibleTabs = s.VisibleTabs.Add(ArtifactType.RecipeEditor),
                ActiveArtifactType = s.ActiveArtifactType == ArtifactType.None
                    ? ArtifactType.RecipeEditor : s.ActiveArtifactType
            });

    // ... Additional reducers follow the same UpdateSession pattern
    // Each existing reducer maps 1:1 to a session-scoped version
}
```

### 6.3 Migration Rule

Every existing reducer transforms mechanically:

| Before | After |
|--------|-------|
| `public static ChatState OnAddMessage(ChatState state, AddMessageAction action)` | `public static SessionManagerState OnAddMessage(SessionManagerState state, AddMessageAction action) => UpdateSession(state, action.SessionId, s => ...)` |
| Direct field update: `state with { Messages = ... }` | Nested update: `UpdateSession(state, id, s => s with { Messages = ... })` |

The `UpdateSession` helper ensures:
1. Session existence check (no-op if missing)
2. Immutable dictionary update via `SetItem`
3. Consistent pattern across all reducers

---

## 7. Atomic Session Switching

### 7.1 The Problem

When a user switches sessions, ALL displayed data must change atomically: messages, plan, artifacts, agent status. If these update in separate dispatches, components could briefly render a mix of old-session and new-session data.

### 7.2 The Solution

With the session-keyed dictionary pattern, session switching is **inherently atomic**.

```
User clicks Session B →
  Dispatch: SetActiveSessionAction("session-b") →
    Reducer: state with { ActiveSessionId = "session-b" } →
      Single state change notification →
        ALL selectors re-evaluate: Sessions["session-b"].Messages, .Plan, .Artifacts, etc.
```

A single `SetActiveSessionAction` produces a single new `SessionManagerState` with an updated `ActiveSessionId`. Fluxor's single-dispatch model guarantees all subscribed components receive the same state snapshot. Components never see partial updates because:

1. **Single dispatch**: Only one action is dispatched for switching
2. **Selector derivation**: All component data derives from `Sessions[ActiveSessionId]`
3. **Immutable snapshots**: The state reference changes once; components re-render from the new snapshot

> **Ref:** Q-STATE-002 — "With the dictionary approach, session switching is a single action that produces a new state with updated ActiveSessionId. All component selectors derive from Sessions[ActiveSessionId], so they ALL update in the same Fluxor dispatch cycle."

### 7.3 Proof: No Partial State Swap

Consider the failure mode where partial swaps could occur:

| Approach | Dispatches for Switch | Atomic? |
|----------|----------------------|---------|
| ❌ 4 separate stores (current) | 4 dispatches (clear chat, clear plan, clear artifacts, set endpoint) | No — components see intermediate states |
| ✅ Session-keyed dictionary | 1 dispatch (SetActiveSessionAction) | Yes — single state transition |

With the current 4-store model, `ResetConversation()` dispatches 6 separate actions. With the session-keyed model, switching never resets anything — it just changes the pointer.

### 7.4 Background Session State Preservation

When switching from Session A (streaming) to Session B:
1. Session A continues streaming in the background (its `SessionStreamingContext` runs independently)
2. Session A's state in the dictionary continues to be updated by background dispatches
3. The UI shows Session B's state because `ActiveSessionId` now points to B
4. Session A's unread count increments for each background response
5. Switching back to Session A instantly shows its updated state — no reload needed

---

## 8. SessionStreamingContext

The `SessionStreamingContext` class extracts all per-session streaming state from `AgentStreamingService`. This is the counterpart to `SessionState` in Fluxor — while `SessionState` holds the persisted/visible state, `SessionStreamingContext` holds the transient streaming infrastructure state.

> **Ref:** Q-STATE-003 — "Create a SessionStreamingContext class containing all per-session streaming state."

```csharp
namespace AGUIDojoClient.Services;

/// <summary>
/// Contains all transient streaming infrastructure state for a single session.
/// Extracted from AgentStreamingService's per-circuit fields.
/// Each active session gets its own context, enabling background streaming.
/// </summary>
public sealed class SessionStreamingContext : IDisposable
{
    /// <summary>Cancellation source for the active SSE stream in this session.</summary>
    public CancellationTokenSource? ResponseCancellation { get; set; }

    /// <summary>Deduplication set for function call IDs (prevents duplicate tool renders).</summary>
    public HashSet<string> SeenFunctionCallIds { get; } = new();

    /// <summary>Deduplication set for function result IDs.</summary>
    public HashSet<string> SeenFunctionResultCallIds { get; } = new();

    /// <summary>Maps function call IDs to tool names for artifact dispatch.</summary>
    public Dictionary<string, string> FunctionCallIdToToolName { get; } = new();

    /// <summary>Chat options with session-specific ConversationId.</summary>
    public ChatOptions ChatOptions { get; } = new();

    /// <summary>Pending HITL approval task source. Only one per session at a time.</summary>
    public TaskCompletionSource<bool>? ApprovalTaskSource { get; set; }

    /// <summary>The in-flight streaming message being accumulated.</summary>
    public ChatMessage? StreamingMessage { get; set; }

    /// <summary>Diff preview before/after state for this session.</summary>
    public object? LastDiffBefore { get; set; }
    public object? LastDiffAfter { get; set; }
    public string LastDiffTitle { get; set; } = "State Diff";

    /// <summary>
    /// Resets all transient state for a new conversation within the same session.
    /// Called when the user explicitly clears the conversation.
    /// </summary>
    public void Reset()
    {
        ResponseCancellation?.Cancel();
        ResponseCancellation?.Dispose();
        ResponseCancellation = null;
        SeenFunctionCallIds.Clear();
        SeenFunctionResultCallIds.Clear();
        FunctionCallIdToToolName.Clear();
        ChatOptions.ConversationId = null;
        ApprovalTaskSource?.TrySetCanceled();
        ApprovalTaskSource = null;
        StreamingMessage = null;
        LastDiffBefore = null;
        LastDiffAfter = null;
        LastDiffTitle = "State Diff";
    }

    public void Dispose()
    {
        ResponseCancellation?.Cancel();
        ResponseCancellation?.Dispose();
        ApprovalTaskSource?.TrySetCanceled();
    }
}
```

### 8.1 Field Migration from AgentStreamingService

| AgentStreamingService Field | SessionStreamingContext Field | Notes |
|----------------------------|------------------------------|-------|
| `_currentResponseCancellation` | `ResponseCancellation` | Per-session CTS enables independent cancel |
| `_seenFunctionCallIds` | `SeenFunctionCallIds` | Per-session dedup |
| `_seenFunctionResultCallIds` | `SeenFunctionResultCallIds` | Per-session dedup |
| `_functionCallIdToToolName` | `FunctionCallIdToToolName` | Per-session tool tracking |
| `_chatOptions` | `ChatOptions` | Per-session ConversationId |
| `_approvalTaskSource` | `ApprovalTaskSource` | Per-session HITL approval |
| `_streamingMessage` | `StreamingMessage` | Per-session streaming accumulator |
| `_lastDiffBefore/After/Title` | `LastDiffBefore/After/Title` | Per-session diff preview |

---

## 9. AgentStreamingService Refactoring

### 9.1 Before: Single-Session Service

```
AgentStreamingService (Scoped per circuit)
├── _currentResponseCancellation  ← ONE per circuit
├── _seenFunctionCallIds          ← ONE per circuit
├── _chatOptions                  ← ONE per circuit
├── _approvalTaskSource           ← ONE per circuit
└── ProcessAgentResponseAsync()   ← Operates on global state
```

### 9.2 After: Multi-Session Service

```
AgentStreamingService (Scoped per circuit)
├── _contexts: Dictionary<string, SessionStreamingContext>
├── _dispatcher, _stores (Fluxor injections)
├── GetOrCreateContext(sessionId): SessionStreamingContext
├── ProcessAgentResponseAsync(sessionId, chatClient): Task
├── CancelSession(sessionId): void
├── ResetSession(sessionId, systemPrompt): void
├── DestroySession(sessionId): void
└── ResolveApproval(sessionId, approved): void
```

### 9.3 Key API Changes

All public methods gain a `sessionId` parameter:

```csharp
public sealed class AgentStreamingService : IAgentStreamingService
{
    private readonly ConcurrentDictionary<string, SessionStreamingContext> _contexts = new();

    // Injected dependencies remain the same
    private readonly IDispatcher _dispatcher;
    private readonly IState<SessionManagerState> _sessionStore;
    // ... other injections

    /// <summary>
    /// Gets or creates a streaming context for the specified session.
    /// </summary>
    private SessionStreamingContext GetOrCreateContext(string sessionId) =>
        _contexts.GetOrAdd(sessionId, _ => new SessionStreamingContext());

    /// <summary>
    /// Processes the agent response stream for a specific session.
    /// Can run concurrently for multiple sessions (background streaming).
    /// </summary>
    public async Task ProcessAgentResponseAsync(string sessionId, IChatClient chatClient)
    {
        var ctx = GetOrCreateContext(sessionId);
        ctx.ResponseCancellation = new CancellationTokenSource();

        _dispatcher.Dispatch(new SessionActions.SetRunningAction(sessionId, true));

        try
        {
            var responseText = new TextContent("");
            ctx.StreamingMessage = new ChatMessage(ChatRole.Assistant, [responseText]);
            _dispatcher.Dispatch(
                new SessionActions.UpdateResponseMessageAction(sessionId, ctx.StreamingMessage));

            await foreach (var update in chatClient.GetStreamingResponseAsync(
                /* messages from session state */,
                ctx.ChatOptions,
                ctx.ResponseCancellation.Token))
            {
                // Process update using ctx.SeenFunctionCallIds, etc.
                // All dispatches include sessionId:
                //   _dispatcher.Dispatch(new SessionActions.AddMessageAction(sessionId, msg));

                // If session is not active (background), increment unread:
                if (_sessionStore.Value.ActiveSessionId != sessionId)
                {
                    _dispatcher.Dispatch(
                        new SessionActions.IncrementUnreadAction(sessionId));
                }
            }
        }
        finally
        {
            _dispatcher.Dispatch(new SessionActions.SetRunningAction(sessionId, false));
            _dispatcher.Dispatch(
                new SessionActions.SetSessionStatusAction(sessionId, SessionStatus.Completed));
        }
    }

    /// <summary>
    /// Cancels the active stream for a specific session without affecting others.
    /// </summary>
    public void CancelSession(string sessionId)
    {
        if (_contexts.TryGetValue(sessionId, out var ctx))
        {
            ctx.ResponseCancellation?.Cancel();
            _dispatcher.Dispatch(new SessionActions.SetRunningAction(sessionId, false));
        }
    }

    /// <summary>
    /// Destroys a session's streaming context and frees resources.
    /// </summary>
    public void DestroySession(string sessionId)
    {
        if (_contexts.TryRemove(sessionId, out var ctx))
        {
            ctx.Dispose();
        }
    }
}
```

### 9.4 Concurrent Streaming

With session-keyed contexts, multiple sessions can stream simultaneously:

```
Session A: ProcessAgentResponseAsync("sess-a", chatClient) → runs on Task
Session B: ProcessAgentResponseAsync("sess-b", chatClient) → runs on Task
```

Each uses its own `SessionStreamingContext` with independent:
- `CancellationTokenSource` (cancel A without affecting B)
- Deduplication sets (no cross-session tool ID collision)
- `ChatOptions` (independent ConversationId per session)
- Approval state (per-session HITL blocking)

**Thread Safety**: `ConcurrentDictionary` for the contexts map. Fluxor's `IDispatcher.Dispatch()` is thread-safe — dispatches from background tasks are safe. Individual `SessionStreamingContext` instances are accessed by a single streaming task, so no internal synchronization is needed.

### 9.5 Resource Limits

> **Ref:** R13 — "Practical limit: 3-5 concurrent active streams recommended."

The `AgentStreamingService` should enforce a maximum concurrent streaming limit:

```csharp
private const int MaxConcurrentStreams = 5;

public async Task ProcessAgentResponseAsync(string sessionId, IChatClient chatClient)
{
    int activeCount = _contexts.Values.Count(c => c.ResponseCancellation is not null
        && !c.ResponseCancellation.IsCancellationRequested);

    if (activeCount >= MaxConcurrentStreams)
    {
        // Queue or reject — design decision for task-5 (notifications)
        throw new InvalidOperationException(
            $"Maximum concurrent streams ({MaxConcurrentStreams}) reached.");
    }

    // ... proceed with streaming
}
```

---

## 10. IStateManager Refactoring

### 10.1 Current Role

`StateManager` currently serves three roles:
1. **State owner**: Holds `CurrentRecipe` as a mutable singleton
2. **Serializer**: Creates `DataContent` from recipe for AG-UI state sync
3. **Parser**: Extracts `Recipe?` from `DataContent` snapshots

### 10.2 Target: Stateless Utility

With recipe state moved into `SessionState.CurrentRecipe`, `StateManager` sheds its state-owning role and becomes a pure utility service.

> **Ref:** Q-STATE-006 — "StateManager becomes a stateless service that creates DataContent from a given recipe and parses snapshots — no longer owning the recipe state itself."

```csharp
namespace AGUIDojoClient.Services;

/// <summary>
/// Stateless utility for recipe serialization/deserialization.
/// No longer owns recipe state — that lives in SessionState.
/// </summary>
public sealed class RecipeSerializer : IRecipeSerializer
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Creates a DataContent payload from a recipe for AG-UI state sync.
    /// </summary>
    public DataContent CreateStateContent(Recipe recipe)
    {
        string json = JsonSerializer.Serialize(recipe, s_jsonOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return new DataContent(bytes, "application/json");
    }

    /// <summary>
    /// Tries to extract a Recipe from a DataContent snapshot.
    /// Returns false if the content is not a recipe (e.g., plan or document).
    /// </summary>
    public bool TryExtractRecipeSnapshot(DataContent dataContent, out Recipe? recipe)
    {
        // Same heuristic logic as current StateManager.TryExtractRecipeSnapshot()
        // but as a pure function — no side effects, no state mutation
    }

    /// <summary>
    /// Creates a default recipe template for new sessions.
    /// </summary>
    public static Recipe CreateDefaultRecipe() => new()
    {
        Title = string.Empty,
        SkillLevel = "Beginner",
        CookingTime = "30 minutes",
        SpecialPreferences = [],
        Ingredients =
        [
            new Ingredient { Icon = "🍅", Name = "Tomatoes", Amount = "2 cups" },
            new Ingredient { Icon = "🧅", Name = "Onion", Amount = "1 medium" },
            new Ingredient { Icon = "🧄", Name = "Garlic", Amount = "3 cloves" }
        ],
        Instructions = []
    };
}
```

### 10.3 Impact on Consumers

| Consumer | Before | After |
|----------|--------|-------|
| `AgentStreamingService` | `_stateManager.UpdateFromServerSnapshot(recipe)` | `Dispatch(new SetRecipeAction(sessionId, recipe))` |
| `AgentStreamingService` | `_stateManager.CurrentRecipe` | `SessionSelectors.ActiveSessionState(store.Value)?.CurrentRecipe` |
| `AgentStreamingService` | `_stateManager.CreateStateContent()` | `_recipeSerializer.CreateStateContent(currentRecipe)` |
| `RecipeEditor` component | `_stateManager.UpdateRecipe(recipe)` + event handling | `Dispatch(new SetRecipeAction(sessionId, recipe))` |
| `Chat.razor` | `_stateManager.Initialize()` on endpoint switch | Recipe initialization via `CreateSessionAction` reducer |

---

## 11. Data Flow

### 11.1 Primary Flow: User Action → State Update → Re-render

```
┌─────────────┐    ┌──────────┐    ┌────────────────────────┐    ┌────────────┐
│ User Action  │───>│ Dispatch │───>│ SessionReducer          │───>│ Components │
│ (send msg)   │    │ Action   │    │ UpdateSession(state,    │    │ re-render  │
│              │    │ w/ sessId│    │   sessId, s => s with   │    │ via IState │
│              │    │          │    │   { Messages = ... })   │    │ .StateChanged
└─────────────┘    └──────────┘    └────────────────────────┘    └────────────┘
```

### 11.2 Background Streaming Flow

```
┌──────────────────────────────────────────────────────────────────────┐
│ User sends message in Session A                                       │
│ → ProcessAgentResponseAsync("sess-a", chatClient) starts on Task      │
│                                                                       │
│ User switches to Session B (SetActiveSessionAction("sess-b"))         │
│ → UI instantly shows Session B state                                  │
│ → Session A streaming continues in background                         │
│                                                                       │
│ Session A receives streaming token:                                   │
│ → Dispatch(AddMessageAction("sess-a", msg))                          │
│ → Dispatch(IncrementUnreadAction("sess-a"))                          │
│ → SessionReducer updates Sessions["sess-a"].State.Messages           │
│ → Session list sidebar re-renders (shows unread badge on A)          │
│ → Chat area does NOT re-render (shows Session B)                     │
│                                                                       │
│ User switches back to Session A:                                      │
│ → SetActiveSessionAction("sess-a")                                   │
│ → UI instantly shows Session A with all accumulated messages         │
│ → Unread count cleared                                                │
└──────────────────────────────────────────────────────────────────────┘
```

### 11.3 Session Creation Flow

```
1. User clicks "New Chat" button
2. Generate sessionId = Guid.NewGuid().ToString()
3. Dispatch: CreateSessionAction(sessionId, DateTimeOffset.UtcNow)
4. Reducer: Creates SessionEntry with empty SessionState,
   adds to Sessions dictionary, sets as ActiveSessionId
5. Components re-render: empty chat, no artifacts, no plan
6. User types first message → Dispatch: AddMessageAction(sessionId, userMsg)
7. Auto-title: UpdateSessionTitleAction(sessionId, userMsg.Text[..50])
8. Start streaming: ProcessAgentResponseAsync(sessionId, chatClient)
```

---

## 12. State Shape Diagram

```
SessionManagerState
│
├── ActiveSessionId: "sess-abc-123"
│
├── SessionOrder: ["sess-abc-123", "sess-def-456", "sess-ghi-789"]
│
└── Sessions: ImmutableDictionary<string, SessionEntry>
    │
    ├── "sess-abc-123" ─── SessionEntry
    │   ├── Metadata: SessionMetadata
    │   │   ├── Id: "sess-abc-123"
    │   │   ├── Title: "Help me cook pasta"
    │   │   ├── Status: Active
    │   │   ├── CreatedAt: 2026-02-24T10:00:00Z
    │   │   ├── LastActivityAt: 2026-02-24T10:05:32Z
    │   │   ├── UnreadCount: 0
    │   │   └── HasPendingApproval: false
    │   │
    │   └── State: SessionState
    │       ├── Messages: ImmutableList<ChatMessage> [5 items]
    │       ├── CurrentResponseMessage: null (not streaming)
    │       ├── ConversationId: "conv-xyz"
    │       ├── StatefulMessageCount: 5
    │       ├── PendingApproval: null
    │       ├── IsRunning: false
    │       ├── CurrentAuthorName: "Chef Agent"
    │       ├── Plan: null
    │       ├── PlanDiff: null
    │       ├── CurrentRecipe: Recipe { Title: "Pasta Carbonara", ... }
    │       ├── CurrentDocumentState: null
    │       ├── IsDocumentPreview: true
    │       ├── HasInteractiveArtifact: true
    │       ├── ActiveArtifactType: RecipeEditor
    │       ├── DiffPreview: null
    │       ├── CurrentDataGrid: null
    │       └── VisibleTabs: { RecipeEditor }
    │
    ├── "sess-def-456" ─── SessionEntry
    │   ├── Metadata: SessionMetadata
    │   │   ├── Id: "sess-def-456"
    │   │   ├── Title: "Weather analysis"
    │   │   ├── Status: Background  ← streaming in background
    │   │   ├── UnreadCount: 3      ← 3 unread since user switched away
    │   │   └── HasPendingApproval: false
    │   │
    │   └── State: SessionState
    │       ├── Messages: ImmutableList<ChatMessage> [12 items]
    │       ├── CurrentResponseMessage: ChatMessage (accumulating)
    │       ├── IsRunning: true      ← still streaming
    │       ├── Plan: Plan { Steps: [...] }
    │       └── ...
    │
    └── "sess-ghi-789" ─── SessionEntry
        ├── Metadata: { Title: "Email draft", Status: Completed }
        └── State: SessionState { Messages: [8 items], IsRunning: false }


AgentStreamingService._contexts (parallel structure, NOT in Fluxor)
│
├── "sess-abc-123" ─── SessionStreamingContext
│   ├── ResponseCancellation: null (idle)
│   ├── SeenFunctionCallIds: { "fc-1", "fc-2" }
│   ├── ChatOptions: { ConversationId: "conv-xyz" }
│   └── ApprovalTaskSource: null
│
├── "sess-def-456" ─── SessionStreamingContext
│   ├── ResponseCancellation: CTS (active)  ← streaming
│   ├── SeenFunctionCallIds: { "fc-3", "fc-4", "fc-5" }
│   ├── ChatOptions: { ConversationId: "conv-abc" }
│   └── StreamingMessage: ChatMessage (accumulating)
│
└── "sess-ghi-789" ─── SessionStreamingContext
    ├── ResponseCancellation: null (completed)
    └── SeenFunctionCallIds: { "fc-6" }
```

---

## 13. Selector Performance

### 13.1 The Challenge

Fluxor notifies ALL subscribers of `IState<SessionManagerState>` whenever any session changes. If Session B receives a background streaming token, components displaying Session A should NOT re-render.

### 13.2 Mitigation Strategies

**Strategy 1: Component-Level Change Detection**

Components compare the specific slice of state they need:

```csharp
// In Chat.razor — only re-render when active session's messages change
_sessionStore.StateChanged += (sender, args) =>
{
    var activeState = SessionSelectors.ActiveSessionState(_sessionStore.Value);
    if (activeState?.Messages != _lastRenderedMessages)
    {
        _lastRenderedMessages = activeState?.Messages;
        InvokeAsync(StateHasChanged);
    }
};
```

This is the same pattern currently used for `ThrottledStateHasChanged()` — checking if the relevant slice actually changed before triggering a render.

**Strategy 2: Selector Memoization**

For expensive derivations, memoized selectors avoid recomputation:

```csharp
public static class MemoizedSelectors
{
    private static string? _lastActiveId;
    private static SessionState? _cachedActiveState;

    public static SessionState? ActiveSessionState(SessionManagerState state)
    {
        if (state.ActiveSessionId == _lastActiveId && _cachedActiveState is not null)
            return _cachedActiveState;

        _lastActiveId = state.ActiveSessionId;
        _cachedActiveState = state.ActiveSessionId is not null
            && state.Sessions.TryGetValue(state.ActiveSessionId, out var entry)
                ? entry.State : null;
        return _cachedActiveState;
    }
}
```

**Strategy 3: Session List Optimization**

The session sidebar only needs `SessionMetadata`, not full `SessionState`. Because metadata changes less frequently than message streaming, the sidebar naturally re-renders less often:

```csharp
// SessionList component — only re-render when metadata order/content changes
var currentMetadata = SessionSelectors.OrderedSessionMetadata(_sessionStore.Value).ToList();
if (!currentMetadata.SequenceEqual(_lastRenderedMetadata))
{
    _lastRenderedMetadata = currentMetadata;
    InvokeAsync(StateHasChanged);
}
```

### 13.3 Expected Impact

| Component | Re-renders on Background Token? | Strategy |
|-----------|-------------------------------|----------|
| Chat message list | No (checks `ActiveSessionState?.Messages` reference equality) | Strategy 1 |
| Input box | No (only cares about `IsRunning` of active session) | Strategy 1 |
| Plan view | No (checks `ActiveSessionState?.Plan` reference equality) | Strategy 1 |
| Canvas pane | No (checks active session artifact fields) | Strategy 1 |
| Session list sidebar | Yes (unread count changed) | Strategy 3 (lightweight) |
| Panic button | No (checks `IsActiveSessionRunning`) | Strategy 1 |

---

## 14. Migration Strategy

### 14.1 Phased Approach

The migration from 4 stores to 1 session-keyed store should be done in phases to minimize risk:

**Phase 1: Create `SessionManagerState` alongside existing stores**
- Add `SessionManagerState`, `SessionState`, `SessionMetadata` records
- Add `SessionManagerFeature` to Fluxor
- Wire up `CreateSessionAction` → creates a session on app start
- At this point, both old stores and new store coexist

**Phase 2: Migrate reducers one store at a time**
- Start with `ChatState` → move `Messages`, `ConversationId`, `StatefulMessageCount` into `SessionState`
- Then `PlanState` → move `Plan`, `Diff` into `SessionState`
- Then `ArtifactState` → move all artifact fields into `SessionState`
- Then `AgentState` → move `IsRunning`, `CurrentAuthorName` (remove `SelectedEndpointPath`)
- Update components to read from `SessionSelectors` instead of individual `IState<T>`

**Phase 3: Refactor AgentStreamingService**
- Extract `SessionStreamingContext` class
- Replace per-circuit fields with `ConcurrentDictionary<string, SessionStreamingContext>`
- Add `sessionId` parameter to all public methods

**Phase 4: Refactor StateManager → RecipeSerializer**
- Remove `CurrentRecipe`, `HasActiveState`, `StateChanged` event
- Keep `CreateStateContent(Recipe)` and `TryExtractRecipeSnapshot()` as stateless utilities
- Rename to `RecipeSerializer`

**Phase 5: Remove old stores**
- Delete `AgentState/`, `ChatState/`, `PlanState/`, `ArtifactState/` directories
- Remove old feature registrations from DI

### 14.2 Backward Compatibility

During migration, components can temporarily read from both old and new stores. The `SessionSelectors` helper provides the new API surface while old stores are phased out. This prevents a big-bang rewrite.

---

## Appendix A: Alternatives Considered

### A.1 Multiple Fluxor Features (One per Session)

**Rejected.** Fluxor discovers features at startup via assembly scanning. Dynamic feature registration at runtime is not supported. Creating N features at compile time for N possible sessions is impractical.

### A.2 Custom Fluxor Middleware for Session Scoping

**Rejected.** Middleware intercepts dispatches globally and could theoretically route actions to session-specific sub-states. However, this fights Fluxor's design, adds complex custom infrastructure, and is harder to debug. The dictionary pattern achieves the same result with standard Fluxor mechanisms.

### A.3 Separate Fluxor Store Instances via DI Scoping

**Rejected.** Fluxor registers `IState<T>` as singletons within its `IServiceScope`. Creating per-session DI scopes would require forking Fluxor's initialization, and components would need manual scope resolution instead of standard DI injection.

> **Ref:** Q-STATE-001 — "Options (b) and (c) involve custom abstractions or middleware that fight the framework."

---

## Appendix B: Reference Index

| Ref | Description | Section Used |
|-----|-------------|-------------|
| Q-STATE-001 | Session-keyed dictionary recommended | §2, §6, Appendix A |
| Q-STATE-002 | Atomic switch via ActiveSessionId | §7 |
| Q-STATE-003 | SessionStreamingContext extraction | §8 |
| Q-STATE-004 | Session lifecycle states | §4 |
| Q-STATE-005 | In-memory only for MVP | §2.1 |
| Q-STATE-006 | IStateManager becomes stateless | §10 |
| Q-AGUI-005 | Multi-feature conversations | §3 (VisibleTabs in SessionState) |
| Q-HIST-003 | StatefulMessageCount per session | §3.1 |
| R5 | 4 Fluxor stores are global singletons | §1 |
| R6 | AgentStreamingService per-circuit state | §1.2, §8.1 |
| R7 | AGUIChatClientFactory hardcoded endpoints | §10.3 |
| R13 | Concurrent SSE: 3-5 max streams | §9.5 |
| ISS-006 | Concurrent SSE streams for multi-session | §9.5 |
| ISS-013 | AgentStreamingService complexity unspecified | §9 |
| ISS-015 | No session sidebar in layout | §5.2 |
