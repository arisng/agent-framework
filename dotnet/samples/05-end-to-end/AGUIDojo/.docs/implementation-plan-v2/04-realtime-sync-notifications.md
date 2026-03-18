# Design Section 04: Real-Time Sync & Push Notifications

> **Spec Section:** Client-Side Real-Time Synchronization & Notification Architecture  
> **Created:** 2026-02-24  
> **Status:** Design  
> **References:** Q-SYNC-001, Q-SYNC-002, Q-SYNC-003, Q-SYNC-004, Q-NOTIF-001, Q-NOTIF-002, Q-NOTIF-004, R10, R13, ISS-006, ISS-016, ISS-020, ISS-042  
> **Depends On:** 01-unified-endpoint.md, 02-multi-session-state.md, 03-session-lifecycle.md  
> **Inherited By:** task-6, task-9

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [Real-Time Sync Architecture](#2-real-time-sync-architecture)
3. [Background SSE Stream Data Flow](#3-background-sse-stream-data-flow)
4. [Notification Data Model](#4-notification-data-model)
5. [MVP Notification Types](#5-mvp-notification-types)
6. [Notification Delivery Pipeline](#6-notification-delivery-pipeline)
7. [BB v3 Toast Integration](#7-bb-v3-toast-integration)
8. [HITL Approval Notification Flow](#8-hitl-approval-notification-flow)
9. [Concurrent Stream Management](#9-concurrent-stream-management)
10. [Sequence Diagram: Background Session Completion](#10-sequence-diagram-background-session-completion)
11. [Future: Dedicated SignalR Hub & Browser Notifications](#11-future-dedicated-signalr-hub--browser-notifications)
12. [Design Decisions Summary](#12-design-decisions-summary)

---

## 1. Problem Statement

The existing spec (ISS-042) has **zero push notification architecture**. The only real-time mechanism is SSE for token streaming within the active session. When a user has multiple sessions running concurrently, they need:

1. **Real-time state sync** — background sessions' Fluxor state must stay current even when the user is viewing a different session
2. **Completion notifications** — know when a background agent finishes its response
3. **HITL approval alerts** — urgently learn when a background session is blocked waiting for approval
4. **Error notifications** — learn when a background session stream fails

> **Ref:** ISS-042 — "No push notification architecture. The spec's only real-time mechanism is SSE for agent token streaming."  
> **Ref:** Q-SYNC-001 — "When a user is viewing Session A and Session B completes in the background, how does the client know?"

### 1.1 Key Insight: Blazor Server Already Has SignalR

Blazor Server maintains a permanent SignalR connection per circuit for DOM diffing. This connection is **always active** as long as the user is on the page. The core realization is that we do not need new infrastructure for MVP push notifications:

- Background `Task.Run()` threads update Fluxor state via `IDispatcher.Dispatch()`
- Fluxor dispatches trigger component re-renders via `IState<T>.StateChanged`
- Blazor's existing SignalR circuit pushes DOM diffs to the browser

The push notification "infrastructure" is simply: **dispatch a Fluxor action from a background thread → component re-renders → browser receives the update via the existing circuit**.

> **Ref:** R10 — "Blazor Server callbacks (InvokeAsync) for MVP. The background SSE stream runs on a background task. When it completes, it updates the session state and triggers InvokeAsync(StateHasChanged) on the session list component."  
> **Ref:** Q-SYNC-002 — "Blazor Server already maintains a SignalR circuit for DOM diffing — piggyback on it for MVP."

---

## 2. Real-Time Sync Architecture

### 2.1 The Pipeline: Background SSE → Fluxor → InvokeAsync → DOM Diff

For MVP, real-time synchronization between background sessions and the UI uses a **four-stage pipeline** that requires zero new infrastructure:

```
Stage 1: Background SSE Stream (per session)
  └─ AgentStreamingService.ProcessAgentResponseAsync(sessionId, chatClient)
     runs on Task.Run() — independent of UI thread

Stage 2: Fluxor Dispatch (thread-safe)
  └─ _dispatcher.Dispatch(new AddMessageAction(sessionId, msg))
     _dispatcher.Dispatch(new IncrementUnreadAction(sessionId))
     Fluxor's IDispatcher is thread-safe — safe to call from background tasks

Stage 3: State Change Notification
  └─ IState<SessionManagerState>.StateChanged fires
     All subscribed components receive the new state snapshot

Stage 4: Blazor DOM Diff (via existing SignalR circuit)
  └─ Components call StateHasChanged() / InvokeAsync(StateHasChanged)
     Blazor diffs the render tree and pushes changes via SignalR
     Browser updates: sidebar badges, toast notifications, etc.
```

### 2.2 Why This Works Without a Dedicated Hub

| Concern | Dedicated SignalR Hub | Blazor Circuit Piggyback (MVP) |
|---------|----------------------|-------------------------------|
| Transport | Separate SignalR connection | Reuses existing Blazor circuit connection |
| Infrastructure | New hub class, DI registration, JS client code | Zero new infrastructure |
| Push mechanism | Hub sends message → JS client handles | Fluxor dispatch → component re-render → DOM diff |
| Cross-tab support | ✅ Multiple tabs share hub | ❌ Each tab has its own circuit |
| Security | Needs auth configuration | Inherits Blazor circuit auth |
| Complexity | New protocol, serialization, routing | Just dispatch Fluxor actions |

> **Ref:** Q-SYNC-002 — "Piggybacking on the Blazor SignalR circuit is simpler and creates no new infrastructure for MVP."

### 2.3 Background Thread Safety

Fluxor's `IDispatcher` is thread-safe. Dispatching from a background `Task.Run()` thread is safe:

```csharp
// Inside AgentStreamingService — running on a background thread for session "sess-b"
await foreach (var update in chatClient.GetStreamingResponseAsync(messages, ctx.ChatOptions, ct))
{
    // Process update...
    
    // Safe: Fluxor dispatcher is thread-safe
    _dispatcher.Dispatch(new SessionActions.AddMessageAction(sessionId, assistantMsg));
    
    // If this is a background session, increment unread
    if (_sessionStore.Value.ActiveSessionId != sessionId)
    {
        _dispatcher.Dispatch(new SessionActions.IncrementUnreadAction(sessionId));
    }
}
```

Components consuming `IState<SessionManagerState>` must use `InvokeAsync(StateHasChanged)` (not bare `StateHasChanged()`) because the state change originates from a non-UI thread. This is standard Blazor Server practice.

> **Ref:** Q-SYNC-003 — "Full state update for background sessions — no lazy hydration."

---

## 3. Background SSE Stream Data Flow

### 3.1 Full State Continuity

Background sessions maintain **full state continuity** (see 03-session-lifecycle.md §6). Every SSE event dispatches the same Fluxor actions regardless of whether the session is foreground or background. The only difference: background sessions also dispatch `IncrementUnreadAction` and may trigger notification toasts.

```
Background Session "sess-b" SSE Stream:
│
├── TEXT_MESSAGE_CONTENT → AddMessageAction("sess-b", msg)
│                        → UpdateResponseMessageAction("sess-b", streamingMsg)
│                        → IncrementUnreadAction("sess-b")  // background only
│
├── TOOL_CALL_START     → (update streaming context dedup sets)
├── TOOL_CALL_END       → SetRecipeAction / SetPlanAction / etc. (session-scoped)
│
├── STATE_SNAPSHOT      → SetRecipeAction / SetPlanAction / SetDocumentAction
│                        → IncrementUnreadAction("sess-b")
│
├── RUN_FINISHED        → SetRunningAction("sess-b", false)
│                        → SetSessionStatusAction("sess-b", Completed)
│                        → ★ TRIGGER: SessionCompletedNotification
│
└── RUN_ERROR           → SetRunningAction("sess-b", false)
│                        → SetSessionStatusAction("sess-b", Error)
│                        → ★ TRIGGER: SessionErrorNotification
```

### 3.2 Notification Trigger Points

Notifications are **not** fired on every streaming token — that would flood the user with toasts. Instead, notifications trigger only on **significant state transitions** from background sessions:

| SSE Event / State Change | Notification? | Rationale |
|--------------------------|---------------|-----------|
| Text message content token | ❌ No | Too frequent; unread badge suffices |
| Tool call started | ❌ No | Intermediate state; not user-actionable |
| Tool call completed (artifact update) | ❌ No | Artifact badge in sidebar suffices |
| `FunctionApprovalRequestContent` | ✅ **Yes — ApprovalRequired** | Urgent — agent is blocked |
| Stream completed (`RUN_FINISHED`) | ✅ **Yes — SessionCompleted** | Agent done; user may want to review |
| Stream error (`RUN_ERROR`) | ✅ **Yes — SessionError** | Requires user attention (retry?) |

---

## 4. Notification Data Model

### 4.1 SessionNotification Record

```csharp
namespace AGUIDojoClient.Models;

/// <summary>
/// Represents an in-app notification about a session event.
/// Used by the notification toast system and the notification center (future).
/// </summary>
public sealed record SessionNotification
{
    /// <summary>The type of notification event.</summary>
    public required NotificationType Type { get; init; }

    /// <summary>The session that produced this notification.</summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Display title for the notification toast.
    /// Typically the session title (e.g., "Help me draft an email").
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Optional detail message shown below the title.
    /// Example: "Agent finished responding" or "Approval needed: send_email".
    /// </summary>
    public string? Message { get; init; }

    /// <summary>When the notification was created (UTC).</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Whether this notification requires urgent attention.
    /// Urgent notifications use Warning toast variant and persist longer.
    /// </summary>
    public bool IsUrgent { get; init; }

    /// <summary>
    /// The action to perform when the user clicks the notification.
    /// For MVP, this is always "switch to session" — the SessionId
    /// is the target. Future: could navigate to specific content within
    /// the session.
    /// </summary>
    public string? ActionUrl { get; init; }
}
```

### 4.2 NotificationType Enum

```csharp
namespace AGUIDojoClient.Models;

/// <summary>
/// Types of session notifications for the in-app notification system.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// A background session's agent has finished its response.
    /// Non-urgent. Toast variant: Success.
    /// </summary>
    SessionCompleted,

    /// <summary>
    /// A background session requires HITL approval to continue.
    /// URGENT. Toast variant: Warning. Persists until acknowledged.
    /// </summary>
    ApprovalRequired,

    /// <summary>
    /// A background session's streaming encountered an error.
    /// Non-urgent but requires attention. Toast variant: Destructive.
    /// </summary>
    SessionError
}
```

### 4.3 Field Semantics

| Field | SessionCompleted | ApprovalRequired | SessionError |
|-------|-----------------|------------------|--------------|
| `Type` | `SessionCompleted` | `ApprovalRequired` | `SessionError` |
| `SessionId` | The completed session | The blocked session | The errored session |
| `Title` | Session title (e.g., "Draft email") | Session title | Session title |
| `Message` | "Agent finished responding" | "Approval needed: {toolName}" | "Stream error: {errorMessage}" |
| `Timestamp` | When `RUN_FINISHED` received | When `FunctionApprovalRequestContent` received | When `RUN_ERROR` received |
| `IsUrgent` | `false` | `true` | `false` |
| `ActionUrl` | `null` (click switches to session) | `null` (click switches to session + shows approval dialog) | `null` (click switches to session) |

---

## 5. MVP Notification Types

### 5.1 SessionCompleted

**When**: A background session's agent stream finishes (`RUN_FINISHED` event received while session is in `Background` status).

**UX**:
- Toast: **Success** variant — green accent
- Duration: 5 seconds, auto-dismiss
- Title: Session title (e.g., "Draft email")
- Message: "Agent finished responding"
- Click action: Switch to that session (`SetActiveSessionAction`)
- Sidebar: Checkmark badge + unread count

> **Ref:** Q-NOTIF-001 — "MVP notifications: session completed + HITL approval required."

### 5.2 ApprovalRequired

**When**: A background session's agent requests HITL approval (`FunctionApprovalRequestContent` received while session is in `Background` status).

**UX**:
- Toast: **Warning** variant — amber accent, pulsing attention indicator
- Duration: **Persistent** — does not auto-dismiss. Stays until the user clicks or explicitly dismisses
- Title: Session title
- Message: "Approval needed: {toolName} — {toolDescription}"
- Click action: Switch to session + approval dialog is already visible (dispatched by `SetPendingApprovalAction`)
- Sidebar: Amber pulsing icon (distinct from unread badge per 03-session-lifecycle.md §10.2)

**Critical constraint: Only one approval dialog at a time.** When the user clicks the notification and switches to the session, the approval dialog renders because `PendingApproval` is already set in that session's `SessionState`. No other session's approval dialog is visible. If multiple sessions have pending approvals simultaneously, each has its own toast notification in the toast stack — but only the session the user switches to shows its dialog.

> **Ref:** Q-NOTIF-004 — "HITL in background: clickable notification → switch to session → show approval dialog."

### 5.3 SessionError

**When**: A background session's stream encounters an error (`RUN_ERROR` event or exception in `ProcessAgentResponseAsync` while session is in `Background` status).

**UX**:
- Toast: **Destructive** variant — red accent
- Duration: 8 seconds, auto-dismiss (longer than success to ensure visibility)
- Title: Session title
- Message: "Error: {brief error description}" (truncated to 100 chars)
- Click action: Switch to session where error banner with retry option is visible
- Sidebar: Red error badge

### 5.4 Notification Stacking

When multiple background sessions produce notifications simultaneously (e.g., 3 sessions complete within seconds):

- Toasts stack vertically in the toast viewport (BB v3 default: bottom-right)
- Maximum visible toasts: **3** (configurable via `BbToastProvider`)
- Excess toasts queue and appear as earlier ones dismiss
- Each toast is independent — clicking one switches to that session without dismissing others
- Approval notifications (urgent) always render on top of non-urgent toasts

---

## 6. Notification Delivery Pipeline

### 6.1 Architecture: Fluxor Effect → NotificationService → Toast

Notifications are triggered by Fluxor **Effects** that react to specific action patterns. This keeps the notification logic decoupled from the streaming service.

```
AgentStreamingService (background thread)
  │
  ├── Dispatch: SetSessionStatusAction(sessId, Completed)     ─┐
  ├── Dispatch: SetSessionStatusAction(sessId, Error)          ─┤
  └── Dispatch: SetHasPendingApprovalAction(sessId, true)      ─┤
                                                                 │
                                                                 ▼
NotificationEffects (Fluxor Effect)                              
  │                                                              
  ├── Reacts to SetSessionStatusAction                          
  │   IF session is NOT the active session                       
  │   AND new status is Completed or Error                       
  │   THEN: dispatch ShowNotificationAction(...)                 
  │                                                              
  ├── Reacts to SetHasPendingApprovalAction                     
  │   IF session is NOT the active session                       
  │   AND HasPending is true                                     
  │   THEN: dispatch ShowNotificationAction(type: ApprovalRequired)
  │                                                              
  └── Does NOT fire if the session IS active                    
      (user is already seeing the session — no toast needed)     
                                                                 │
                                                                 ▼
NotificationState (Fluxor Feature)                              
  │                                                              
  ├── Holds: ImmutableList<SessionNotification> Notifications   
  └── Reducer: adds notification to list                        
                                                                 │
                                                                 ▼
NotificationToastComponent (subscribed to NotificationState)    
  │                                                              
  └── When new notification added:                               
      → Calls IToastService.Show(notification)                   
      → Toast renders with click handler → SetActiveSessionAction
```

### 6.2 NotificationEffects Implementation Sketch

```csharp
namespace AGUIDojoClient.Store.Notifications;

/// <summary>
/// Fluxor effects that trigger notification toasts when background
/// sessions reach significant state transitions.
/// </summary>
public class NotificationEffects
{
    private readonly IState<SessionManagerState> _sessionStore;

    public NotificationEffects(IState<SessionManagerState> sessionStore)
    {
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Fires when a session's status changes. Produces a notification
    /// only if the session is NOT the active (foreground) session.
    /// </summary>
    [EffectMethod]
    public Task OnSessionStatusChanged(
        SessionActions.SetSessionStatusAction action, IDispatcher dispatcher)
    {
        // Only notify for background sessions
        if (action.SessionId == _sessionStore.Value.ActiveSessionId)
            return Task.CompletedTask;

        var entry = _sessionStore.Value.Sessions.GetValueOrDefault(action.SessionId);
        if (entry is null) return Task.CompletedTask;

        SessionNotification? notification = action.Status switch
        {
            SessionStatus.Completed => new SessionNotification
            {
                Type = NotificationType.SessionCompleted,
                SessionId = action.SessionId,
                Title = entry.Metadata.Title,
                Message = "Agent finished responding",
                Timestamp = DateTimeOffset.UtcNow,
                IsUrgent = false
            },
            SessionStatus.Error => new SessionNotification
            {
                Type = NotificationType.SessionError,
                SessionId = action.SessionId,
                Title = entry.Metadata.Title,
                Message = "An error occurred during streaming",
                Timestamp = DateTimeOffset.UtcNow,
                IsUrgent = false
            },
            _ => null
        };

        if (notification is not null)
        {
            dispatcher.Dispatch(new ShowNotificationAction(notification));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Fires when a session's pending approval flag changes.
    /// Produces an urgent notification for background HITL approvals.
    /// </summary>
    [EffectMethod]
    public Task OnApprovalRequired(
        SessionActions.SetHasPendingApprovalAction action, IDispatcher dispatcher)
    {
        if (!action.HasPending) return Task.CompletedTask;
        if (action.SessionId == _sessionStore.Value.ActiveSessionId)
            return Task.CompletedTask;

        var entry = _sessionStore.Value.Sessions.GetValueOrDefault(action.SessionId);
        if (entry is null) return Task.CompletedTask;

        // Extract tool name from PendingApproval if available
        string toolInfo = entry.State.PendingApproval?.ToolName ?? "unknown tool";

        dispatcher.Dispatch(new ShowNotificationAction(new SessionNotification
        {
            Type = NotificationType.ApprovalRequired,
            SessionId = action.SessionId,
            Title = entry.Metadata.Title,
            Message = $"Approval needed: {toolInfo}",
            Timestamp = DateTimeOffset.UtcNow,
            IsUrgent = true
        }));

        return Task.CompletedTask;
    }
}
```

### 6.3 Notification-to-Action Gateway

When the user clicks a notification toast, the click handler dispatches `SetActiveSessionAction` to switch to the notification's session:

```csharp
// Inside the toast click handler
private void OnNotificationClicked(SessionNotification notification)
{
    Dispatcher.Dispatch(new SessionActions.SetActiveSessionAction(notification.SessionId));
    
    // For ApprovalRequired, the approval dialog is already visible because
    // PendingApproval was set in the session's SessionState by the streaming service.
    // No additional action needed — switching to the session shows the dialog.
}
```

This is the complete pipeline: **background SSE → Fluxor dispatch → Effect creates notification → toast renders → user clicks → session switch → approval dialog visible**.

---

## 7. BB v3 Toast Integration

### 7.1 BbToastProvider Configuration

BB v3's `BbToastProvider` supports semantic variants, per-toast positioning, and pause-on-hover. The root layout configures it for session notifications:

```razor
@* In MainLayout.razor or App.razor *@
<BbToastProvider Position="ToastPosition.BottomRight"
                 MaxVisible="3"
                 Duration="5000"
                 SwipeDirection="SwipeDirection.Right" />
```

### 7.2 Toast Variant Mapping

| NotificationType | BB Toast Variant | Duration | Dismiss Behavior |
|-----------------|-----------------|----------|-----------------|
| `SessionCompleted` | `ToastVariant.Success` | 5 seconds | Auto-dismiss; swipe to dismiss; click to navigate |
| `ApprovalRequired` | `ToastVariant.Warning` | **Persistent** (0 = no auto-dismiss) | Click to navigate; explicit close button; does NOT auto-dismiss |
| `SessionError` | `ToastVariant.Destructive` | 8 seconds | Auto-dismiss; swipe to dismiss; click to navigate |

### 7.3 Toast Content Structure

Each notification toast renders with:

```
┌─────────────────────────────────────────┐
│ 🟢 Draft email                     ✕   │  ← Title + close button
│ Agent finished responding               │  ← Message
│ Click to view                           │  ← Action hint
└─────────────────────────────────────────┘
```

For `ApprovalRequired`:

```
┌─────────────────────────────────────────┐
│ ⚠️ Help me send email (pulsing)    ✕   │  ← Urgent indicator
│ Approval needed: send_email             │  ← Tool name
│ Click to approve or reject              │  ← Action hint
└─────────────────────────────────────────┘
```

### 7.4 IToastService Integration

BB v3's `IToastService` is injected and called by the notification rendering component:

```csharp
[Inject] private IToastService ToastService { get; set; } = default!;

private void ShowNotificationToast(SessionNotification notification)
{
    var variant = notification.Type switch
    {
        NotificationType.SessionCompleted => ToastVariant.Success,
        NotificationType.ApprovalRequired => ToastVariant.Warning,
        NotificationType.SessionError => ToastVariant.Destructive,
        _ => ToastVariant.Info
    };

    var duration = notification.IsUrgent ? 0 : notification.Type switch
    {
        NotificationType.SessionCompleted => 5000,
        NotificationType.SessionError => 8000,
        _ => 5000
    };

    ToastService.Show(builder =>
    {
        builder.SetTitle(notification.Title);
        builder.SetDescription(notification.Message ?? string.Empty);
        builder.SetVariant(variant);
        builder.SetDuration(duration);
        builder.SetAction("View", () => OnNotificationClicked(notification));
    });
}
```

---

## 8. HITL Approval Notification Flow

### 8.1 The Problem

HITL approval in background sessions is the most complex UX flow in the multi-session architecture. The agent is **blocked** on the server side (SSE connection held open), waiting for a `FunctionApprovalResponseContent` from the client. But the user is viewing a different session.

> **Ref:** ISS-020 — "HITL in background sessions — what happens when an approval is needed in a session the user isn't viewing?"  
> **Ref:** Q-NOTIF-004 — "For HITL approvals in background sessions, should the notification be clickable to switch to that session and show the approval dialog?"

### 8.2 Complete HITL Background Flow

```
1. Session "sess-b" is streaming in Background status
   (User is viewing "sess-a")

2. Agent invokes approval-required tool (send_email)
   → ServerFunctionApprovalAgent emits FunctionApprovalRequestContent
   → SSE delivers TOOL_CALL_START with approval metadata to client

3. AgentStreamingService.ProcessAgentResponseAsync("sess-b"):
   a. Detects FunctionApprovalRequestContent
   b. Creates ApprovalTaskSource for sess-b's SessionStreamingContext
   c. Dispatch: SetPendingApprovalAction("sess-b", approvalDetails)
   d. Dispatch: SetHasPendingApprovalAction("sess-b", true)
   e. Await: ctx.ApprovalTaskSource.Task
      → SSE stream is PAUSED — connection stays open but no new events

4. Fluxor Effects fire (NotificationEffects.OnApprovalRequired):
   a. Detects: sess-b is NOT the active session AND HasPending = true
   b. Dispatch: ShowNotificationAction(type: ApprovalRequired, ...)

5. UI updates (via Blazor SignalR circuit):
   a. Sidebar: sess-b shows amber pulsing icon + "Approval needed" tooltip
   b. Toast: Warning variant appears — "Help me send email — Approval needed: send_email"

6. User clicks toast (or clicks sess-b in sidebar):
   a. Dispatch: SetActiveSessionAction("sess-b")
   b. Reducer: sess-b becomes foreground
   c. Chat area renders sess-b's messages
   d. Approval dialog renders because PendingApproval is set in sess-b's SessionState

7. User approves (or rejects):
   a. Click "Approve" → ResolveApproval("sess-b", true)
   b. AgentStreamingService completes ApprovalTaskSource with result
   c. Dispatch: SetHasPendingApprovalAction("sess-b", false)
   d. Dispatch: SetPendingApprovalAction("sess-b", null)
   e. SSE stream resumes — agent continues with the approved action

8. Session "sess-b" continues streaming (now in foreground as Streaming status)
```

### 8.3 The "Only One Approval Dialog at a Time" Constraint

**Invariant**: At most one approval dialog is visible on screen at any time.

This is naturally enforced by the architecture:
- The approval dialog component renders based on `ActiveSession.State.PendingApproval`
- Only the active session's state is rendered in the chat area
- When the user switches sessions, the previous session's approval dialog disappears and the new session's dialog appears (if it has one)
- Each session's `ApprovalTaskSource` is independent — approving in one session does not affect others

**What if multiple sessions have pending approvals?**

| Session | Status | PendingApproval | Visible? |
|---------|--------|-----------------|----------|
| sess-a | Active | `send_email(to: alice)` | ✅ Approval dialog visible |
| sess-b | Background | `send_email(to: bob)` | ❌ Toast notification only — dialog shows when user switches |
| sess-c | Background | `null` | N/A |

The user resolves approvals one at a time by switching between sessions. Each session's toast persists until acknowledged. The toast stack serves as a queue of pending approvals.

### 8.4 Timeout Behavior

For MVP, HITL approvals **do not timeout** (see 03-session-lifecycle.md §10.3):

- The SSE connection stays open indefinitely (Kestrel supports long-lived connections)
- The amber notification badge persists in the sidebar
- The warning toast persists until dismissed or clicked
- The agent's `ApprovalTaskSource.Task` blocks until the user responds

Future enhancement: configurable timeout with auto-reject and notification.

---

## 9. Concurrent Stream Management

### 9.1 Stream Cap

Per 03-session-lifecycle.md §7 and R13, the system caps concurrent active SSE streams at **5 per Blazor circuit**. This section specifies the cap, queue, and resume strategy in detail.

> **Ref:** R13 — "HTTP/2 multiplexing eliminates the HTTP/1.1 6-connection limit, but practical limit: 3-5 concurrent active streams recommended."  
> **Ref:** Q-SYNC-004 — "Cap at 3-5 concurrent streams."

### 9.2 Stream States

Each session's SSE stream is in one of these states:

| Stream State | Description | Count Toward Cap? |
|-------------|-------------|-------------------|
| **Active** | Connected and receiving events | ✅ Yes |
| **Queued** | Waiting for a slot to open | ❌ No |
| **Completed** | Stream finished (`RUN_FINISHED`) | ❌ No |
| **Canceled** | User canceled or preempted | ❌ No |
| **Error** | Stream failed | ❌ No |

### 9.3 Queue and Resume Strategy

```
AgentStreamingService
├── MaxConcurrentStreams = 5
├── _activeStreams: ConcurrentDictionary<string, Task>  (running streams)
├── _streamQueue: ConcurrentQueue<QueuedStreamRequest>  (waiting streams)
│
├── ProcessAgentResponseAsync(sessionId, chatClient):
│   │
│   ├── IF _activeStreams.Count < MaxConcurrentStreams:
│   │   └── Start stream immediately → add to _activeStreams
│   │
│   └── ELSE:
│       └── Enqueue request → dispatch toast:
│           "Agent response queued — other sessions still processing"
│
├── OnStreamCompleted(sessionId):
│   │
│   ├── Remove from _activeStreams
│   └── IF _streamQueue has items:
│       └── Dequeue oldest → start stream → add to _activeStreams
│
└── PreemptForForeground(sessionId):
    │
    ├── User sends message in active session but cap is full
    ├── Find oldest background stream → Cancel it → Remove from _activeStreams
    └── Start foreground session's stream immediately
```

### 9.4 Preemption Policy

Active (foreground) session requests **always** get priority. If the cap is full and all streams are background, the oldest background stream is preempted:

1. User sends message in foreground session
2. Cap is full (5 background streams active)
3. Cancel the stream with the oldest start time (`CancellationTokenSource.Cancel()`)
4. The canceled session transitions to `Error` with message: "Stream preempted — will resume when a slot is available"
5. Add a `QueuedStreamRequest` for the preempted session (it will auto-resume)
6. Start the foreground session's stream immediately

This ensures the user **never waits** for a response in the session they're actively viewing.

### 9.5 Queue Notification

When a session's stream request is queued (cap reached), a brief informational toast notifies the user:

- Toast variant: **Info**
- Duration: 3 seconds
- Message: "Response queued — other sessions are still processing"
- Not clickable (informational only)

---

## 10. Sequence Diagram: Background Session Completion

### 10.1 Full Flow: Background Session Completes While User Views Another

```
┌──────────┐         ┌────────────────────┐         ┌─────────────┐        ┌──────────┐
│  Browser  │         │ AgentStreamingService│         │    Fluxor   │        │  Server  │
│ (Blazor)  │         │  (background Task)  │         │  Dispatcher │        │ (AGUI)   │
└─────┬─────┘         └──────────┬──────────┘         └──────┬──────┘        └────┬─────┘
      │                          │                           │                     │
      │  User viewing sess-a     │                           │                     │
      │  sess-b streaming in BG  │                           │                     │
      │                          │                           │                     │
      │                          │◄──────────── SSE: TEXT_MESSAGE_CONTENT ─────────│
      │                          │                           │                     │
      │                          │──── Dispatch: ────────────►                     │
      │                          │  AddMessageAction(sess-b) │                     │
      │                          │  IncrementUnreadAction     │                     │
      │                          │     (sess-b)              │                     │
      │                          │                           │                     │
      │◄───── StateChanged ──────┼───────────────────────────│                     │
      │  (sidebar re-renders:    │                           │                     │
      │   sess-b unread = +1)    │                           │                     │
      │                          │                           │                     │
      │                          │◄──────────── SSE: RUN_FINISHED ────────────────│
      │                          │                           │                     │
      │                          │──── Dispatch: ────────────►                     │
      │                          │  SetRunningAction         │                     │
      │                          │    (sess-b, false)        │                     │
      │                          │  SetSessionStatusAction   │                     │
      │                          │    (sess-b, Completed)    │                     │
      │                          │                           │                     │
      │                          │             ┌─────────────┼───────────────┐     │
      │                          │             │ NotificationEffects         │     │
      │                          │             │ detects: sess-b ≠ active    │     │
      │                          │             │ AND status = Completed      │     │
      │                          │             │ Dispatch:                   │     │
      │                          │             │   ShowNotificationAction    │     │
      │                          │             └─────────────┼───────────────┘     │
      │                          │                           │                     │
      │◄───── StateChanged ──────┼───────────────────────────│                     │
      │                          │                           │                     │
      │  1. Sidebar: sess-b      │                           │                     │
      │     shows ✓ + unread     │                           │                     │
      │  2. Toast: "Draft email  │                           │                     │
      │     — Agent finished"    │                           │                     │
      │                          │                           │                     │
      │  User clicks toast       │                           │                     │
      │──── Dispatch: ───────────┼───────────────────────────►                     │
      │  SetActiveSessionAction  │                           │                     │
      │    ("sess-b")            │                           │                     │
      │                          │                           │                     │
      │◄───── StateChanged ──────┼───────────────────────────│                     │
      │                          │                           │                     │
      │  Chat area renders       │                           │                     │
      │  sess-b's completed      │                           │                     │
      │  conversation            │                           │                     │
      │                          │                           │                     │
```

### 10.2 HITL Approval Variant

The same diagram applies for `ApprovalRequired`, with the additional step that after the user switches to the session, the approval dialog is already rendered (because `PendingApproval` is set in `SessionState`). The user approves/rejects, which completes the `ApprovalTaskSource`, and the SSE stream resumes — all within the existing Blazor circuit.

---

## 11. Future: Dedicated SignalR Hub & Browser Notifications

### 11.1 Limitations of the MVP Approach

The Blazor circuit piggyback approach has two limitations that a future dedicated SignalR hub would resolve:

| Limitation | Impact | SignalR Hub Solution |
|-----------|--------|---------------------|
| **Single-tab only** | Each browser tab has its own Blazor circuit with independent Fluxor state. Notifications in Tab A don't appear in Tab B. | A shared SignalR hub broadcasts session events to all connected tabs for the same user. |
| **No OS-level notifications** | In-app toasts require the browser tab to be visible. If the user is in another application, they won't see the notification. | The hub's JS client calls the Browser Notifications API (`Notification.requestPermission()` → `new Notification(...)`) for OS-level push. |

### 11.2 Future SignalR Hub Design (Sketch)

```csharp
// Future: SessionNotificationHub
public class SessionNotificationHub : Hub
{
    public async Task SubscribeToSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");
    }

    // Server-side: called by AgentStreamingService when a session completes
    public static async Task NotifySessionCompleted(
        IHubContext<SessionNotificationHub> hubContext,
        string sessionId, string title)
    {
        await hubContext.Clients.Group($"session:{sessionId}")
            .SendAsync("SessionCompleted", sessionId, title);
    }
}
```

### 11.3 Future Browser Notifications API Integration

```javascript
// Future: JS interop for Browser Notifications
async function requestNotificationPermission() {
    if ("Notification" in window) {
        const permission = await Notification.requestPermission();
        return permission === "granted";
    }
    return false;
}

function showBrowserNotification(title, message, sessionId) {
    if (Notification.permission === "granted") {
        const notification = new Notification(title, {
            body: message,
            icon: "/favicon.ico",
            tag: `session-${sessionId}`, // Prevents duplicate notifications
            requireInteraction: false
        });

        notification.onclick = () => {
            window.focus();
            // DotNet interop to switch session
            DotNet.invokeMethodAsync('AGUIDojoClient', 'SwitchToSession', sessionId);
        };
    }
}
```

### 11.4 Migration Path

The MVP Fluxor-based notification pipeline (§6) is designed to be **forward-compatible** with the SignalR hub approach:

1. **Phase 1 (MVP — current design)**: Notifications via Fluxor Effects → BB toasts. Single-tab, in-app only.
2. **Phase 2**: Add `SessionNotificationHub` alongside the existing Fluxor pipeline. Hub broadcasts to all tabs; each tab's Effect also fires. Deduplication by `SessionNotification.Timestamp`.
3. **Phase 3**: Add Browser Notifications API via JS interop. Hub's JS client triggers OS-level notifications when the tab is not focused.

Each phase is additive — no existing code is replaced, only augmented.

---

## 12. Design Decisions Summary

| Decision | Choice | Rationale | Reference |
|----------|--------|-----------|-----------|
| Push mechanism (MVP) | Blazor circuit piggyback (Fluxor dispatch → re-render) | Zero new infrastructure; Blazor Server already has SignalR circuit | Q-SYNC-001, Q-SYNC-002, R10 |
| Background session state | Full state continuity (no lazy hydration) | Instant switch-back; AG-UI protocol is stateless (no replay); simpler implementation | Q-SYNC-003 |
| Notification trigger | Fluxor Effects reacting to status transitions | Decoupled from streaming service; testable; composable | Q-NOTIF-001 |
| Notification types (MVP) | SessionCompleted, ApprovalRequired, SessionError | Covers the three user-actionable background events | Q-NOTIF-001 |
| Notification UI (MVP) | BB v3 `BbToastProvider` with semantic variants | In-app, immediate, no permission required; BB v3 supports all needed features | Q-NOTIF-002 |
| HITL in background | Clickable toast → switch session → approval dialog visible | Minimal UX friction; agent blocks until user responds | Q-NOTIF-004, ISS-020 |
| One approval dialog at a time | Enforced by rendering only active session's PendingApproval | Natural constraint of single-active-session UI model | Q-NOTIF-004 |
| Concurrent stream cap | 5 per circuit; FIFO queue; foreground preemption | HTTP/2 removes hard limits but resource pressure remains; 5 is practical | Q-SYNC-004, R13 |
| HITL timeout (MVP) | No timeout — blocks indefinitely | Auto-reject risks data loss; SSE connections are long-lived in Kestrel | Q-NOTIF-004 |
| Future push mechanism | Dedicated SignalR hub + Browser Notifications API | Cross-tab support; OS-level notifications when tab not focused | Q-NOTIF-002 |
