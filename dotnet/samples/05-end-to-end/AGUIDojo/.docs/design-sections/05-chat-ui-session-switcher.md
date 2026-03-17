# Design Section 05: Chat UI with Session Switcher

> **Spec Section:** Frontend Layout & Session Management UX  
> **Created:** 2026-02-24  
> **Status:** Design  
> **References:** Q-UX-001, Q-UX-002, Q-UX-003, Q-UX-004, Q-UX-005, Q-BB-002, Q-STATE-002, Q-NOTIF-004, R5, R8, ISS-015, ISS-016  
> **Depends On:** 01-unified-endpoint.md, 02-multi-session-state.md, 03-session-lifecycle.md, 04-realtime-sync-notifications.md, 06-agui-feature-integration.md  
> **Inherited By:** task-8, task-9

---

## Table of Contents

1. [Three-Pane Layout](#1-three-pane-layout)
2. [Session Sidebar Component](#2-session-sidebar-component)
3. [Session List Item Component](#3-session-list-item-component)
4. [Session Status Icons](#4-session-status-icons)
5. [ChatHeader Overhaul](#5-chatheader-overhaul)
6. [New Chat Flow](#6-new-chat-flow)
7. [Session Switching UX](#7-session-switching-ux)
8. [Responsive Behavior](#8-responsive-behavior)
9. [Keyboard Shortcuts](#9-keyboard-shortcuts)
10. [Wireframe Description: Three-Pane Layout](#10-wireframe-description-three-pane-layout)
11. [Component Hierarchy](#11-component-hierarchy)
12. [Integration with Other Design Sections](#12-integration-with-other-design-sections)
13. [Design Decisions Summary](#13-design-decisions-summary)

---

## 1. Three-Pane Layout

### 1.1 Layout Overview

The application uses a **three-pane layout**: session sidebar (collapsible) | chat pane | canvas pane. This follows the industry-standard pattern used by ChatGPT, Claude, and Gemini.

> **Ref:** Q-UX-001 — "Option (d): Collapsible sidebar using BB v3's `BbSidebar` component. This is the industry standard."

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           BbSidebarProvider                                 │
│ ┌──────────────┐ ┌────────────────────────────────────────────────────────┐ │
│ │              │ │  BbSidebarInset                                        │ │
│ │   BbSidebar  │ │ ┌──────────────────────────────────────────────────┐  │ │
│ │  (Session    │ │ │  Inset Header (SidebarTrigger | Session Title)   │  │ │
│ │   Sidebar)   │ │ ├──────────────────────────────────────────────────┤  │ │
│ │              │ │ │  DualPaneLayout                                  │  │ │
│ │  ┌────────┐  │ │ │ ┌──────────────────┐ ┌───────────────────────┐  │  │ │
│ │  │ Header │  │ │ │ │   ContextPane    │ │     CanvasPane        │  │  │ │
│ │  │ Search │  │ │ │ │   (Chat Area)    │ │   (Artifacts)         │  │  │ │
│ │  ├────────┤  │ │ │ │                  │ │                       │  │  │ │
│ │  │Session │  │ │ │ │ ┌──────────────┐ │ │ ┌───────────────────┐ │  │  │ │
│ │  │ List   │  │ │ │ │ │ MessageList  │ │ │ │ Plan|Recipe|Doc|  │ │  │  │ │
│ │  │ Items  │  │ │ │ │ │              │ │ │ │ Chart|Grid|Form   │ │  │  │ │
│ │  │        │  │ │ │ │ └──────────────┘ │ │ └───────────────────┘ │  │  │ │
│ │  │        │  │ │ │ │ ┌──────────────┐ │ │                       │  │  │ │
│ │  ├────────┤  │ │ │ │ │ Suggestions  │ │ │                       │  │  │ │
│ │  │ Footer │  │ │ │ │ │ ChatInput    │ │ │                       │  │  │ │
│ │  │NewChat │  │ │ │ │ └──────────────┘ │ │                       │  │  │ │
│ │  │ Theme  │  │ │ │ └──────────────────┘ └───────────────────────┘  │  │ │
│ │  └────────┘  │ │ └──────────────────────────────────────────────────┘  │ │
│ └──────────────┘ └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Before/After Comparison

| Aspect | Before (Current) | After (Multi-Session) |
|--------|-------------------|----------------------|
| Sidebar content | Endpoint selector (10 endpoints) | Session list with session items |
| Sidebar header | "AG-UI Dojo" branding | "AG-UI Dojo" branding + search trigger |
| Sidebar footer | "New Chat" + theme toggle | "New Chat" + theme toggle (unchanged) |
| Inset header title | Endpoint display name (`GetCurrentEndpointDisplayName()`) | Active session title + agent status |
| Chat pane data source | Global `ChatStore.Value.Messages` | `SessionSelectors.ActiveMessages(state)` |
| Canvas pane data source | Global `ArtifactStore` | `SessionSelectors.ActiveSessionState(state)` (artifact fields) |
| DualPaneLayout | Chat + Canvas (unchanged) | Chat + Canvas (unchanged structurally) |

### 1.3 Layout Foundation: BbSidebarProvider

The current `Chat.razor` already uses the BB Sidebar layout pattern. The restructuring **preserves the existing `BbSidebarProvider` → `BbSidebar` + `BbSidebarInset` → `DualPaneLayout`** hierarchy.

The BB Chat App blueprint (`blazorblueprintui.com/blueprints/apps/app-chat`) validates this pattern: `BbSidebarProvider` wraps the entire page, with `BbSidebar` containing navigation/channel list and `BbSidebarInset` containing the message area.

What changes:
- **`BbSidebar` content**: Endpoint list → Session list
- **Inset header**: Endpoint name → Session context bar
- **Data binding**: Global Fluxor selectors → Session-keyed selectors from `SessionManagerState`

### 1.4 Pane Width Distribution

| Pane | Default Width | Min Width | Collapsible? |
|------|-------------|-----------|-------------|
| Session sidebar | 280px | 48px (icon-only collapsed) | Yes — click trigger or keyboard shortcut |
| Chat pane (ContextPane) | Flexible (fills remaining) | 320px | No |
| Canvas pane | ~50% of inset | 280px | Yes — toggle button (existing) |

The BB `BbSidebar` component handles collapse/expand natively, including the icon-only collapsed state on desktop and drawer overlay on mobile.

---

## 2. Session Sidebar Component

### 2.1 Component: `SessionSidebar.razor`

This component replaces the current `ChatHeader.razor`'s endpoint list with a session-focused sidebar. The BB Chat App blueprint demonstrates the exact pattern: sidebar with grouped items, badges, and active state highlighting.

#### Structure

```
BbSidebar
├── BbSidebarHeader
│   ├── Brand identity ("AG-UI Dojo" + sparkles icon)
│   └── Session search trigger button (opens BbCommandDialog)
│
├── BbSidebarContent
│   ├── BbSidebarGroup (label: "Sessions")
│   │   └── BbSidebarMenu
│   │       └── BbScrollArea (vertical scroll for session list)
│   │           ├── SessionListItem (session-1, active)
│   │           ├── SessionListItem (session-2, background streaming)
│   │           ├── SessionListItem (session-3, completed)
│   │           └── ... (all sessions from SessionManagerState.SessionOrder)
│   │
│   └── (Empty state when no sessions — auto-created on cold start, so rare)
│
└── BbSidebarFooter
    ├── "New Chat" button (BbSidebarMenuButton + plus icon)
    └── Theme toggle (BbSidebarMenuButton + sun/moon icon)
```

### 2.2 Data Binding

The sidebar reads its data from the `SessionManagerState` Fluxor store:

```
SessionSidebar reads:
  ├── SessionManagerState.SessionOrder        → Iteration order for the list
  ├── SessionManagerState.Sessions[id].Metadata → Title, status, timestamp, unread, approval
  └── SessionManagerState.ActiveSessionId      → Which item shows active highlight
```

On session item click:
```
User clicks session item →
  Dispatch: SetActiveSessionAction(targetSessionId) →
  Reducer: Atomic session switch (see 03-session-lifecycle.md §5) →
  All components re-derive from new ActiveSessionId
```

### 2.3 Session List Ordering

Sessions are displayed in **reverse chronological order by `LastActivityAt`**, matching the pattern used by ChatGPT and Claude. The `SessionOrder` list in `SessionManagerState` is maintained by reducers on every `AddMessageAction` or status change.

| Rule | Behavior |
|------|----------|
| New session | Inserted at position 0 (top of list) |
| New message in session | Session bubbles to top |
| Background completion | Session does NOT auto-bubble (avoids jarring reorder during typing) |
| Session switch | No reorder (clicking doesn't change order) |

### 2.4 Search / Filter

A search trigger button in the sidebar header opens the `BbCommandDialog` (Cmd+K pattern) for quick session search. This is a **future enhancement** with the UI slot reserved.

> **Ref:** Q-BB-002 — "`BbCommandDialog` — session search/quick switch (Cmd+K)"

The `BbCommandDialog` lists all sessions filtered by title substring matching. Selecting a result dispatches `SetActiveSessionAction`. The dialog closes on selection or Escape.

---

## 3. Session List Item Component

### 3.1 Component: `SessionListItem.razor`

Each session in the sidebar is rendered as a `BbSidebarMenuItem` with a custom `BbSidebarMenuButton` interior. The BB Chat App blueprint demonstrates the pattern: icon + text + trailing badge inside `BbSidebarMenuButton`, with `IsActive` for highlight state.

#### Visual Layout

```
┌──────────────────────────────────────────────────────┐
│ [Status Icon]  Session Title...          [2] [Badge] │
│                2 minutes ago                         │
└──────────────────────────────────────────────────────┘
```

#### Decomposed Layout

```
BbSidebarMenuItem
└── BbSidebarMenuButton (IsActive=@isActive, @onclick=SwitchToSession)
    ├── Left: Status icon (16px, see §4)
    ├── Center (flex-1, min-w-0):
    │   ├── Row 1: Title (text-sm font-medium, truncate-ellipsis)
    │   └── Row 2: Relative timestamp (text-[10px] text-muted-foreground)
    └── Right (ml-auto, flex items-center gap-1):
        ├── Unread count badge (BbBadge variant=Secondary, if UnreadCount > 0)
        └── Approval badge (BbBadge variant=Destructive, pulsing, if HasPendingApproval)
```

### 3.2 Session Item States

| State | Status Icon | Title Style | Badges | Background |
|-------|------------|-------------|--------|-----------|
| **Active (current)** | Session status icon | `font-semibold` | None | `bg-sidebar-accent` (BB active state) |
| **Streaming (background)** | Animated spinner | Normal | Unread count (if > 0) | Default |
| **Completed** | Checkmark | Normal | Unread count (if > 0) | Default |
| **Error** | Warning triangle | `text-destructive` | Error badge | Default with `border-l-2 border-destructive` |
| **Needs Approval** | Bell (pulsing) | Normal | "Approval" badge (destructive, pulsing) | Default with subtle urgent highlight |
| **Created** | Dim circle | `text-muted-foreground italic` | None | Default |

### 3.3 Interaction States

| Interaction | Behavior |
|-------------|----------|
| **Hover** | `bg-sidebar-accent/50` (BB default on `BbSidebarMenuButton`) |
| **Click** | Dispatches `SetActiveSessionAction(sessionId)` → atomic session switch |
| **Right-click** | Context menu (future): Rename, Delete, Duplicate |
| **Long-press (mobile)** | Same as right-click (future) |

### 3.4 Unread Badge Behavior

The unread count badge appears when `SessionMetadata.UnreadCount > 0`. It is:
- Incremented by background session events (new messages, state changes) per [04-realtime-sync-notifications.md §3.1](04-realtime-sync-notifications.md#31-full-state-continuity)
- Cleared when the user switches to the session (dispatch `ClearUnreadAction` inside `SetActiveSessionAction` reducer)

Badge rendering:
```
@if (session.UnreadCount > 0)
{
    <BbBadge Variant="BadgeVariant.Secondary" Class="ml-auto h-5 px-1.5 text-[10px]">
        @session.UnreadCount
    </BbBadge>
}
```

This pattern is directly adapted from the BB Chat App blueprint, which uses `BbBadge` with `BadgeVariant.Secondary` for unread counts on channel items.

### 3.5 Approval Badge Behavior

When `SessionMetadata.HasPendingApproval` is true, an **urgent pulsing badge** appears:

```
@if (session.HasPendingApproval)
{
    <BbBadge Variant="BadgeVariant.Destructive" Class="ml-auto h-5 px-1.5 text-[10px] animate-pulse">
        !
    </BbBadge>
}
```

> **Ref:** Q-NOTIF-004 — "The session list item shows an 'Approval Required' badge (pulsing/urgent). Clicking the notification switches to that session and displays the approval dialog."

---

## 4. Session Status Icons

### 4.1 Icon Mapping

Each `SessionStatus` value maps to a Lucide icon rendered at 16×16px within the session list item.

| SessionStatus | Lucide Icon Name | Color | Animation | Visual Description |
|--------------|-----------------|-------|-----------|-------------------|
| `Created` | `circle` | `text-muted-foreground/40` | None | Dim empty circle — session exists but unused |
| `Active` | `circle` | `text-primary` | None | Solid primary-colored circle — active idle |
| `Streaming` | `loader` | `text-primary` | `animate-spin` | Spinning loader — agent is generating |
| `Background` | `loader` | `text-muted-foreground` | `animate-spin` | Spinning loader, muted — background stream |
| `Completed` | `check-circle` | `text-green-500` | None | Green checkmark circle — agent done |
| `Error` | `alert-triangle` | `text-destructive` | None | Red warning triangle — error state |
| `NeedsApproval`* | `bell` | `text-amber-500` | `animate-pulse` | Pulsing amber bell — HITL blocking |

*`NeedsApproval` is NOT a `SessionStatus` enum value. It is a derived visual state when `SessionMetadata.HasPendingApproval == true`. The status icon override logic:

```
Icon derivation:
  if (session.HasPendingApproval) → bell (pulsing amber)
  else → map SessionStatus to icon per table above
```

### 4.2 Icon Component Usage

```
<LucideIcon Name="@GetStatusIcon(session)" 
            Size="16" 
            class="@GetStatusIconClass(session)" />
```

The `GetStatusIcon` and `GetStatusIconClass` helper methods encapsulate the mapping from [§4.1](#41-icon-mapping), including the `HasPendingApproval` override.

---

## 5. ChatHeader Overhaul

### 5.1 Before (Current ChatHeader)

The current `ChatHeader.razor` is a `BbSidebar` component that serves as the **entire sidebar**. It contains:
- App branding ("AG-UI Dojo" + sparkles icon)
- Endpoint selector (10 `BbSidebarMenuButton` items, one per endpoint)
- "New Chat" button
- Theme toggle

> **Ref:** Q-UX-004 — "The endpoint selector dropdown is replaced by session metadata display."

### 5.2 After (Session Context Header)

The inset header (the bar inside `BbSidebarInset`, above the `DualPaneLayout`) transforms from an endpoint display to a **session context bar**:

```
Current inset header:
┌──────────────────────────────────────────────────────────────────────────┐
│ [☰ Sidebar Toggle]  |  Agentic Chat                     [Canvas Toggle] │
└──────────────────────────────────────────────────────────────────────────┘

New inset header:
┌──────────────────────────────────────────────────────────────────────────┐
│ [☰ Toggle] | [●] Session Title (editable)  [Status Pill] [...] [Canvas] │
└──────────────────────────────────────────────────────────────────────────┘
```

### 5.3 Inset Header Components

```
<header class="sidebar-inset-header">
    ├── BbSidebarTrigger (collapse/expand sidebar)
    ├── BbSeparator (vertical, h-6)
    ├── Status dot (live indicator matching session status color)
    ├── Session title (editable on double-click — future)
    │   └── Text: SessionMetadata.Title (truncated with ellipsis)
    ├── Agent status pill (see §5.4)
    ├── Session actions dropdown (see §5.5) — ml-auto pushes right
    └── Canvas toggle button (existing — shows when HasInteractiveArtifact)
</header>
```

### 5.4 Agent Status Pill

A small `BbBadge` displaying the agent's current state for the active session:

| Session State | Pill Text | Variant | Icon |
|--------------|-----------|---------|------|
| `IsRunning == true` | "Generating..." | `BadgeVariant.Default` | Animated spinner |
| `IsRunning == false`, `Status == Completed` | "Ready" | `BadgeVariant.Secondary` | None |
| `Status == Error` | "Error" | `BadgeVariant.Destructive` | Alert triangle |
| `HasPendingApproval` | "Approval Required" | `BadgeVariant.Destructive` + pulse | Bell |
| `Status == Created` | "New" | `BadgeVariant.Outline` | None |

### 5.5 Session Actions Dropdown

A `BbDropdownMenu` triggered by a "..." (more-horizontal) icon button, placed at the right edge of the inset header:

```
BbDropdownMenu
├── BbDropdownMenuTrigger
│   └── BbButton (Ghost, Icon size, "more-horizontal" icon)
└── BbDropdownMenuContent
    ├── BbDropdownMenuLabel: "Session Actions"
    ├── BbDropdownMenuSeparator
    ├── BbDropdownMenuItem: "Rename" (pencil icon) — future
    ├── BbDropdownMenuItem: "Clear Messages" (eraser icon) — dispatches ClearSessionMessagesAction
    ├── BbDropdownMenuSeparator
    └── BbDropdownMenuItem: "Delete Session" (trash-2 icon, text-destructive) — with confirmation
```

> **Ref:** Q-UX-005 — "Session deletion: confirmation + state cleanup — deferred but design needed"

#### Deletion Confirmation

When "Delete Session" is clicked, a `BbAlertDialog` appears asking for confirmation:
- Title: "Delete session?"
- Description: "This will permanently delete '{session.Title}' and all messages. This cannot be undone."
- Actions: "Cancel" (outline) | "Delete" (destructive)
- On confirm: dispatches `DestroySessionAction(sessionId)` → reducer archives session → if it was the active session, switch to the next available session or create a new one

### 5.6 Sidebar Component Rename

The current `ChatHeader.razor` is renamed conceptually:
- **Before**: `ChatHeader.razor` — a sidebar with endpoint navigation
- **After**: `SessionSidebar.razor` — a sidebar with session management

The `ChatHeader` name was a misnomer. The component functions as a full sidebar, not a "header." The new name reflects its role: session-focused sidebar navigation.

---

## 6. New Chat Flow

### 6.1 Trigger Points

| Trigger | Location | UX |
|---------|----------|-----|
| "New Chat" button | Sidebar footer (`BbSidebarMenuButton`) | Always visible |
| Keyboard shortcut | `Ctrl+Shift+N` | Global — works from any context |
| Empty state CTA | Chat area empty state | Large button: "Start a new conversation" |

### 6.2 Creation Sequence

```
1. User clicks "New Chat" (or presses Ctrl+Shift+N)
   │
2. Generate: sessionId = Guid.NewGuid().ToString()
   │
3. Dispatch: CreateSessionAction(sessionId, DateTimeOffset.UtcNow)
   │
4. Reducer (see 03-session-lifecycle.md §3):
   │ a. New SessionEntry with metadata (Title="New Chat", Status=Created)
   │ b. Insert at SessionOrder[0] (top of sidebar)
   │ c. ActiveSessionId = sessionId
   │ d. If previous session was Streaming → transition to Background
   │
5. UI re-renders (atomic — single dispatch cycle):
   │ a. Sidebar: new "New Chat" item at top, highlighted as active
   │ b. Chat area: empty state ("Start a conversation")
   │ c. Canvas pane: hidden or default state
   │
6. Focus chat input (auto-focus via ElementReference.FocusAsync)
```

### 6.3 UX Detail: Smooth Transition

When creating a new session while another is active:
- The previous session's messages **remain in memory** (accessible by clicking its sidebar item)
- If the previous session was streaming, it transitions to `Background` and continues receiving tokens
- The sidebar immediately shows both sessions, with the new one highlighted

### 6.4 Edge Cases

| Scenario | Behavior |
|----------|----------|
| Multiple rapid "New Chat" clicks | Each creates a new session (no debounce — sessions are cheap) |
| "New Chat" while current session has no messages | Create new session anyway — empty sessions are cleaned up on archive |
| "New Chat" while HITL approval is pending | New session created; pending session stays in Background with approval badge |

---

## 7. Session Switching UX

### 7.1 Switching Mechanism

Session switching is a **single-dispatch atomic operation** (see [02-multi-session-state.md §7](02-multi-session-state.md#7-atomic-session-switching) and [03-session-lifecycle.md §5](03-session-lifecycle.md#5-session-activation-and-switching)).

**User action**: Click a session item in the sidebar  
**System response**: Dispatch `SetActiveSessionAction(targetSessionId)` → all UI components re-derive from the new active session in a single render cycle.

### 7.2 Transition Style: Instant Swap

Session switching uses **instant swap** (no animation). Rationale:

| Option | Description | Decision |
|--------|-------------|----------|
| ~~Cross-fade~~ | 200ms opacity transition between old and new session content | Rejected — adds perceived latency; Fluxor state swap is synchronous |
| ~~Slide~~ | Chat area slides left/right like mobile tab switching | Rejected — doesn't match desktop chat app conventions |
| **Instant swap** | Immediate re-render with new session data | **Selected** — matches ChatGPT, Claude, Copilot behavior; zero perceived latency since state is in-memory |

Since all session data lives in the Fluxor `SessionManagerState` dictionary (in-memory), the switch is synchronous. There is no loading state, no skeleton, no spinner. The chat messages, plan, artifacts all appear instantly from the pre-existing state snapshot.

### 7.3 Visual Feedback on Switch

Even with instant swap, visual feedback confirms the switch occurred:

| Feedback | Mechanism |
|----------|-----------|
| Sidebar active highlight | `BbSidebarMenuButton IsActive` change — previous item loses highlight, target gains it |
| Inset header title | Updates to target session's title |
| Agent status pill | Updates to target session's agent state |
| Chat area scroll | Scrolls to bottom of target session's message list |
| Canvas pane | Updates to target session's artifacts (or hides if no artifacts) |

### 7.4 Scroll Position Preservation (Future)

For MVP, switching to a session always scrolls to the bottom of its message list. A future enhancement can preserve and restore per-session scroll positions using a `ScrollPosition` field in `SessionState`.

---

## 8. Responsive Behavior

### 8.1 Breakpoint Strategy

| Viewport | Sidebar Behavior | Canvas Behavior |
|----------|-----------------|----------------|
| **Desktop** (≥1024px) | Expanded sidebar (280px) with full session items | Visible by default when artifacts exist |
| **Tablet** (768px–1023px) | Collapsed sidebar (icon-only, 48px) | Hidden by default, toggle to show |
| **Mobile** (<768px) | **Drawer overlay** — slides from left edge | Hidden by default, full-screen when shown |

### 8.2 BB v3 BbSidebar Responsiveness

The `BbSidebar` component handles responsive behavior natively:
- **Collapsible mode** (`Collapsible="icon"` or `Collapsible="offcanvas"`): Desktop collapse to icon-only
- **Mobile drawer**: On viewports below the BB breakpoint, `BbSidebar` automatically becomes a drawer overlay triggered by `BbSidebarTrigger`

Configuration:
```
<BbSidebarProvider>
  <BbSidebar Collapsible="icon">
    <!-- Session list content -->
  </BbSidebar>
  <BbSidebarInset>
    <!-- Chat + Canvas -->
  </BbSidebarInset>
</BbSidebarProvider>
```

### 8.3 Mobile Session Switching

On mobile, the sidebar is a full-height drawer overlay:
1. User taps `BbSidebarTrigger` (hamburger icon) in the inset header
2. Drawer slides in from the left, showing the full session list
3. User taps a session item
4. Dispatch `SetActiveSessionAction` → drawer closes automatically → chat area shows new session

The drawer auto-close on item selection is handled by the `BbSidebar` component's built-in mobile behavior.

### 8.4 Canvas Pane Responsive

The canvas pane (right side of `DualPaneLayout`) behavior is **unchanged** from the current implementation:
- Desktop: Visible when `HasInteractiveArtifact` is true
- Mobile: Hidden by default; toggle button in inset header shows/hides it
- The `DualPaneLayout` component handles the responsive split

---

## 9. Keyboard Shortcuts

### 9.1 Shortcut Registry

| Shortcut | Action | Context | Implementation |
|----------|--------|---------|---------------|
| `Ctrl+Shift+N` | Create new session | Global | Dispatch `CreateSessionAction` → focus chat input |
| `Ctrl+K` | Open session search | Global | Opens `BbCommandDialog` with session list |
| `Ctrl+[` | Toggle sidebar | Global | Calls `BbSidebar` collapse/expand API |
| `Escape` | Close search dialog | When dialog is open | `BbCommandDialog` built-in behavior |
| `↑ / ↓` | Navigate session list in search | When search dialog is open | `BbCommandDialog` built-in keyboard nav |
| `Enter` | Select session in search | When search dialog focused on item | Dispatch `SetActiveSessionAction` → close dialog |

### 9.2 Session Search via BbCommandDialog

The `BbCommandDialog` provides a Spotlight/Alfred-style search overlay:

```
┌─────────────────────────────────────────────────────┐
│  🔍 Search sessions...                              │
├─────────────────────────────────────────────────────┤
│  Sessions                                            │
│  ┌─────────────────────────────────────────────────┐ │
│  │ [●] Help me write a marketing plan    2 min ago │ │
│  │ [✓] Debug the authentication flow    15 min ago │ │
│  │ [●] Plan the team offsite             1 hr ago  │ │
│  └─────────────────────────────────────────────────┘ │
│                                                      │
│  Actions                                             │
│  ┌─────────────────────────────────────────────────┐ │
│  │ [+] New Chat                       Ctrl+Shift+N │ │
│  └─────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

The dialog renders:
- **Session group**: All sessions matching the search query (title substring), sorted by `LastActivityAt`
- **Actions group**: Static actions like "New Chat" with their keyboard shortcut hints
- Each session item shows: status icon, title (highlighted match), relative timestamp

### 9.3 Implementation Approach

Keyboard shortcuts are registered via a JavaScript interop layer that listens for `keydown` events and invokes .NET callbacks via `DotNetObjectReference`. The `Chat.razor` component registers handlers on `OnAfterRenderAsync(firstRender)` and unregisters on `Dispose()`.

```
window.addEventListener('keydown', (e) => {
    if (e.ctrlKey && e.shiftKey && e.key === 'N') {
        e.preventDefault();
        dotNetRef.invokeMethodAsync('OnNewChatShortcut');
    }
    if (e.ctrlKey && e.key === 'k') {
        e.preventDefault();
        dotNetRef.invokeMethodAsync('OnSearchShortcut');
    }
});
```

---

## 10. Wireframe Description: Three-Pane Layout

### 10.1 Desktop View (≥1024px) — Full Layout

```
┌───────────────────────────────────────────────────────────────────────────────────┐
│ AG-UI Dojo Chat                                                          [—][□][×]│
├──────────────┬────────────────────────────────────────────────────────────────────┤
│              │ [☰]│ ● Help me write a plan  [Generating...]           [...] [◫]  │
│  AG-UI Dojo  │───────────────────┬───────────────────────────────────────────────│
│  [🔍 Search] │                   │                                               │
│──────────────│   Chat Messages   │         Canvas Pane                           │
│  Sessions    │                   │                                               │
│──────────────│  👤 You           │   ┌─ Plan ─┬ Recipe ┬ Doc ─┐                  │
│              │  Help me plan a   │   │ ✓ Step 1: Research      │                  │
│ ● Help me    │  marketing...     │   │ → Step 2: Draft copy    │                  │
│   write a    │                   │   │ ○ Step 3: Review        │                  │
│   plan    2m │  🤖 Assistant     │   │ ○ Step 4: Publish       │                  │
│              │  I'll create a    │   │                          │                  │
│ ✓ Debug the  │  marketing plan   │   └──────────────────────────┘                  │
│   auth     15m│  for you...      │                                               │
│              │  ████████▒▒▒▒▒   │                                               │
│ ✓ Plan the   │  (streaming)      │                                               │
│   offsite  1h│                   │                                               │
│              │                   │                                               │
│──────────────│───────────────────│                                               │
│ [+ New Chat] │  💡 Suggestions   │                                               │
│ [🌙 Theme]   │  [Type a message...]  [Send]                                     │
└──────────────┴───────────────────┴───────────────────────────────────────────────┘
```

### 10.2 Desktop View — Sidebar Collapsed (Icon-Only)

```
┌────────────────────────────────────────────────────────────────────────────────────┐
│ [☰]│ ● Help me write a plan  [Generating...]                       [...] [◫]     │
├────┼──────────────────────────────┬────────────────────────────────────────────────┤
│ ★  │                              │                                                │
│    │   Chat Messages              │         Canvas Pane                            │
│ ●  │                              │                                                │
│ ✓  │  👤 You                      │   Plan | Recipe | Doc                          │
│ ✓  │  Help me plan a...           │   ...                                          │
│    │                              │                                                │
│    │  🤖 Assistant                │                                                │
│    │  I'll create a plan...       │                                                │
│    │                              │                                                │
│────│──────────────────────────────│                                                │
│ +  │  [Type a message...]  [Send] │                                                │
│ 🌙 │                              │                                                │
└────┴──────────────────────────────┴────────────────────────────────────────────────┘
```

In icon-only mode, each session is represented by its status icon only. Hovering shows a tooltip with the session title. The sidebar header shows the app icon (★ sparkles), and footer shows "+" and theme icons.

### 10.3 Mobile View (<768px) — Sidebar as Drawer Overlay

```
┌──────────────────────────────┐
│ [☰]│ ● Help me plan...     │
├──────────────────────────────┤
│                              │
│   👤 You                     │
│   Help me plan a marketing   │
│   campaign...                │
│                              │
│   🤖 Assistant               │
│   I'll create a marketing    │
│   plan for you. Let me       │
│   start by...                │
│   ████████▒▒▒                │
│                              │
│──────────────────────────────│
│  [Type a message...]  [Send] │
└──────────────────────────────┘

    ┌─── Sidebar Drawer (overlay) ────────────┐
    │ (slides from left on ☰ tap)              │
    │                                          │
    │  AG-UI Dojo                              │
    │  [🔍 Search sessions]                    │
    │                                          │
    │  Sessions                                │
    │  ─────────                               │
    │  ● Help me write a plan          2 min   │
    │  ✓ Debug the auth flow          15 min   │
    │  ✓ Plan the team offsite          1 hr   │
    │                                          │
    │  [+ New Chat]                            │
    │  [🌙 Theme]                              │
    └──────────────────────────────────────────┘
```

Tapping a session in the drawer: dispatches `SetActiveSessionAction` → drawer closes → chat area shows selected session content.

### 10.4 Session with HITL Approval Badge and Error State

```
Session sidebar detail:
┌──────────────────────────────────────────────────────────┐
│  Sessions                                                 │
│  ─────────────────────────                                │
│  [●] Help me write a plan                  2 min ago      │
│  [🔔] Send email to client  [!]            5 min ago      │  ← pulsing bell + red "!" badge
│  [⚠] Broken API integration                8 min ago      │  ← red warning, destructive text
│  [✓] Debug the auth flow                  15 min ago      │
│  [○] New Chat                             just now        │  ← dim circle, italic text
└──────────────────────────────────────────────────────────┘
```

---

## 11. Component Hierarchy

### 11.1 Full Component Tree

The complete component hierarchy for the three-pane layout:

```
Chat.razor (@page "/")
├── SessionSidebar.razor (was ChatHeader.razor)
│   ├── BbSidebar
│   │   ├── BbSidebarHeader
│   │   │   ├── Brand (sparkles + "AG-UI Dojo")
│   │   │   └── Search button (opens SessionSearchDialog)
│   │   ├── BbSidebarContent
│   │   │   └── BbSidebarGroup ("Sessions")
│   │   │       └── BbSidebarMenu
│   │   │           └── BbScrollArea
│   │   │               └── @foreach session in ordered sessions:
│   │   │                   └── SessionListItem.razor
│   │   │                       └── BbSidebarMenuItem
│   │   │                           └── BbSidebarMenuButton
│   │   │                               ├── StatusIcon (LucideIcon)
│   │   │                               ├── Title + Timestamp
│   │   │                               └── Badges (unread, approval)
│   │   └── BbSidebarFooter
│   │       ├── "New Chat" (BbSidebarMenuButton)
│   │       └── Theme toggle (BbSidebarMenuButton)
│   └── SessionSearchDialog.razor
│       └── BbCommandDialog
│           ├── BbCommandInput (search field)
│           ├── BbCommandGroup ("Sessions")
│           │   └── @foreach matching session:
│           │       └── BbCommandItem
│           └── BbCommandGroup ("Actions")
│               └── BbCommandItem ("New Chat")
│
├── BbSidebarInset
│   ├── <header> (Inset Header — session context bar)
│   │   ├── BbSidebarTrigger
│   │   ├── BbSeparator (vertical)
│   │   ├── Status dot
│   │   ├── Session title text
│   │   ├── Agent status pill (BbBadge)
│   │   ├── Session actions (BbDropdownMenu)
│   │   └── Canvas toggle (BbButton)
│   │
│   └── DualPaneLayout.razor (existing, unchanged)
│       ├── ContextPane.razor (existing)
│       │   ├── ChatMessageList (data: active session messages)
│       │   ├── ChatSuggestions
│       │   └── ChatInput
│       └── CanvasPane.razor (existing)
│           └── BB Tabs (Plan | Recipe | Document | Chart | Grid | Form)
│
└── DeleteConfirmationDialog.razor
    └── BbAlertDialog (confirm session deletion)
```

### 11.2 New Components Summary

| Component | Purpose | BB Components Used |
|-----------|---------|-------------------|
| `SessionSidebar.razor` | Session sidebar (replaces `ChatHeader.razor`) | `BbSidebar`, `BbSidebarHeader/Content/Footer`, `BbSidebarMenu`, `BbSidebarMenuButton`, `BbScrollArea` |
| `SessionListItem.razor` | Individual session item in sidebar | `BbSidebarMenuItem`, `BbSidebarMenuButton`, `BbBadge`, `LucideIcon` |
| `SessionSearchDialog.razor` | Cmd+K session search overlay | `BbCommandDialog`, `BbCommandInput`, `BbCommandGroup`, `BbCommandItem` |
| `DeleteConfirmationDialog.razor` | Session deletion confirmation | `BbAlertDialog`, `BbAlertDialogContent`, `BbAlertDialogAction` |

### 11.3 Modified Components

| Component | Change |
|-----------|--------|
| `Chat.razor` | Replace `ChatHeader` with `SessionSidebar`; change data binding from global stores to `SessionSelectors`; add keyboard shortcut registration |
| Inset header (in `Chat.razor`) | Replace endpoint name with session title + agent status pill + actions dropdown |

### 11.4 Unchanged Components

| Component | Notes |
|-----------|-------|
| `DualPaneLayout.razor` | Structural layout — unchanged |
| `ContextPane.razor` | Chat message area — data source changes (session-keyed) but template unchanged |
| `CanvasPane.razor` | Artifact tabs — data source changes (session-keyed) but template unchanged |
| `ChatInput.razor` | Message input — unchanged |
| `ChatMessageList.razor` | Message rendering — unchanged |
| `ChatSuggestions.razor` | Suggestion chips — unchanged |

---

## 12. Integration with Other Design Sections

### 12.1 Integration with 02-multi-session-state.md

| This Document Defines | Uses from 02 |
|-----------------------|-------------|
| Session list displays `SessionOrder` | `SessionManagerState.SessionOrder` for iteration |
| Session items show metadata | `SessionMetadata` fields (Title, Status, CreatedAt, UnreadCount, HasPendingApproval) |
| Click triggers session switch | `SetActiveSessionAction` → atomic switch (02 §7) |
| Data binding uses selectors | `SessionSelectors.ActiveMessages()`, `SessionSelectors.IsActiveSessionRunning()`, etc. |

### 12.2 Integration with 03-session-lifecycle.md

| This Document Defines | Uses from 03 |
|-----------------------|-------------|
| Status icons map to `SessionStatus` enum | 7-value enum: Created, Active, Streaming, Background, Completed, Error, Archived (03 §1) |
| New Chat flow | Creation sequence (03 §3) |
| Session switch transitions | Status transition rules: Streaming → Background, Background → Streaming (03 §5) |
| Deletion confirmation → `DestroySessionAction` | Session cleanup (03 §9) |

### 12.3 Integration with 04-realtime-sync-notifications.md

| This Document Defines | Uses from 04 |
|-----------------------|-------------|
| Unread badges on session items | `IncrementUnreadAction` dispatched from background SSE (04 §3) |
| Approval badge on session items | `SessionNotification` with `NotificationType.ApprovalRequired` (04 §5) |
| Toast notifications appearing | BB v3 `BbToastProvider` (04 §7) — toasts with "Go to session" action |

### 12.4 Integration with 06-agui-feature-integration.md

| This Document Defines | Uses from 06 |
|-----------------------|-------------|
| Canvas pane tabs per session | Tab management follows `VisibleTabs` per session (06 §7) |
| Tool-based UI renders inline in chat | `WeatherInfo`, `ApprovalDialog` render in chat messages (06 §3 F2, F3) |
| Predictive state pill in inset header | Future: streaming document preview pill when `write_document` is active |

---

## 13. Design Decisions Summary

| # | Decision | Rationale | Alternatives Considered |
|---|----------|-----------|------------------------|
| D1 | Three-pane layout: sidebar \| chat \| canvas | Industry standard (ChatGPT, Claude, Gemini). Proven UX for multi-session chat + artifacts. | Two-pane (tabs instead of sidebar) — rejected: tabs don't scale to many sessions |
| D2 | Collapsible sidebar using BB `BbSidebar` | Native BB component with collapse, mobile drawer, icon-only mode. Zero custom layout code. | Custom sidebar — rejected: reinvents BB functionality |
| D3 | Instant swap on session switch (no animation) | All state is in-memory (Fluxor). Synchronous swap = zero perceived latency. Matches ChatGPT/Claude. | Cross-fade (200ms) — rejected: adds latency for no benefit when data is instant |
| D4 | Session list ordered by `LastActivityAt` descending | Most recently active session on top. Familiar pattern from all chat apps. | Alphabetical — rejected: not useful for chat sessions |
| D5 | Status icon overridden by `HasPendingApproval` | Approval-blocked sessions need maximum visual urgency. Bell icon + pulse animation communicates "action needed." | Separate badge column — rejected: sidebar width is constrained |
| D6 | `BbCommandDialog` for session search (Ctrl+K) | Standard power-user pattern (VS Code, Slack, Notion). BB provides the component. | Search input in sidebar header — rejected: takes permanent vertical space |
| D7 | Inset header transforms to session context bar | Unified endpoint removes endpoint selection need. Header real estate → session context + agent status. | Separate header component — rejected: uses same inset header, just different content |
| D8 | Sidebar footer retains "New Chat" + theme toggle | Consistent with current UX. Footer is always visible even when scrolling session list. | "New Chat" in header — rejected: header is crowded with search; footer is standard placement |
| D9 | No scroll position preservation for MVP | Simplifies implementation. Users expect to see latest messages on switch. | Per-session scroll position — deferred to future enhancement |
| D10 | Keyboard shortcuts via JS interop | Blazor Server doesn't natively handle global keyboard shortcuts. JS listener → .NET callback is standard. | `@onkeydown` on root element — rejected: doesn't work reliably for global shortcuts in Blazor Server |
