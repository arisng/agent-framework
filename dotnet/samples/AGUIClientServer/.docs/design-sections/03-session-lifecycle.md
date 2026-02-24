# Design Section 03: Session Lifecycle Management

> **Spec Section:** Client-Side Session Lifecycle  
> **Created:** 2026-02-24  
> **Status:** Design  
> **References:** Q-STATE-004, Q-STATE-005, Q-UX-003, Q-UX-005, Q-SYNC-003, Q-SYNC-004, Q-HIST-001, Q-NOTIF-004, R5, R6, R13, ISS-015, ISS-020, ISS-031  
> **Depends On:** 01-unified-endpoint.md, 02-multi-session-state.md  
> **Inherited By:** task-6, task-9

---

## Table of Contents

1. [Session Status Enum](#1-session-status-enum)
2. [Session Status Transition Diagram](#2-session-status-transition-diagram)
3. [Session Creation](#3-session-creation)
4. [Title Generation](#4-title-generation)
5. [Session Activation and Switching](#5-session-activation-and-switching)
6. [Background Session Behavior](#6-background-session-behavior)
7. [Concurrent Stream Cap and Queueing](#7-concurrent-stream-cap-and-queueing)
8. [Session Resume](#8-session-resume)
9. [Session Cleanup](#9-session-cleanup)
10. [HITL Approval in Background Sessions](#10-hitl-approval-in-background-sessions)
11. [Future: Session Persistence](#11-future-session-persistence)
12. [Status Transition Reference Table](#12-status-transition-reference-table)
13. [Interaction with Multi-Session State](#13-interaction-with-multi-session-state)

---

## 1. Session Status Enum

The `SessionStatus` enum defines the complete set of lifecycle states for a chat session. These statuses drive sidebar badges, notification behavior, session ordering, and resource cleanup decisions.

```csharp
/// <summary>
/// Lifecycle status of a chat session.
/// Represents both user-visible states and internal machinery states.
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// Session has been created but the user has not yet sent a message.
    /// Default status after "New Chat" click.
    /// </summary>
    Created,

    /// <summary>
    /// Session is the foreground (active) session with no streaming in progress.
    /// The user is viewing this session and can type messages.
    /// </summary>
    Active,

    /// <summary>
    /// Session's agent is currently streaming a response.
    /// The user is viewing this session (foreground streaming).
    /// </summary>
    Streaming,

    /// <summary>
    /// Session is streaming in the background — the user has switched to
    /// another session while this one is still receiving agent responses.
    /// This is NOT a user-visible status label; the sidebar shows a streaming
    /// indicator instead.
    /// </summary>
    Background,

    /// <summary>
    /// Session's agent finished its response. The conversation is idle and
    /// the user can send follow-up messages or switch away.
    /// </summary>
    Completed,

    /// <summary>
    /// An error occurred during streaming (network failure, server error,
    /// LLM rate limit). The user can retry or dismiss.
    /// </summary>
    Error,

    /// <summary>
    /// Session has been archived (soft-deleted). Not displayed in the
    /// active session list but retained for potential future restore.
    /// For MVP (in-memory only), Archived is equivalent to Destroyed since
    /// there is no persistence layer.
    /// </summary>
    Archived
}
```

### 1.1 Relationship to `SessionMetadata.Status`

The `SessionStatus` enum defined here **extends** the 5-value enum sketched in [02-multi-session-state.md §4](02-multi-session-state.md#4-sessionmetadata-model) by adding `Streaming`, `Error`, and `Archived`. The original enum (`Created`, `Active`, `Background`, `Completed`, `Destroyed`) is superseded by this 7-value version.

> **Ref:** Q-STATE-004 — "Proposed lifecycle: Created → Title → Active → Background → Completed → Destroyed"

The rationale for the additions:

| Added Status | Rationale |
|-------------|-----------|
| `Streaming` | Distinguishes "active + idle" from "active + receiving tokens". Enables the typing indicator and the "stop generation" button. Without this, `IsRunning` in `SessionState` would be the only signal, but `SessionMetadata.Status` is what drives sidebar UI. |
| `Error` | Enables retry UX and error badges in the sidebar. Without it, errors force status back to `Active` or `Completed`, losing context. |
| `Archived` | Replaces `Destroyed` for soft-deletion semantics. Future persistence can restore archived sessions. For MVP (in-memory), archival triggers immediate resource cleanup (equivalent to destruction). |

### 1.2 Status Visibility in UI

| Status | Sidebar Badge | Chat Area Indicator | Canvas Impact |
|--------|--------------|--------------------|----|
| `Created` | Dim/empty icon | Empty state: "Start a conversation" | Canvas hidden or default |
| `Active` | None (normal) | Idle chat input | Shows session's artifacts |
| `Streaming` | Animated pulse dot | Typing indicator, "Stop" button visible | Live artifact updates |
| `Background` | Animated pulse dot + unread count | N/A (user is viewing another session) | N/A |
| `Completed` | Checkmark or none | Idle chat input | Shows session's artifacts |
| `Error` | Red error badge | Error banner with retry action | Last known artifacts |
| `Archived` | Not shown in active list | N/A | N/A |

---

## 2. Session Status Transition Diagram

### 2.1 State Machine

```
                    ┌──────────┐
        ┌──────────>│ Created  │<──── User clicks "New Chat"
        │           └────┬─────┘
        │                │ User sends first message
        │                ▼
        │           ┌──────────┐
        │      ┌───>│ Active   │<──────────────────────────────────────┐
        │      │    └────┬─────┘                                       │
        │      │         │ Agent starts streaming                      │
        │      │         ▼                                             │
        │      │    ┌───────────┐  User switches away   ┌────────────┐│
        │      │    │ Streaming ├──────────────────────>│ Background ││
        │      │    └─────┬─────┘                       └──────┬─────┘│
        │      │          │                                    │       │
        │      │          │ Agent finishes                     │ Agent │
        │      │          │                                    │ finishes
        │      │          ▼                                    ▼       │
        │      │    ┌───────────┐                       ┌───────────┐ │
        │      │    │ Completed │<──────────────────────│ Completed │ │
        │      │    └─────┬─────┘  User switches back   └───────────┘ │
        │      │          │        (already completed)                 │
        │      │          │                                            │
        │      │          │ User sends follow-up message               │
        │      │          └────────────────────────────────────────────┘
        │      │
        │      │    ┌───────────┐
        │      └────│   Error   │  Stream error at any streaming state
        │           └─────┬─────┘
        │                 │ User retries
        │                 ▼
        │           (back to Active → Streaming)
        │
        │           ┌───────────┐
        └───────────│ Archived  │  User deletes session (from any state)
                    └───────────┘
```

### 2.2 Transition Rules

| From | Trigger | To | Action |
|------|---------|-----|--------|
| — | User clicks "New Chat" | `Created` | `CreateSessionAction` — generates UUID, sets timestamps |
| `Created` | User sends first message | `Active` | `SetSessionStatusAction(Active)`, `AddMessageAction`, start streaming |
| `Active` | Agent begins streaming response | `Streaming` | `SetRunningAction(true)`, `SetSessionStatusAction(Streaming)` |
| `Streaming` | Agent finishes response | `Completed` | `SetRunningAction(false)`, `SetSessionStatusAction(Completed)` |
| `Streaming` | User switches to another session | `Background` | `SetSessionStatusAction(Background)` — streaming continues |
| `Streaming` | Stream error (network, server, LLM) | `Error` | `SetRunningAction(false)`, `SetSessionStatusAction(Error)`, store error details |
| `Streaming` | User clicks "Stop Generation" | `Active` | `CancelSession(sessionId)`, `SetRunningAction(false)`, `SetSessionStatusAction(Active)` |
| `Background` | Agent finishes response | `Completed` | `SetRunningAction(false)`, `SetSessionStatusAction(Completed)`, `IncrementUnreadAction` |
| `Background` | Stream error | `Error` | `SetRunningAction(false)`, `SetSessionStatusAction(Error)`, trigger notification |
| `Background` | User switches back to this session | `Streaming` | `SetSessionStatusAction(Streaming)`, `ClearUnreadAction` — streaming is still in progress |
| `Completed` | User sends follow-up message | `Active` | `AddMessageAction`, start new streaming → transitions to `Streaming` |
| `Completed` | User switches away | `Completed` | No status change — completed sessions don't become Background |
| `Error` | User clicks "Retry" | `Active` | Clear error state, re-send last message → transitions to `Streaming` |
| Any | User deletes session | `Archived` | Confirmation dialog → `DestroySessionAction` → resource cleanup |
| Any | Circuit disconnect | — | **All sessions destroyed** — in-memory state lost (Q-STATE-005) |

### 2.3 Key Invariants

1. **Only one `Active`/`Streaming` foreground session**: The session pointed to by `ActiveSessionId` has status `Active`, `Streaming`, or `Created`. All other sessions are `Background`, `Completed`, `Error`, or `Archived`.
2. **`Background` only from `Streaming`**: A session can only enter `Background` if it was actively streaming when the user switched away. Idle sessions (`Active`, `Completed`) stay in their current status when the user navigates away.
3. **`Background` → `Completed` is automatic**: When a background stream finishes, the reducer automatically transitions to `Completed` without user interaction.
4. **`Error` is recoverable**: The user can retry from `Error`, returning to `Active` → `Streaming`.

---

## 3. Session Creation

### 3.1 Creation Triggers

| Trigger | Context | Behavior |
|---------|---------|----------|
| **"New Chat" button** | Sidebar header or empty state | Create session, set as active, show empty chat area |
| **App cold start** | First page load / circuit connect | Auto-create one default session so the user never sees an empty app |
| **Keyboard shortcut** | `Ctrl+Shift+N` (future) | Same as "New Chat" button |

> **Ref:** Q-UX-003 — "Session creation: 'New Chat' button — deferred but design needed"

### 3.2 Default Values

When `CreateSessionAction` is dispatched, the reducer initializes the session with:

```csharp
var metadata = new SessionMetadata
{
    Id = Guid.NewGuid().ToString(),    // UUID v4
    Title = "New Chat",                 // Default placeholder title
    Status = SessionStatus.Created,
    CreatedAt = DateTimeOffset.UtcNow,
    LastActivityAt = DateTimeOffset.UtcNow,
    UnreadCount = 0,
    HasPendingApproval = false
};

var state = new SessionState();         // All fields at default (empty messages, no plan, no artifacts)
```

### 3.3 Creation Flow (Sequence)

```
1. User clicks "New Chat" (or app cold start)
2. Component generates: sessionId = Guid.NewGuid().ToString()
3. Dispatch: CreateSessionAction(sessionId, DateTimeOffset.UtcNow)
4. Reducer:
   a. Creates new SessionEntry with metadata + empty SessionState
   b. Inserts at position 0 in SessionOrder (most recent first)
   c. Sets ActiveSessionId = sessionId
   d. If previous active session was Streaming → transition to Background
5. UI re-renders:
   - Sidebar shows new session at top with "New Chat" title
   - Chat area shows empty state ("Start a conversation")
   - Canvas pane hides or shows default state
6. AgentStreamingService: GetOrCreateContext(sessionId) — lazy, created on first message
```

### 3.4 Cold Start Behavior

On initial circuit connection, the app creates a single default session automatically:

```csharp
// In Chat.razor OnInitializedAsync or a Fluxor Effect
if (SessionManagerState.Value.Sessions.IsEmpty)
{
    var sessionId = Guid.NewGuid().ToString();
    Dispatcher.Dispatch(new CreateSessionAction(sessionId, DateTimeOffset.UtcNow));
}
```

This ensures the user always lands on a ready-to-use chat interface, matching the behavior of ChatGPT, Claude, and Copilot web clients.

---

## 4. Title Generation

### 4.1 Strategy: Progressive Title Refinement

Session titles evolve through a predictable progression:

| Phase | Title Source | Timing | Format |
|-------|------------|--------|--------|
| **Phase 1: Default** | Hardcoded | On creation | `"New Chat"` |
| **Phase 2: First-message truncation** | User's first message | On first `AddMessageAction` | First 50 characters of user message, ellipsis if truncated |
| **Future: LLM-generated** | Agent summarization | After first agent response | 3-5 word summary of conversation topic |

### 4.2 MVP: First-Message Truncation

For MVP, title generation is deterministic and synchronous — no LLM call required:

```csharp
// In a Fluxor Effect triggered by the first user message in a session
public class SessionTitleEffect : Effect<SessionActions.AddMessageAction>
{
    private readonly IState<SessionManagerState> _state;

    public override Task HandleAsync(SessionActions.AddMessageAction action, IDispatcher dispatcher)
    {
        var entry = _state.Value.Sessions.GetValueOrDefault(action.SessionId);
        if (entry is null) return Task.CompletedTask;

        // Only auto-generate title for sessions still titled "New Chat"
        // AND only from user messages (not assistant responses)
        if (entry.Metadata.Title == "New Chat"
            && action.Message.Role == ChatRole.User
            && action.Message.Text is { Length: > 0 } text)
        {
            string title = text.Length <= 50
                ? text
                : string.Concat(text.AsSpan(0, 47), "...");

            // Normalize: collapse whitespace, remove newlines
            title = title.ReplaceLineEndings(" ").Trim();

            dispatcher.Dispatch(
                new SessionActions.UpdateSessionTitleAction(action.SessionId, title));
        }

        return Task.CompletedTask;
    }
}
```

### 4.3 Future: LLM-Generated Titles

After MVP, titles can be improved with an LLM summarization call:

1. After the first agent response completes (status transitions from `Streaming` to `Completed`)
2. Fire a background task that sends the first 2-3 messages to a lightweight model
3. System prompt: `"Generate a 3-5 word title summarizing this conversation. Return only the title, no quotes."`
4. Dispatch `UpdateSessionTitleAction` with the result
5. If the LLM call fails (timeout, rate limit), keep the truncated title

This is a **non-blocking enhancement** — the session is fully functional with the truncated title. The LLM title replaces it asynchronously when available.

### 4.4 Manual Rename (Future)

Users may want to rename sessions. This is a future feature:
- Double-click the title in the sidebar to enter edit mode
- `Enter` to confirm, `Escape` to cancel
- Dispatch `UpdateSessionTitleAction` with user-provided title
- Manual titles are never overwritten by auto-generation

---

## 5. Session Activation and Switching

### 5.1 What "Active" Means

At any point, exactly one session is active (the foreground session):
- Its messages are rendered in the chat area
- Its artifacts are rendered in the canvas pane
- Its streaming state drives the typing indicator and stop button
- It is the target for new user messages

The active session is identified by `SessionManagerState.ActiveSessionId`.

### 5.2 Switching Protocol

When the user clicks a different session in the sidebar:

```
┌─────────────────────────────────────────────────────────────────────┐
│ Switch from Session A (current active) to Session B (target)        │
│                                                                      │
│ 1. Dispatch: SetActiveSessionAction("session-b")                    │
│                                                                      │
│ 2. Reducer logic:                                                    │
│    a. Examine Session A's current status:                            │
│       • If Streaming → transition A to Background                   │
│       • If Active/Completed/Error → NO status change (stays as-is) │
│    b. Move Session B to foreground:                                  │
│       • If B was Background → transition to Streaming               │
│         (stream is still running, user now sees it live)            │
│       • If B was Completed → transition to Active                   │
│       • If B was Created → stays Created                            │
│       • If B was Error → stays Error                                │
│    c. Clear B's unread count: UnreadCount = 0                       │
│    d. Set ActiveSessionId = "session-b"                             │
│                                                                      │
│ 3. Single state change notification → ALL components re-render      │
│    with Session B's data (atomic swap per §7 of 02-multi-session)  │
└─────────────────────────────────────────────────────────────────────┘
```

> **Ref:** 02-multi-session-state.md §7 — "Session switching is inherently atomic. A single `SetActiveSessionAction` produces a single new `SessionManagerState`."

### 5.3 What Happens to the Previous Session

| Previous Session Status | Behavior on Switch Away |
|-------------------------|-------------------------|
| `Created` | Stays `Created` — no messages, no streaming |
| `Active` | Stays `Active` — idle, no status change needed |
| `Streaming` | **Transitions to `Background`** — streaming continues; unread increments |
| `Completed` | Stays `Completed` |
| `Error` | Stays `Error` |

The key insight: **only `Streaming` sessions change status when the user switches away**. This is because only streaming sessions have active background work that produces new content the user hasn't seen.

### 5.4 What Happens to the Target Session

| Target Session Status | Behavior on Switch To |
|-----------------------|----------------------|
| `Created` | Stays `Created`, clear unread |
| `Active` | Stays `Active`, clear unread |
| `Background` | **Transitions to `Streaming`** — user now sees the live stream, clear unread |
| `Completed` | Transitions to `Active` (ready for follow-up), clear unread |
| `Error` | Stays `Error`, clear unread — user can retry |

### 5.5 Switching Invariant Proof

Because session switching is a single `SetActiveSessionAction` dispatch that produces a single new `SessionManagerState` (see 02-multi-session-state.md §7.2–§7.3), there is **zero risk** of:
- Showing Session A's messages with Session B's plan
- Briefly rendering stale data during the swap
- Component re-renders interleaving two sessions' data

All selectors derive from `Sessions[ActiveSessionId]` in the new state snapshot. Fluxor's single-dispatch model guarantees atomicity.

---

## 6. Background Session Behavior

### 6.1 Design Principle: Full State Continuity

Background sessions are **not** suspended, paused, or "lazy-loaded". They maintain full state continuity:

> **Ref:** Q-SYNC-003 — "Background sessions maintain full state — no lazy hydration"

| Aspect | Background Behavior |
|--------|-------------------|
| SSE stream | **Continues running** on a background `Task` via `AgentStreamingService.ProcessAgentResponseAsync()` |
| Fluxor state | **Fully updated** — each streaming token dispatches `AddMessageAction`, `SetRecipeAction`, etc. with the session's ID |
| Deduplication | **Independent** — each session's `SessionStreamingContext` has its own `SeenFunctionCallIds` set |
| Tool results | **Processed** — tool call results are dispatched to the correct session's state |
| Artifacts | **Accumulated** — plans, recipes, documents update in the background session's `SessionState` |
| Unread tracking | **Incremented** — `IncrementUnreadAction` dispatched for each assistant response chunk |

### 6.2 Why Not Lazy Hydration?

Lazy hydration (storing only "has updates" flag and replaying the stream when the user switches back) was considered and rejected:

| Approach | Pros | Cons |
|----------|------|------|
| Full state continuity | Instant switch back; state always consistent; simpler implementation | Higher memory usage per session |
| Lazy hydration | Lower memory during background | Switch-back requires replay; inconsistent state during replay; complex bookkeeping of "where we left off"; session metadata (unread count, pending approval) still needs real-time updates |

Since the AG-UI protocol is stateless (each request sends full history), there is no "replay" mechanism — the stream is consumed once and dispatched to Fluxor. Lazy hydration would require buffering the raw SSE events, which adds complexity without meaningful memory savings (the Fluxor state is already the most compact representation).

### 6.3 Sidebar Updates from Background Sessions

When a background session receives updates, the sidebar re-renders to show:
- **Unread count badge**: Numeric badge on the session item (e.g., "3")
- **Streaming indicator**: Animated dot showing the session is still receiving data
- **HITL approval badge**: Urgent indicator if a background session needs user approval (see §10)
- **Title update**: If the session's title was auto-generated during background streaming, the sidebar reflects it

These updates happen because background dispatches (`IncrementUnreadAction`, `SetHasPendingApprovalAction`) modify the `SessionManagerState`, and the sidebar component subscribes to `IState<SessionManagerState>`.

---

## 7. Concurrent Stream Cap and Queueing

### 7.1 Resource Constraint

Each active SSE stream maintains an open HTTP connection from the Blazor circuit to the AGUIDojoServer. While HTTP/2 multiplexing eliminates the HTTP/1.1 6-connection limit, each stream still consumes:
- A server-side Kestrel connection
- An `HttpClient` POST request in the Blazor circuit
- Agent processing resources (LLM API calls, tool execution)
- Fluxor dispatch overhead for each streaming token

> **Ref:** R13 — "Practical limit: 3-5 concurrent active streams recommended."  
> **Ref:** Q-SYNC-004 — "Concurrent streams capped at 3-5; queue beyond cap."

### 7.2 Streaming Cap: 5 Concurrent Streams

The `AgentStreamingService` enforces a maximum of **5** concurrent active SSE streams per Blazor circuit:

```csharp
private const int MaxConcurrentStreams = 5;

public async Task ProcessAgentResponseAsync(string sessionId, IChatClient chatClient)
{
    int activeStreams = _contexts.Values.Count(c =>
        c.ResponseCancellation is { IsCancellationRequested: false });

    if (activeStreams >= MaxConcurrentStreams)
    {
        // Enqueue the request and return — the queue processor will start
        // this session's stream when a slot becomes available
        EnqueueStreamRequest(sessionId, chatClient);
        return;
    }

    // Proceed with streaming...
}
```

### 7.3 Queueing Strategy

When the cap is reached, new stream requests are **queued** rather than rejected:

```
Queue: FIFO order → [session-f, session-g, session-h]
Active: [session-a (streaming), session-b (streaming), session-c (streaming),
         session-d (streaming), session-e (streaming)]

When session-a completes → dequeue session-f → start streaming session-f
```

**Queue behavior:**
- Queued sessions have status `Active` (not `Streaming`) — the user can still see their messages and type
- When queued, a brief toast notifies: "Agent response queued — other sessions are still processing"
- The user's message is already dispatched to the session's state; only the SSE stream start is deferred
- Queue processing is automatic — no user intervention needed
- If the user sends a new message to a queued session, the queue entry is updated with the latest message history (no duplicate streams)

### 7.4 Priority: Active Session Gets Precedence

If the user is actively viewing a session and sends a message while the cap is reached, that session's request **preempts** the oldest background stream:

1. User sends message in foreground session
2. Cap is full (5 streams active, all background)
3. Cancel the oldest background stream (FIFO — the one that has been running longest)
4. Start the foreground session's stream immediately

This ensures the user never waits for a response in the session they're actively viewing. Background sessions can be retried.

---

## 8. Session Resume

### 8.1 What is Session Resume?

"Resume" is when a user returns to a `Completed` session and sends a follow-up message, essentially continuing a previously finished conversation. This is distinct from:
- **Switching to a `Background` session** — the stream is still running
- **Retrying from `Error`** — re-sends the failed request

### 8.2 Resume Flow

```
1. User is viewing Session A (status: Completed)
   — Messages show the previous conversation
   — Chat input is enabled (idle state)
   — Canvas shows Session A's last artifacts (plan, recipe, document, etc.)

2. User types follow-up message and presses Enter

3. Flow:
   a. Dispatch: AddMessageAction(sessionA.Id, userMessage)
   b. Dispatch: SetSessionStatusAction(sessionA.Id, SessionStatus.Active)
   c. Update LastActivityAt timestamp
   d. Start streaming: ProcessAgentResponseAsync(sessionA.Id, chatClient)
      — The AG-UI request sends ALL messages (previous + new) to the server
      — The server's stateless agent sees the full conversation context
   e. Dispatch: SetSessionStatusAction(sessionA.Id, SessionStatus.Streaming)

4. Agent responds with awareness of the full conversation history:
   — Can reference previous tool results, plans, recipes
   — Can build on earlier plans or modify existing artifacts
   — The Canvas pane updates if new artifacts are produced

5. When streaming completes:
   — Dispatch: SetSessionStatusAction(sessionA.Id, SessionStatus.Completed)
   — Session is ready for another follow-up
```

### 8.3 State Continuity on Resume

When a session resumes, **all prior state is preserved**:

| State | Preserved? | Detail |
|-------|-----------|--------|
| Messages | ✅ Yes | Full `ImmutableList<ChatMessage>` from `SessionState.Messages` |
| Plan | ✅ Yes | `SessionState.Plan` retains the last plan state |
| Recipe | ✅ Yes | `SessionState.CurrentRecipe` retains the last recipe |
| Document | ✅ Yes | `SessionState.CurrentDocumentState` retains last document |
| Artifacts/Tabs | ✅ Yes | `VisibleTabs`, `ActiveArtifactType` retained |
| Deduplication | ✅ Yes | `SessionStreamingContext.SeenFunctionCallIds` retained — prevents re-rendering completed tool calls |
| ConversationId | ✅ Yes | Retained in `SessionStreamingContext.ChatOptions.ConversationId` |
| StatefulMessageCount | ✅ Yes | Retained — server knows which messages it already processed |

### 8.4 Why Resume Works with Stateless AG-UI

The AG-UI protocol sends the **full message history** with every request. The server agent is stateless (see 01-unified-endpoint.md §8.2). This means:
- No server-side session store is needed for resume
- The agent sees the complete conversation context on every follow-up
- Wrapper state (plan, recipe, document) can be reconstructed from the conversation history + `ag_ui_state` sent by the client

---

## 9. Session Cleanup

### 9.1 Explicit Delete (User-Initiated)

When the user explicitly deletes a session:

```
1. User clicks delete button on session item in sidebar
   (or swipes left on mobile — future)

2. Confirmation dialog:
   — "Delete this conversation? This action cannot be undone."
   — [Cancel] [Delete]
   — Uses BB DialogService.Confirm() (BB v3)

3. If confirmed:
   a. If session is Streaming or Background:
      — Cancel active stream: AgentStreamingService.CancelSession(sessionId)
      — Cancel any pending HITL approval: ApprovalTaskSource.TrySetCanceled()
   b. Dispatch: DestroySessionAction(sessionId)
   c. Reducer:
      — Remove session from Sessions dictionary
      — Remove sessionId from SessionOrder
      — If deleted session was ActiveSessionId:
        → Set ActiveSessionId to the next session in SessionOrder
        → If no sessions remain, create a new default session
   d. AgentStreamingService.DestroySession(sessionId)
      — Disposes SessionStreamingContext (cancels CTS, cleans dedup sets)
      — Removes from ConcurrentDictionary
   e. Queue processor: if there are queued sessions, dequeue one (slot freed)
```

> **Ref:** Q-UX-005 — "Session deletion: confirmation + state cleanup — deferred but design needed"

### 9.2 Circuit Disconnect (Involuntary Cleanup)

When the Blazor Server SignalR circuit disconnects (browser close, tab close, navigation away, network loss):

```
1. Blazor runtime detects circuit disconnect
2. All scoped services (per-circuit) are disposed:
   — AgentStreamingService.Dispose() is called
   — All SessionStreamingContext instances are disposed
   — All CancellationTokenSources are canceled → SSE streams abort
3. Fluxor store is garbage collected (in-memory, no persistence)
4. ALL session state is LOST
```

> **Ref:** Q-STATE-005 — "In-memory only for MVP — circuit disconnect destroys all sessions"

This is **by design for MVP**. The Blazor Server circuit is the session boundary. Future persistence (§11) will address cross-circuit state recovery.

### 9.3 Cleanup Cascade

When a session is destroyed (either explicitly or via circuit disconnect), the following resources are cleaned up in order:

| Step | Resource | Cleanup Action |
|------|----------|---------------|
| 1 | Active SSE stream | `CancellationTokenSource.Cancel()` — aborts the HTTP POST |
| 2 | Pending HITL approval | `ApprovalTaskSource.TrySetCanceled()` — unblocks waiting agent |
| 3 | SessionStreamingContext | `Dispose()` — frees all transient streaming state |
| 4 | Fluxor SessionEntry | `Sessions.Remove(sessionId)` — frees messages, plan, artifacts |
| 5 | SessionOrder entry | `SessionOrder.Remove(sessionId)` — removes from sidebar |
| 6 | Queue entry (if queued) | Remove from stream queue — session will never start streaming |

### 9.4 Deleting a Session with Pending HITL Approval

If a session has a pending `FunctionApprovalRequestContent` (user hasn't approved/rejected yet):

1. Delete confirmation dialog shows an **additional warning**: "This session has a pending approval request. Deleting will cancel the approval."
2. On confirm:
   - `ApprovalTaskSource.TrySetCanceled()` — the server-side agent's `await` unblocks with cancellation
   - The `ServerFunctionApprovalAgent` wrapper handles the cancellation by emitting a `RUN_ERROR` event to the SSE stream
   - Since the SSE stream is also being canceled (step 1 of cleanup cascade), this error is swallowed
3. The session is removed normally

---

## 10. HITL Approval in Background Sessions

### 10.1 The Problem

When a background session's agent invokes an approval-required tool (e.g., `send_email`), the `ServerFunctionApprovalAgent` wrapper blocks on `ApprovalTaskSource` waiting for user response. But the user is viewing a different session.

> **Ref:** Q-NOTIF-004 — "For HITL approvals in background sessions, should the notification be clickable to switch to that session?"  
> **Ref:** ISS-020 — "HITL in background sessions — what happens when an approval is needed in a session the user isn't viewing?"

### 10.2 Design: Notification-Driven Approval

```
1. Background session "sess-b" reaches HITL approval point
2. AgentStreamingService detects FunctionApprovalRequestContent for sess-b:
   a. Dispatch: SetPendingApprovalAction(sess-b, approval details)
   b. Dispatch: SetHasPendingApprovalAction(sess-b, true)  (metadata flag for sidebar)
3. Sidebar shows urgent notification badge on sess-b:
   — Amber/orange pulsing icon (distinct from unread count)
   — Tooltip: "Approval needed: Send email to user@example.com"
4. Push notification fires (in-app toast):
   — "Session 'Help me draft email' needs your approval"
   — Toast is CLICKABLE → switches to sess-b
5. User clicks toast or clicks sess-b in sidebar:
   a. Dispatch: SetActiveSessionAction("sess-b")
   b. UI shows sess-b with the approval dialog visible
   c. User approves or rejects
   d. Dispatch: ResolveApproval(sess-b, approved/rejected)
   e. AgentStreamingService completes the ApprovalTaskSource
   f. Streaming resumes for sess-b (now in foreground as Streaming)
```

### 10.3 Timeout Behavior

If the user doesn't respond to a HITL approval within a configurable timeout:

| Strategy | Behavior | Default |
|----------|----------|---------|
| **No timeout (MVP)** | The approval blocks indefinitely. The SSE connection stays open. The user sees the notification badge until they acknowledge it. | ✅ Default for MVP |
| **Configurable timeout (future)** | After N minutes, auto-reject the approval. Dispatch `ResolveApproval(sessionId, false)`. Log rejection reason. Notify user: "Approval for [tool] in [session] timed out and was automatically rejected." | Optional enhancement |

MVP uses no timeout because:
- SSE connections are long-lived in Blazor Server (backed by Kestrel)
- The user is expected to respond eventually (the badge persists)
- Auto-rejection could cause data loss (e.g., sending a half-written email)

---

## 11. Future: Session Persistence

### 11.1 Current MVP: In-Memory Only

For MVP, all session state resides in:
- **Fluxor store** (`SessionManagerState`) — scoped to the Blazor Server circuit
- **AgentStreamingService** (transient streaming state) — scoped to the Blazor Server circuit

Both are lost on circuit disconnect. This matches the current single-session behavior (Q-STATE-005).

> **Ref:** Q-HIST-001 — "Chat history in-memory for MVP — server-side store for future"

### 11.2 Future Persistence Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Browser Tab (Blazor Server Circuit)                              │
│                                                                  │
│  Fluxor Store ←──── hydrate on connect                          │
│       │                                                         │
│       └──── persist on each state change ──→ Session Store API  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────┐
│ Server-Side Session Store                                        │
│                                                                  │
│  ┌─────────┐  ┌──────────────┐  ┌────────────────────────────┐ │
│  │ Redis    │  │ SQL / Cosmos │  │ In-Memory (dev/testing)    │ │
│  │ (fast)   │  │ (durable)    │  │                            │ │
│  └─────────┘  └──────────────┘  └────────────────────────────┘ │
│                                                                  │
│  API:                                                            │
│  - GET  /api/sessions                  → list metadata           │
│  - GET  /api/sessions/{id}             → full session state      │
│  - PUT  /api/sessions/{id}             → upsert session state    │
│  - DELETE /api/sessions/{id}           → delete                  │
│  - PATCH  /api/sessions/{id}/metadata  → update title/status     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 11.3 Persistence Triggers

| Event | Persistence Action | Priority |
|-------|-------------------|----------|
| Session created | Persist metadata only | Low-latency |
| Title updated | Persist metadata | Low-latency |
| Streaming chunk received | **Do not persist** — too frequent | N/A |
| Streaming completed | Persist full session state (messages + artifacts) | Batch |
| Session deleted | Delete from store | Immediate |
| Circuit disconnect | Final persist of all dirty sessions | Best-effort |

### 11.4 Hydration on Reconnect

When a user reconnects (new circuit):
1. Fetch session list metadata from store → populate sidebar
2. Set `ActiveSessionId` to the last active session
3. Lazy-load full session state for the active session only
4. Other sessions' full state loaded on-demand when the user switches to them

This is the "lazy hydration" approach that was rejected for in-memory background streaming (§6.2) but is appropriate for cross-circuit persistence because the alternative (loading all sessions fully on reconnect) scales poorly with many sessions.

---

## 12. Status Transition Reference Table

Complete reference of all valid status transitions:

| # | From | To | Trigger | Reducer Action | Side Effects |
|---|------|----|---------|---------------|--------------|
| T1 | — | `Created` | "New Chat" click / cold start | `CreateSessionAction` | Session added to dictionary and order list |
| T2 | `Created` | `Active` | User sends first message | `SetSessionStatusAction` | Title auto-generated from message |
| T3 | `Active` | `Streaming` | `ProcessAgentResponseAsync` starts | `SetSessionStatusAction`, `SetRunningAction(true)` | SSE stream opens |
| T4 | `Streaming` | `Completed` | Agent stream finishes | `SetRunningAction(false)`, `SetSessionStatusAction` | SSE stream closes |
| T5 | `Streaming` | `Background` | User switches to another session | `SetSessionStatusAction` | Streaming continues on background task |
| T6 | `Streaming` | `Error` | Stream error (network/server/LLM) | `SetRunningAction(false)`, `SetSessionStatusAction` | Error details stored in `SessionState` |
| T7 | `Streaming` | `Active` | User clicks "Stop Generation" | `CancelSession()`, `SetRunningAction(false)`, `SetSessionStatusAction` | SSE stream canceled |
| T8 | `Background` | `Completed` | Background stream finishes | `SetRunningAction(false)`, `SetSessionStatusAction` | `IncrementUnreadAction`, notification toast |
| T9 | `Background` | `Error` | Background stream error | `SetRunningAction(false)`, `SetSessionStatusAction` | Error notification toast |
| T10 | `Background` | `Streaming` | User switches back to this session | `SetSessionStatusAction`, `ClearUnreadAction` | User now sees live stream |
| T11 | `Completed` | `Active` | User sends follow-up message | `AddMessageAction`, `SetSessionStatusAction` | Session resume (§8) |
| T12 | `Completed` | `Active` | User switches to completed session | `SetSessionStatusAction`, `ClearUnreadAction` | Session ready for follow-up |
| T13 | `Error` | `Active` | User clicks "Retry" | `SetSessionStatusAction`, clear error state | Leads to T3 (new streaming) |
| T14 | Any | `Archived` | User confirms delete | `DestroySessionAction` | Full cleanup cascade (§9.3) |

### 12.1 Invalid Transitions

The following transitions are **never valid** and indicate a bug if observed:

| From | To | Why Invalid |
|------|----|------------|
| `Created` | `Background` | Cannot stream without a message |
| `Created` | `Streaming` | Must transition through `Active` first |
| `Completed` | `Background` | No stream to run in background |
| `Error` | `Background` | Must retry first (go through Active → Streaming) |
| `Archived` | Any | Archived sessions are removed from state; no transition possible |
| `Background` | `Active` | Background must transition to `Streaming` (still running) or `Completed` (finished) |

---

## 13. Interaction with Multi-Session State

### 13.1 Lifecycle Actions Map to State Mutations

Each lifecycle transition maps to specific Fluxor actions defined in [02-multi-session-state.md §6.1](02-multi-session-state.md#61-action-design-all-actions-carry-sessionid):

| Lifecycle Event | Fluxor Actions Dispatched |
|----------------|--------------------------|
| Session created | `CreateSessionAction(sessionId, timestamp)` |
| Title generated | `UpdateSessionTitleAction(sessionId, title)` |
| Session activated | `SetActiveSessionAction(sessionId)` |
| Streaming started | `SetRunningAction(sessionId, true)`, `SetSessionStatusAction(sessionId, Streaming)` |
| Streaming completed | `SetRunningAction(sessionId, false)`, `SetSessionStatusAction(sessionId, Completed)` |
| Session backgrounded | `SetSessionStatusAction(sessionId, Background)` |
| Background unread | `IncrementUnreadAction(sessionId)` |
| HITL approval needed | `SetPendingApprovalAction(sessionId, approval)`, `SetHasPendingApprovalAction(sessionId, true)` |
| Session deleted | `DestroySessionAction(sessionId)` |
| Error occurred | `SetRunningAction(sessionId, false)`, `SetSessionStatusAction(sessionId, Error)` |

### 13.2 SessionStreamingContext Lifecycle

The `SessionStreamingContext` (from 02-multi-session-state.md §8) has a lifecycle that parallels `SessionMetadata.Status`:

| Session Status | SessionStreamingContext State |
|---------------|------------------------------|
| `Created` | **Not yet created** — lazily created on first `ProcessAgentResponseAsync` call |
| `Active` | Created but idle (no active CTS) |
| `Streaming` | Active CTS, streaming message accumulating |
| `Background` | Active CTS (same as Streaming — the context doesn't know about foreground/background) |
| `Completed` | Idle (CTS completed or null), deduplication sets retained for resume |
| `Error` | Idle (CTS canceled), deduplication sets retained for retry |
| `Archived` | **Disposed** — `DestroySession()` removes and disposes the context |

### 13.3 Unified Endpoint Integration

All sessions share the same unified `/chat` endpoint (from 01-unified-endpoint.md §2.1). The session lifecycle is purely a **client-side concern**:

- The server has no awareness of session lifecycle status
- The AG-UI protocol's `threadId` field carries the session ID, enabling server-side logging and tracing per session
- The server agent is stateless and singleton — it doesn't care whether the client considers a session "Active", "Background", or "Completed"
- Multiple concurrent sessions result in multiple concurrent POST requests to the same `/chat` endpoint, each with different `threadId` values

This clean separation means the lifecycle design in this document can evolve independently of the server-side architecture.
