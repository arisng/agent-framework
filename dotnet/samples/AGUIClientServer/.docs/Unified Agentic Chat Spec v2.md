# Unified Agentic Chat Spec v2

> **Version:** 2.0  
> **Date:** 2026-02-24  
> **Status:** Design  
> **Supersedes:** *Agentic UX Unified Spec — Blazor AG-UI Blueprint* (v1)  
> **Stack:** Blazor Server (.NET 10) · Microsoft Agent Framework (MAF) RC · AG-UI Protocol · Fluxor · BlazorBlueprint v3

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 2.0 | 2026-02-24 | Full rewrite: unified endpoint, multi-session state, session lifecycle, push notifications, BB v3 migration, AG-UI feature integration matrix |
| 1.0 | 2026-01-29 | Initial spec — 6 UX patterns, 10-endpoint architecture, BB v2.1.1, single-session |

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Overview & Goals](#2-overview--goals)
3. [Architecture Overview](#3-architecture-overview)
4. [Unified Agent Endpoint](#4-unified-agent-endpoint)
5. [Multi-Session State Architecture](#5-multi-session-state-architecture)
6. [Session Lifecycle Management](#6-session-lifecycle-management)
7. [Real-Time Sync & Push Notifications](#7-real-time-sync--push-notifications)
8. [Chat UI & Session Switcher](#8-chat-ui--session-switcher)
9. [AG-UI Feature Integration Matrix](#9-ag-ui-feature-integration-matrix)
10. [BB v3 Component Guide](#10-bb-v3-component-guide)
11. [Migration Guide](#11-migration-guide)
12. [Critique Resolution Matrix](#12-critique-resolution-matrix)
13. [Open Questions & Future Work](#13-open-questions--future-work)

---

## 1. Executive Summary

This spec is a ground-up redesign of the AG-UI Dojo agentic chat application. It resolves **52 issues** identified in the v1 spec critique (12 critical, 17 major, 13 minor) and introduces five fundamental architectural changes:

### Five Key Changes from v1

| # | Change | From (v1) | To (v2) | Primary Issues Resolved |
|---|--------|-----------|---------|------------------------|
| **1** | **Unified Endpoint** | 10 separate `MapAGUI` endpoints, each with a dedicated agent | Single `POST /chat` endpoint with composed wrapper pipeline | ISS-003, ISS-004, ISS-009, ISS-051 |
| **2** | **Multi-Session State** | 4 global Fluxor singletons, single conversation | 1 session-keyed `SessionManagerState` with `ImmutableDictionary<string, SessionEntry>` | ISS-031, ISS-043, ISS-044 |
| **3** | **Push Notifications** | Zero notification architecture | Fluxor dispatch from background threads → Blazor SignalR circuit → toast notifications | ISS-042 |
| **4** | **BB v3 Migration** | BB v2.1.1 (non-prefixed components, sub-namespaces, ApexCharts) | BB v3 (`Bb` prefix, flat namespaces, ECharts, two-layer portals) | ISS-005, ISS-045, ISS-046, ISS-047, ISS-048 |
| **5** | **Accurate Codebase Alignment** | Fictional types (`AgentService`, `ChatNode` DAG), non-existent multi-agent orchestration | Real types (`AgentStreamingService`, `ChatClientAgentFactory`), documented codebase | ISS-023, ISS-030, ISS-032, ISS-039, ISS-040 |

### What Was Removed from v1

- **DAG conversation branching** (§14.2 in v1) — never implemented; removed entirely (ISS-032)
- **Multi-Agent Orchestration UI** (§11 in v1) — fiction; zero multi-agent code exists (ISS-023)
- **Fictional code samples** — `AgentService`, `ChatNode`, `AgentStatus` enum replaced with real types (ISS-039, ISS-040)
- **Competitor comparison tables** — Telerik/Syncfusion/Blazorise comparisons removed; BB v3 is the committed choice (ISS-014)

---

## 2. Overview & Goals

### 2.1 What This Application Is

AG-UI Dojo is a **sample application** demonstrating all 7 AG-UI protocol features with Blazor Server + Microsoft Agent Framework (MAF). It serves as both a reference implementation and a developer testing tool for AG-UI capabilities.

### 2.2 The Seven AG-UI Features

| # | Feature | Description |
|---|---------|-------------|
| F1 | Agentic Chat | Base text streaming with markdown formatting |
| F2 | Backend Tool Rendering | Tool results rendered as rich components in the chat thread |
| F3 | Human-in-the-Loop (HITL) | User approval/rejection before tool execution |
| F4 | Agentic Generative UI | LLM-driven plan state via DataContent snapshots and deltas |
| F5 | Tool-Based Generative UI | Tool results rendered in the canvas pane (charts, data grids, forms) |
| F6 | Shared State | Bidirectional state sync (recipe editor) between client and server |
| F7 | Predictive State Updates | Progressive document preview via streaming tool arguments |

> **Note:** v1 described 6 conceptual "UX Patterns" (Reflection, Tool Use, Planning, Multi-Agent, Artifact Editing, Generative UI) that don't map 1:1 to the AG-UI feature taxonomy. v2 uses the 7-feature taxonomy from the AG-UI protocol, with 3 data-type sub-features (chart, data grid, form) classified under F5 (ISS-001).

### 2.3 Target User

A developer familiar with Blazor, MAF, and AG-UI who wants to understand how all features integrate in a real application. The spec assumes knowledge of Fluxor state management and the MAF `AIAgent`/`IChatClient` abstractions.

### 2.4 Non-Goals

- Multi-agent orchestration (future — MAF Workflow API exists but is not integrated)
- Session persistence beyond the Blazor circuit lifetime (in-memory MVP)
- Production security hardening (sample app)
- Conversation branching / DAG history

---

## 3. Architecture Overview

### 3.1 System Context

```
┌──────────────┐     SSE (AG-UI)     ┌──────────────────┐     LLM API      ┌──────────┐
│ AGUIDojoClient│◄──────────────────►│  AGUIDojoServer   │◄────────────────►│ OpenAI / │
│ (Blazor Server)│   POST /chat      │  (ASP.NET Core)   │                  │ Azure OAI│
│              │                     │                    │                  └──────────┘
│  Fluxor Store │                    │  Unified Agent     │
│  BB v3 UI     │                    │  (composed pipeline)│
│  SignalR circuit│                  │  8 tools registered │
└──────────────┘                     └──────────────────┘
       ▲                                      ▲
       │ Aspire Service Discovery              │ Aspire Orchestration
       └──────────── AGUIDojo.AppHost ─────────┘
```

### 3.2 Server-Side: Composed Agent Pipeline

The server exposes a **single** `POST /chat` endpoint serving all 7 AG-UI features through a composed wrapper pipeline. The MAF `AIAgentBuilder.Use()` method stacks 4 `DelegatingAIAgent` wrappers around a base `ChatClientAgent`:

```
Request → ① ServerFunctionApprovalAgent (outermost)
            → ② AgenticUIAgent
              → ③ PredictiveStateUpdatesAgent
                → ④ SharedStateAgent (innermost wrapper)
                  → ⑤ OpenTelemetry
                    → ⑥ ChatClientAgent (base)
                      → ⑦ ToolResultStreamingChatClient (IChatClient-level)
                        → ⑧ FunctionInvokingChatClient
                          → ⑨ ChatClient (LLM API)
```

The LLM's native tool selection acts as the router — no explicit routing logic. 8 tools with clear, non-overlapping descriptions guide the LLM to the right capability.

> **Full details:** See [design-sections/01-unified-endpoint.md](design-sections/01-unified-endpoint.md) §3 for ordering rationale; §4 for SharedStateAgent double-invocation handling; §5 for the complete tool catalog; §6 for the unified system prompt.

### 3.3 Client-Side: Session-Keyed Fluxor State

The client replaces 4 global Fluxor stores with a single `SessionManagerState` feature containing all sessions as an `ImmutableDictionary<string, SessionEntry>`. Components derive displayed data from `Sessions[ActiveSessionId]`.

```
SessionManagerState
├── ActiveSessionId: string?
├── SessionOrder: ImmutableList<string>
└── Sessions: ImmutableDictionary<string, SessionEntry>
    └── SessionEntry
        ├── Metadata: SessionMetadata (title, status, timestamps, unread)
        └── State: SessionState (messages, plan, recipe, document, artifacts)
```

### 3.4 Three-Pane Layout

```
┌────────────────────────────────────────────────────────────────┐
│ BbSidebarProvider                                              │
│ ┌──────────────┬─────────────────────────────────────────────┐ │
│ │ BbSidebar    │ BbSidebarInset                              │ │
│ │ (Session     │ ┌─ Inset Header (Session Title + Status) ──┐│ │
│ │  Sidebar)    │ │ DualPaneLayout                           ││ │
│ │              │ │ ┌─ContextPane──┐ ┌──CanvasPane──────────┐││ │
│ │ • New Chat   │ │ │ MessageList  │ │ Plan | Recipe | Doc  │││ │
│ │ • Session 1  │ │ │ ChatInput    │ │ Chart | Grid | Form  │││ │
│ │ • Session 2  │ │ └──────────────┘ └──────────────────────┘││ │
│ │ • ...        │ └──────────────────────────────────────────┘│ │
│ └──────────────┴─────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
```

| Pane | Default Width | Collapsible | Key Component |
|------|--------------|-------------|---------------|
| Session Sidebar | 280px | Yes → 48px icon-only | `BbSidebar` with `Collapsible="icon"` |
| Chat Pane | Flexible (fill) | No | `ContextPane` with `MessageList` + `ChatInput` |
| Canvas Pane | ~50% of inset | Yes (toggle) | `CanvasPane` with dynamic tabs per artifact type |

---

## 4. Unified Agent Endpoint

### 4.1 From 10 Endpoints to 1

The v1 architecture mapped 10 separate `MapAGUI` endpoints, each with a dedicated agent. This caused: client-side routing complexity (hardcoded endpoint list), feature isolation (no multi-feature conversations), duplicated configuration, and inconsistent wrapper application (ISS-003, ISS-004, ISS-051).

**v2 Target:** Single `POST /chat` → SSE response stream.

All 7 AG-UI features activate through one conversation pipeline. The LLM's tool selection routes to the right capability contextually.

### 4.2 Wrapper Composition Order

Four `DelegatingAIAgent` wrappers compose from outermost to innermost:

| Position | Wrapper | Intercepts | Rationale |
|----------|---------|-----------|-----------|
| ① Outermost | `ServerFunctionApprovalAgent` | `FunctionApprovalRequest/ResponseContent` | Must intercept approval events before inner wrappers process tool results |
| ② | `AgenticUIAgent` | `FunctionResultContent` from `create_plan`/`update_plan_step` | Emits `DataContent` plan state; must be outer to SharedStateAgent |
| ③ | `PredictiveStateUpdatesAgent` | `FunctionCallContent` for `write_document` | Tool-name-scoped; safe to compose with other wrappers |
| ④ Innermost | `SharedStateAgent` | `ag_ui_state` from `AdditionalProperties` | Double-invokes inner agent; placed innermost so double-invocation only affects base agent |

**SharedStateAgent Composition Safety:** SharedStateAgent runs the inner agent twice per request (state-update + summary). Outer wrappers use an invocation context flag (`ag_ui_shared_state_phase`) to skip interception during the `"state_update"` phase.

> **Full details:** See [design-sections/01-unified-endpoint.md](design-sections/01-unified-endpoint.md) §3–§4.

### 4.3 Unified Tool Registry (8 Tools)

| # | Tool | Category | Wrapper Interaction | HITL? |
|---|------|----------|-------------------|-------|
| 1 | `get_weather` | Data query | `ToolResultStreamingChatClient` | No |
| 2 | `send_email` | Action | `ServerFunctionApprovalAgent` | **Yes** |
| 3 | `write_document` | Content creation | `PredictiveStateUpdatesAgent` | No |
| 4 | `create_plan` | Planning | `AgenticUIAgent` | No |
| 5 | `update_plan_step` | Planning | `AgenticUIAgent` | No |
| 6 | `show_chart` | Visualization | `ToolResultStreamingChatClient` | No |
| 7 | `show_data_grid` | Visualization | `ToolResultStreamingChatClient` | No |
| 8 | `show_form` | UI generation | `ToolResultStreamingChatClient` | No |

Two features operate without explicit tools: F1 (Agentic Chat) is the base LLM response path; F6 (Shared State) is client-initiated via `ag_ui_state` in `AdditionalProperties`.

`ToolResultStreamingChatClient` is applied **globally** at the `IChatClient` level so all tool results stream to the AG-UI event layer (resolves ISS-007).

> **Full details:** See [design-sections/01-unified-endpoint.md](design-sections/01-unified-endpoint.md) §5–§7.

### 4.4 Unified System Prompt

The prompt follows a structured pattern (identity → tool routing guidance → feature-specific rules → formatting) and is ~250 tokens. It does NOT concatenate the 10 individual v1 prompts.

> **Full prompt text:** See [design-sections/01-unified-endpoint.md](design-sections/01-unified-endpoint.md) §6.2.

---

## 5. Multi-Session State Architecture

### 5.1 From 4 Global Stores to 1 Session-Keyed Store

v1 used 4 independent Fluxor features (`AgentState`, `ChatState`, `PlanState`, `ArtifactState`) as global singletons with zero session concept. v2 consolidates into a single `SessionManagerState` feature using the session-keyed dictionary pattern.

| Aspect | v1 | v2 |
|--------|-----|-----|
| Stores | 4 independent features | 1 `SessionManagerState` feature |
| Session concept | None | `SessionMetadata` + `SessionState` per session |
| Actions | Global (no session ID) | All carry `string SessionId` |
| Switching | Destroy + rebuild | Atomic `ActiveSessionId` pointer update |
| Background streaming | Impossible | Per-session `SessionStreamingContext` |

### 5.2 SessionState Record

`SessionState` consolidates all per-session data from the 4 former stores:

- **Chat:** `Messages`, `CurrentResponseMessage`, `ConversationId`, `StatefulMessageCount`, `PendingApproval`
- **Agent:** `IsRunning`, `CurrentAuthorName`
- **Plan:** `Plan`, `PlanDiff`
- **Artifacts:** `CurrentRecipe`, `CurrentDocumentState`, `IsDocumentPreview`, `DiffPreview`, `CurrentDataGrid`, `ActiveArtifactType`, `VisibleTabs`

**Removed field:** `SelectedEndpointPath` — no longer needed with unified endpoint.

### 5.3 SessionMetadata

Lightweight descriptor for sidebar display: `Id`, `Title`, `Status` (7-value enum), `CreatedAt`, `LastActivityAt`, `UnreadCount`, `HasPendingApproval`.

### 5.4 Global vs. Session-Scoped State

| Stays Global | Reason |
|-------------|--------|
| `ActiveSessionId` | Determines which session is displayed |
| `Sessions` dictionary | Container for all sessions |
| `SessionOrder` list | UI ordering for sidebar |
| UI preferences (theme) | User-level, not session-level |

### 5.5 AgentStreamingService Refactoring

`AgentStreamingService` manages a `Dictionary<string, SessionStreamingContext>` where each context holds: `CancellationTokenSource`, dedup `HashSet`s, `ChatOptions`, HITL `TaskCompletionSource`, diff preview state, and streaming message buffer — all per-session.

> **Full details:** See [design-sections/02-multi-session-state.md](design-sections/02-multi-session-state.md) §3–§10 for record definitions, field migration map, reducer patterns, and selector performance.

---

## 6. Session Lifecycle Management

### 6.1 Session Status Enum (7 values)

```
Created → Active → Streaming → Completed → Active (follow-up) → ...
                       ↓
                  Background → Completed
                       ↓
                     Error → Active (retry)
                     
Any → Archived (user deletes)
```

| Status | Sidebar Badge | Chat Area | Purpose |
|--------|--------------|-----------|---------|
| `Created` | Dim circle | Empty state prompt | Session exists but unused |
| `Active` | None | Idle chat input | Foreground, idle |
| `Streaming` | Animated pulse | Typing indicator + "Stop" button | Foreground, agent generating |
| `Background` | Spinner + unread count | N/A (viewing another session) | Background, agent generating |
| `Completed` | Checkmark | Idle chat input | Agent finished |
| `Error` | Red triangle | Error banner with retry | Stream failure |
| `Archived` | Not shown | N/A | Soft-deleted |

### 6.2 Key Transitions

| From | Trigger | To |
|------|---------|-----|
| — | "New Chat" click or cold start | `Created` |
| `Created` | First message sent | `Active` → `Streaming` |
| `Streaming` | User switches away | `Background` |
| `Streaming` | Agent finishes | `Completed` |
| `Background` | Agent finishes | `Completed` + `IncrementUnreadAction` |
| `Background` | Agent requests HITL | `Background` + notification toast |
| `Error` | User retries | `Active` → `Streaming` |
| Any | User deletes | `Archived` |

### 6.3 Title Generation (MVP)

Progressive: default "New Chat" → first-message truncation (first 50 chars of first user message). LLM-generated titles are a future enhancement.

### 6.4 Concurrent Stream Cap

Maximum 3 concurrent SSE streams recommended (HTTP/2 multiplexing resolves the HTTP/1.1 6-connection limit). Excess sessions queue until a stream slot frees.

> **Full details:** See [design-sections/03-session-lifecycle.md](design-sections/03-session-lifecycle.md) §1–§10 for the complete state machine, transition rules, creation flow, cold start behavior, and HITL-in-background handling.

---

## 7. Real-Time Sync & Push Notifications

### 7.1 Core Insight

Blazor Server already maintains a permanent SignalR connection per circuit. The push notification "infrastructure" is simply: **dispatch a Fluxor action from a background thread → component re-render → browser receives DOM diff via existing SignalR circuit**. Zero new infrastructure needed for MVP.

### 7.2 Four-Stage Pipeline

```
Stage 1: Background SSE stream per session (Task.Run)
Stage 2: Fluxor Dispatch (IDispatcher is thread-safe)
Stage 3: IState<T>.StateChanged fires on all subscribed components
Stage 4: Blazor DOM diff pushed via existing SignalR circuit
```

### 7.3 Notification Trigger Points

Notifications fire only on **significant state transitions** from background sessions:

| Event | Notification? | Toast Variant | Duration |
|-------|--------------|---------------|----------|
| Text token | ❌ | — | — |
| Tool call started/completed | ❌ | — | — |
| `FunctionApprovalRequestContent` | ✅ **ApprovalRequired** | Warning (amber, persistent) | Until dismissed |
| Stream completed (`RUN_FINISHED`) | ✅ **SessionCompleted** | Success (green) | 5s auto-dismiss |
| Stream error (`RUN_ERROR`) | ✅ **SessionError** | Destructive (red) | 8s auto-dismiss |

### 7.4 HITL Approval in Background

When a background session needs approval: pulsing amber badge in sidebar + persistent toast notification. Clicking the toast switches to that session where `PendingApproval` state is already set → approval dialog renders immediately.

> **Full details:** See [design-sections/04-realtime-sync-notifications.md](design-sections/04-realtime-sync-notifications.md) §1–§9 for the notification data model, BB v3 toast integration, concurrent stream management, and sequence diagrams.

---

## 8. Chat UI & Session Switcher

### 8.1 Session Sidebar

Replaces the v1 endpoint selector with a session-focused sidebar using BB v3's `BbSidebar` component:

```
BbSidebar
├── BbSidebarHeader (brand + search trigger)
├── BbSidebarContent
│   └── BbSidebarGroup ("Sessions")
│       └── BbSidebarMenu → SessionListItem per session
└── BbSidebarFooter ("New Chat" + theme toggle)
```

Sessions ordered by reverse-chronological `LastActivityAt`. Search via `BbCommandDialog` (Cmd+K).

### 8.2 Session List Item

Each session displays: status icon (Lucide 16px) | title (truncated) | relative timestamp | unread badge | approval badge.

| State | Status Icon | Badges |
|-------|------------|--------|
| Active (current) | `circle` (primary) | None (highlighted background) |
| Streaming (background) | `loader` (spinning, muted) | Unread count |
| Completed | `check-circle` (green) | Unread count |
| Error | `alert-triangle` (red) | Error badge |
| Needs Approval | `bell` (pulsing amber) | "!" destructive badge |

### 8.3 ChatHeader → Inset Header

The v1 `ChatHeader` (which was the sidebar) is replaced. The inset header now shows: `BbSidebarTrigger` | active session title | agent status (streaming indicator or idle).

### 8.4 Data Binding Change

All components switch from global selectors to session-keyed selectors:

| Component | v1 Source | v2 Source |
|-----------|-----------|-----------|
| Message list | `ChatStore.Value.Messages` | `Sessions[ActiveSessionId].State.Messages` |
| Plan display | `PlanStore.Value.Plan` | `Sessions[ActiveSessionId].State.Plan` |
| Recipe editor | `ArtifactStore.Value.CurrentRecipe` | `Sessions[ActiveSessionId].State.CurrentRecipe` |
| Canvas tabs | `ArtifactStore.Value.VisibleTabs` | `Sessions[ActiveSessionId].State.VisibleTabs` |

> **Full details:** See [design-sections/05-chat-ui-session-switcher.md](design-sections/05-chat-ui-session-switcher.md) §1–§12 for the complete component hierarchy, wireframe, responsive behavior, and keyboard shortcuts.

---

## 9. AG-UI Feature Integration Matrix

### 9.1 Primary Quick-Reference

This matrix shows how each AG-UI feature integrates with the unified pipeline:

| Feature | Tool(s) | Wrapper | DataContent Type | Canvas Component | Fluxor Target |
|---------|---------|---------|-----------------|-----------------|---------------|
| F1: Agentic Chat | *(none)* | None | None | None (inline) | `Messages` |
| F2: Backend Tool Render | `get_weather` | `ToolResultStreamingChatClient` | None | `WeatherInfo` (inline) | `Messages` |
| F3: HITL | `send_email` | `ServerFunctionApprovalAgent` | None | `ApprovalDialog` (modal) | `PendingApproval` |
| F4: Agentic Gen UI | `create_plan`, `update_plan_step` | `AgenticUIAgent` | `application/json`, `application/json-patch+json` | `PlanDisplay` (tab) | `Plan`, `PlanDiff` |
| F5: Tool-Based Gen UI | `show_chart`, `show_data_grid`, `show_form` | `ToolResultStreamingChatClient` | None | `ChartDisplay`, `DataTable`, `DynamicForm` (tabs) | `CurrentDataGrid`, `VisibleTabs` |
| F6: Shared State | *(ag_ui_state)* | `SharedStateAgent` | `application/json` | `RecipeEditor` (tab) | `CurrentRecipe` |
| F7: Predictive State | `write_document` | `PredictiveStateUpdatesAgent` | `application/json` | `DocumentEditor`/`DiffPreview` (tab) | `CurrentDocumentState`, `DiffPreview` |

### 9.2 DataContent Disambiguation

Three wrappers emit `DataContent` with `application/json`. The v2 strategy uses a **typed envelope convention** with `$type` discriminator:

```json
{ "$type": "plan_snapshot",     "data": { "steps": [...] } }
{ "$type": "recipe_snapshot",   "data": { "title": "...", "ingredients": [...] } }
{ "$type": "document_preview",  "data": { "document": "..." } }
```

Client routing: parse `$type` → dispatch to correct Fluxor store. JSON structure heuristics retained as fallback for backward compatibility.

### 9.3 Multi-Feature Coexistence

All feature pairs are compatible except same-type exclusivity (one plan per session, one recipe per session). Key conditional: SharedStateAgent's double-invocation requires phase-awareness in outer wrappers.

### 9.4 Canvas Tab Management

Tabs are dynamically added when a feature first produces output and display in the canvas pane. The `ActiveArtifactType` field determines which tab is visible; `VisibleTabs` tracks the set of available tabs.

> **Full details:** See [design-sections/06-agui-feature-integration.md](design-sections/06-agui-feature-integration.md) §2–§10 for feature interaction sequences, conflict resolution strategies, and the complete tool catalog with LLM-visible descriptions.

---

## 10. BB v3 Component Guide

### 10.1 Migration Overview (6 Breaking Changes)

| # | Change | Severity | Scope |
|---|--------|----------|-------|
| M1 | `Bb` prefix on all components | High | All ~130 component tags across ~20 `.razor` files |
| M2 | Namespace flattening (15 imports → 2) | High | `_Imports.razor` |
| M3 | ApexCharts → ECharts | High | `ChartDisplay.razor`, chart models |
| M4 | Two-layer portal (`BbContainerPortalHost` + `BbOverlayPortalHost`) | Medium | `App.razor` |
| M5 | Input `UpdateTiming` default: `Immediate` → `OnChange` | Medium | `ChatInput.razor` (add explicit `UpdateTiming.Immediate`) |
| M6 | DI registration consolidation | Low | `Program.cs` |

### 10.2 Key BB v3 API Mappings for Session UX

| UI Element | BB v3 Component | Key Parameters |
|------------|----------------|----------------|
| Session sidebar | `BbSidebar` | `Collapsible="icon"` |
| Session item | `BbSidebarMenuButton` | `IsActive="@isActive"` |
| Session search | `BbCommandDialog` | `@bind-Open`, keyboard `Ctrl+K` |
| Unread badge | `BbBadge` | `Variant="BadgeVariant.Secondary"` |
| Approval badge | `BbBadge` | `Variant="BadgeVariant.Destructive"`, `animate-pulse` |
| Session delete | `BbAlertDialog` | Confirmation dialog |
| Toast notifications | `BbToast` via `ToastService` | Success / Warning / Destructive variants |
| Status icons | `BbLucideIcon` | `Name`, `Size="16"` |

### 10.3 New v2 _Imports.razor (BB Section)

```razor
@using BlazorBlueprint.Components
@using BlazorBlueprint.Primitives
@using BlazorBlueprint.Icons.Lucide
@using BlazorBlueprint.Icons.Lucide.Components
```

### 10.4 Scoped CSS Gotchas

1. BB components as root elements do NOT receive Blazor's `b-XXXX` scope attribute — wrap in a plain `<div>` for scoped CSS targeting
2. `BbScrollArea` renders `display: table` internally — breaks CSS Grid containment; use plain `<div style="overflow-y: auto">` in grid layouts
3. `BbTabs` renders an intermediate `<div>` with no class — target with `::deep .class > div` for flex propagation

> **Full details:** See [design-sections/07-bb-v3-component-mapping.md](design-sections/07-bb-v3-component-mapping.md) §2–§11 for the complete v2→v3 migration checklist, chart migration details, portal strategy, and DI registration changes.

---

## 11. Migration Guide

### 11.1 v1 → v2 Section Mapping

| v1 Section | v2 Section | What Changed |
|------------|-----------|-------------|
| §1 Introduction (6 pillars) | §2 Overview & Goals | Reframed as 7 AG-UI features; removed multi-agent pillar |
| §2 Architecture (10 endpoints) | §3–§4 Architecture + Unified Endpoint | Redesigned from scratch: single endpoint, composed wrappers |
| §3 AG-UI Protocol | §9 Feature Integration Matrix | Expanded with full tool catalog, disambiguation strategy |
| §4 Server-Side | §4 Unified Agent Endpoint | Complete rewrite: builder composition, global ToolResultStreaming |
| §5 Client-Side (AGUIChatClient) | §5 Multi-Session State | Rewritten: factory simplification, session-keyed state |
| §6 Dual-Pane Layout | §8 Chat UI (3-pane layout) | Extended to 3-pane with session sidebar |
| §7 Visual Design System | §10 BB v3 Component Guide | Updated for BB v3 APIs |
| §8–§13 UX Patterns I–VI | §9 Feature Integration Matrix | Consolidated into integration matrix; removed fictional sections |
| §14 State Management | §5–§6 Multi-Session State + Lifecycle | Complete redesign: session-keyed Fluxor, 7-status lifecycle |
| §15 Observability | Not in scope (unchanged) | Aspire + OTel approach retained |
| §16 Mobile | §8.4 Responsive (brief) | Session sidebar responsive behavior added |
| §18 Implementation Guide | *Distributed across §4–§10* | Real code samples replace fictional ones |

### 11.2 Server-Side Migration Steps

| Step | Change | Risk |
|------|--------|------|
| 1 | Create `CreateUnifiedAgent()` in factory | Low (additive) |
| 2 | Apply `ToolResultStreamingChatClient` globally | Low |
| 3 | Add invocation context flag to `SharedStateAgent` | Medium |
| 4 | Add phase-awareness to outer wrappers | Medium |
| 5 | Compose all wrappers via `AIAgentBuilder` | Low |
| 6 | Map `POST /chat` → unified agent | Low |
| 7 | Remove 10 legacy `MapAGUI` calls | Medium (breaking) |

### 11.3 Client-Side Migration Steps

| Step | Change | Risk |
|------|--------|------|
| 1 | BB v2 → v3 migration (all 6 categories) | High — bulk changes |
| 2 | Create `SessionManagerState` Fluxor feature | Medium |
| 3 | Create `SessionState`, `SessionMetadata` records | Low |
| 4 | Migrate reducers to session-keyed pattern | Medium |
| 5 | Refactor `AgentStreamingService` to per-session contexts | High |
| 6 | Build session sidebar UI | Medium |
| 7 | Wire notification toast system | Low |
| 8 | Simplify `AGUIChatClientFactory` to single endpoint | Low |
| 9 | Remove endpoint selector UI + `SelectedEndpointPath` | Low |

---

## 12. Critique Resolution Matrix

This matrix maps every ISS-* issue from the [spec critique](spec-critique.md) to its resolution in v2.

### Critical Issues (12)

| Issue | Summary | Resolution | v2 Section |
|-------|---------|-----------|-----------|
| ISS-003 | No unified endpoint design | Fully designed: single `/chat` with composed wrappers | §4 |
| ISS-004 | No wrapper composition | Builder pattern with 4 stacked wrappers | §4.2 |
| ISS-030 | Fictional Fluxor state definitions | Real `SessionState` record matching codebase | §5.2 |
| ISS-031 | No multi-session state design | Session-keyed `SessionManagerState` | §5 |
| ISS-032 | Fictional DAG branching | Removed entirely | §2.4 (Non-Goals) |
| ISS-037 | Outdated package references | BB v3 + correct package names | §10 |
| ISS-038 | Outdated DI registration | BB v3 `AddBlazorBlueprintComponents()` | §10.1 M6 |
| ISS-039 | Fictional `AgentService` class | Replaced with real `AgentStreamingService` | §5.5 |
| ISS-040 | Fictional `ChatNode` and `AgentStatus` | Replaced with real `SessionState` and `SessionStatus` | §5–§6 |
| ISS-042 | No push notification architecture | Fluxor + SignalR circuit piggybacking | §7 |
| ISS-043 | No multi-session architecture | Full design: state, lifecycle, streaming | §5–§6 |
| ISS-044 | No session lifecycle design | 7-status lifecycle with transitions | §6 |

### Major Issues (17)

| Issue | Summary | Resolution | v2 Section |
|-------|---------|-----------|-----------|
| ISS-005 | BB v2 descriptions | Updated to BB v3 | §10 |
| ISS-007 | Missing `FunctionResultContent` | `ToolResultStreamingChatClient` applied globally | §4.3 |
| ISS-008 | Fragile DataContent disambiguation | Typed envelope with `$type` discriminator | §9.2 |
| ISS-009 | Fictional factory code | Real `ChatClientAgentFactory` pattern | §4 |
| ISS-010 | No tool catalog | 8-tool registry with descriptions | §4.3 |
| ISS-011 | Missing HITL experimental status note | Noted `#pragma warning disable MEAI001` | §4 (implicit) |
| ISS-012 | Outdated client DI | Simplified factory with unified endpoint | §5.5 |
| ISS-013 | Missing `AgentStreamingService` detail | Session-scoped contexts | §5.5 |
| ISS-017 | BB v2 code in §8 | All code uses BB v3 APIs | §10 |
| ISS-019 | BB v2 code in §9 | All code uses BB v3 APIs | §10 |
| ISS-020 | No background HITL design | Notification toast + approval dialog on switch | §7.4 |
| ISS-021 | BB v2 code in §10 | All code uses BB v3 APIs | §10 |
| ISS-022 | Missing plan data flow | Feature integration matrix | §9.1 |
| ISS-023 | Fictional multi-agent section | Removed entirely | §2.4 |
| ISS-029 | Conflated generative UI types | Split into F4 (agentic) and F5 (tool-based) | §9.1 |
| ISS-045 | All code samples use BB v2 | BB v3 throughout | §10 |
| ISS-050 | No `AgentStreamingService` spec | Session-keyed contexts | §5.5 |

### Minor Issues (13)

| Issue | Summary | Resolution |
|-------|---------|-----------|
| ISS-001 | 6 patterns vs 7 features | Reframed as 7 AG-UI features (§2.2) |
| ISS-002 | Multi-agent implied | Listed as non-goal (§2.4) |
| ISS-006 | Concurrent SSE limits | 3-stream cap recommendation (§6.4) |
| ISS-014 | Competitor comparisons | Removed (§11.1) |
| ISS-015 | Missing sidebar in layout | 3-pane layout with sidebar (§3.4) |
| ISS-016 | Missing notification styles | Toast variants specified (§7.3) |
| ISS-018 | BB v2 component tree | BB v3 throughout (§10) |
| ISS-025 | Missing PredictiveState flow | Feature matrix (§9.1 F7) |
| ISS-026 | Streaming diff not described | F7 progressive preview (§9.1) |
| ISS-028 | BB v2 in RenderTreeBuilder | BB v3 (§10) |
| ISS-035 | No responsive sidebar | 3-pane responsive behavior (§8) |
| ISS-036 | Overstated conclusion | Honest feature status (§2.4) |
| ISS-041 | Missing references | Design section cross-references throughout |

### Remaining Issues (addressed by scope)

| Issue | Summary | Status |
|-------|---------|--------|
| ISS-024 | BB v2 in multi-agent section | Section removed (ISS-023) |
| ISS-027 | Wrong tool names in registry | Corrected tool catalog (§4.3) |
| ISS-033 | Oversimplified STATE_SNAPSHOT | Feature matrix + disambiguation (§9) |
| ISS-034 | Missing Aspire MCP | Out of scope; Aspire orchestration retained |
| ISS-046 | BB namespace imports | New imports specified (§10.3) |
| ISS-047 | ApexCharts obsolete | ECharts migration (§10.1 M3) |
| ISS-048 | BB v3 portal architecture | Two-layer portals (§10.1 M4) |
| ISS-049 | Redundant `AsChild="true"` | Minor cleanup; noted in BB v3 guide |
| ISS-051 | No client factory spec | Simplified factory (§5.5) |
| ISS-052 | No chat input spec | Chat input described in UI section (§8) |

---

## 13. Open Questions & Future Work

### 13.1 Open Questions

| ID | Question | Current Decision | Revisit When |
|----|----------|-----------------|-------------|
| OQ-1 | Should session persistence use localStorage or server-side storage? | In-memory MVP (lost on circuit disconnect) | When users need cross-session continuity |
| OQ-2 | Should titles be LLM-generated? | First-message truncation for MVP | After core multi-session is stable |
| OQ-3 | Should the unified prompt be auto-generated from tool metadata? | Hand-crafted for MVP | When tool count exceeds 15 |
| OQ-4 | Should DataContent `$type` use vendor media types instead of envelopes? | Envelope wrapper (`$type` field) | If AG-UI transport adds native media type support |
| OQ-5 | Should SharedStateAgent be refactored to single invocation? | Keep two-invocation with phase flag | If prompt engineering reliably yields both JSON + prose |

### 13.2 Future Work

| Item | Priority | Description |
|------|----------|-------------|
| Multi-agent orchestration | Medium | Integrate MAF Workflow API for agent handoffs |
| Session persistence | Medium | Survive circuit disconnects via server-side storage |
| Conversation branching | Low | DAG-based history with branch/merge UX |
| Browser push notifications | Low | Dedicated SignalR hub + Notification API for cross-tab alerts |
| LLM-generated titles | Low | Post-first-response auto-summarization |
| Session sharing/export | Low | Export conversation as markdown/JSON |
| Advanced search | Low | Full-text search across session messages |

---

*This spec is the authoritative design document for AG-UI Dojo v2. Detailed designs for each section are available in the [design-sections/](design-sections/) directory. Implementation should proceed section-by-section following the migration guide (§11).*
