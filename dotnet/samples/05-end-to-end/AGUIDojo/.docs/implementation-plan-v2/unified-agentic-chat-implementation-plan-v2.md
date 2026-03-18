# Implementation Plan — Unified Agentic Chat v2

> **Version:** 1.0  
> **Date:** 2026-03-17  
> **Status:** Ready for Implementation  
> **Spec:** [Unified Agentic Chat Spec v2.md](Unified%20Agentic%20Chat%20Spec%20v2.md) (v2.1)  
> **Design Sections:** [design-sections/](design-sections/) (7 documents, 5,821 lines)  
> **Supersedes:** All prior task-* references in design section headers

---

## Table of Contents

1. [Scope & Goals](#1-scope--goals)
2. [Architecture Delta](#2-architecture-delta)
3. [Phase Overview](#3-phase-overview)
4. [Dependency Graph](#4-dependency-graph)
5. [Phase 0 — Prerequisites](#5-phase-0--prerequisites)
6. [Phase 1 — BB v3 Migration](#6-phase-1--bb-v3-migration)
7. [Phase 2 — Unified Server Endpoint](#7-phase-2--unified-server-endpoint)
8. [Phase 3 — DataContent $type Envelope](#8-phase-3--datacontent-type-envelope)
9. [Phase 4 — Fluxor Multi-Session State](#9-phase-4--fluxor-multi-session-state)
10. [Phase 5 — AgentStreamingService Refactoring](#10-phase-5--agentstreamingservice-refactoring)
11. [Phase 6 — Session Lifecycle](#11-phase-6--session-lifecycle)
12. [Phase 7 — Push Notifications](#12-phase-7--push-notifications)
13. [Phase 8 — Session Sidebar UI](#13-phase-8--session-sidebar-ui)
14. [Phase 9 — Integration & Legacy Removal](#14-phase-9--integration--legacy-removal)
15. [Phase 10 — Polish & Verification](#15-phase-10--polish--verification)
16. [File Inventory](#16-file-inventory)
17. [Risk Matrix](#17-risk-matrix)
18. [Open Questions (Deferred)](#18-open-questions-deferred)
19. [Spec Cross-Reference](#19-spec-cross-reference)

---

## 1. Scope & Goals

This plan implements the **five key changes** from the spec (§1):

| # | Change | Summary |
|---|--------|---------|
| 1 | Unified Endpoint | 10 `MapAGUI` endpoints → single `POST /chat` with composed wrapper pipeline |
| 2 | Multi-Session State | 4 global Fluxor stores → 1 session-keyed `SessionManagerState` |
| 3 | Push Notifications | Zero notification architecture → Fluxor dispatch via Blazor SignalR circuit |
| 4 | BB v3 Migration | BB v2.1.1 → v3 (`Bb` prefix, flat namespaces, ECharts, two-layer portals) |
| 5 | Codebase Alignment | Fictional types removed; real types throughout |

### Non-Goals (Spec §2.4)

- Multi-agent orchestration (future — MAF Workflow API)
- Session persistence beyond Blazor circuit lifetime (in-memory MVP)
- Production security hardening (sample app)
- Conversation branching / DAG history

---

## 2. Architecture Delta

### Before (v1 — Current)

```
AGUIDojoServer (Program.cs, 488 lines):
  10 × MapAGUI → 10 dedicated AIAgent instances
  Each with 0–1 DelegatingAIAgent wrapper
  ToolResultStreamingChatClient on 1/10 agents only

AGUIDojoClient:
  4 Fluxor stores: AgentState, ChatState, PlanState, ArtifactState (16 files)
  Single-session only; no background streaming
  AgentStreamingService: per-circuit singleton state (572 lines)
  ~130 BB v2.1.1 component tags, 15 sub-namespace imports
  Endpoint selector sidebar with 10 items
```

### After (v2 — Target)

```
AGUIDojoServer:
  1 × MapAGUI("/chat") → 1 unified AIAgent
  4 composed DelegatingAIAgent wrappers:
    ① ServerFunctionApprovalAgent (outermost)
    ② AgenticUIAgent
    ③ PredictiveStateUpdatesAgent
    ④ SharedStateAgent (innermost wrapper)
  ToolResultStreamingChatClient applied globally (IChatClient level)
  8 registered tools, ~250-token unified system prompt

AGUIDojoClient:
  1 Fluxor store: SessionManagerState (~10 new files)
    └─ ImmutableDictionary<string, SessionEntry>
  Multi-session: up to 20 sessions, 3 concurrent SSE streams
  7-status session lifecycle with background streaming
  BB v3 components, flat namespace (2–4 imports)
  Session sidebar + session list items
  Push notifications via toast (3 trigger types)
  DataContent routing via $type envelope
```

### System Context (Spec §3.1)

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

### Composed Agent Pipeline (Spec §3.2)

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

### Three-Pane Layout (Spec §3.4)

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

### Session-Keyed State Shape (Spec §3.3)

```
SessionManagerState
├── ActiveSessionId: string?
├── SessionOrder: ImmutableList<string>
└── Sessions: ImmutableDictionary<string, SessionEntry>
    └── SessionEntry
        ├── Metadata: SessionMetadata (title, status, timestamps, unread)
        └── State: SessionState (messages, plan, recipe, document, artifacts)
```

---

## 3. Phase Overview

| Phase | Name | Risk | Effort | Parallelizable With |
|-------|------|------|--------|---------------------|
| **0** | Prerequisites | Low | XS | — |
| **1** | BB v3 Migration | High | M–L | Phase 2 |
| **2** | Unified Server Endpoint | Medium | M | Phase 1 |
| **3** | DataContent `$type` Envelope | Medium | S | — |
| **4** | Fluxor Multi-Session State | **High** | **L** | — |
| **5** | AgentStreamingService Refactoring | High | M–L | — |
| **6** | Session Lifecycle | Medium | M | — |
| **7** | Push Notifications | Low–Med | S–M | Phase 8 |
| **8** | Session Sidebar UI | Medium | M | Phase 7 |
| **9** | Integration & Legacy Removal | Medium | M | — |
| **10** | Polish & Verification | Low | S | — |

**Effort legend:** XS = hours, S = 1–2 days, M = 2–4 days, L = 4+ days

---

## 4. Dependency Graph

```
Phase 0 (Prerequisites)
  ├──► Phase 1 (BB v3 Migration) ──────────────────────┐
  └──► Phase 2 (Unified Server Endpoint) ──┐            │
                                            │            │
                                            ▼            │
                                     Phase 3 ($type)     │
                                            │            │
                                            │    ┌───────┘
                                            │    ▼
                                            │  Phase 4 (Fluxor Multi-Session)
                                            │    │
                                            ▼    ▼
                                     Phase 5 (AgentStreamingService)
                                            │
                                            ▼
                                     Phase 6 (Session Lifecycle)
                                            │
                                   ┌────────┼────────┐
                                   ▼        │        ▼
                            Phase 7         │  Phase 8 (UI)
                         (Notifications)    │
                                   │        │        │
                                   └────────┼────────┘
                                            ▼
                                     Phase 9 (Integration)
                                            │
                                            ▼
                                     Phase 10 (Polish)
```

**Parallelizable pairs:**
- **Phase 1** (client BB v3) ∥ **Phase 2** (server unified endpoint) — no shared files
- **Phase 7** (notifications) ∥ **Phase 8** (session sidebar UI) — different subsystems

---

## 5. Phase 0 — Prerequisites

**Goal:** Verify green build, package availability, and establish baseline.

| ID | Task | Verification |
|----|------|-------------|
| P0-1 | `dotnet build AGUIDojo.slnx` passes | Exit code 0 |
| P0-2 | `dotnet test AGUIDojoServer.Tests` passes | All tests green |
| P0-3 | Record current BB version from `Directory.Packages.props` | Currently `2.1.1` |
| P0-4 | Verify BB v3 NuGet availability | `BlazorBlueprint.Components` ≥ 3.0.0 found |
| P0-5 | Confirm `Fluxor.Blazor.Web` already in client `.csproj` | Present (confirmed) |
| P0-6 | Confirm `BlazorMonaco` already in client `.csproj` | Present (confirmed) |

**Deliverable:** Green build baseline. Known package versions.

---

## 6. Phase 1 — BB v3 Migration

> **Design Section:** [07-bb-v3-component-mapping.md](design-sections/07-bb-v3-component-mapping.md)  
> **Spec Section:** §10  
> **Risk:** High (bulk changes across ~20 files)  
> **Rationale:** "BB v3 migration should be done FIRST because new spec code written against BB v2 APIs is immediately obsolete" (design-sections/07 §1)

### 6 Breaking Changes (Spec §10.1)

| # | Change | Severity | Scope |
|---|--------|----------|-------|
| M1 | `Bb` prefix on all components | High | All ~130 tags across ~20 `.razor` files |
| M2 | Namespace flattening (15 imports → 2–4) | High | `_Imports.razor` |
| M3 | ApexCharts → ECharts | High | `ChartDisplay.razor`, chart models |
| M4 | Two-layer portal (`BbContainerPortalHost` + `BbOverlayPortalHost`) | Medium | `App.razor` |
| M5 | Input `UpdateTiming` default: `Immediate` → `OnChange` | Medium | `ChatInput.razor` |
| M6 | DI registration consolidation | Low | `Program.cs` |

### Tasks

| ID | Task | Files | Change |
|----|------|-------|--------|
| P1-1 | Update BB packages to v3 in `Directory.Packages.props` | `Directory.Packages.props` | M6 |
| P1-2 | Verify `AddBlazorBlueprintComponents()` DI call | `AGUIDojoClient/Program.cs` | M6 |
| P1-3 | Flatten `_Imports.razor`: 15 BB imports → 2–4 | `_Imports.razor` | M2 |

**New `_Imports.razor` BB section (Spec §10.3):**
```razor
@using BlazorBlueprint.Components
@using BlazorBlueprint.Primitives
@using BlazorBlueprint.Icons.Lucide
@using BlazorBlueprint.Icons.Lucide.Components
```

| ID | Task | Files | Change |
|----|------|-------|--------|
| P1-4 | Bulk `Bb` prefix: `<Component>` → `<BbComponent>` across all `.razor` files | All ~20 `.razor` files | M1 |
| P1-5 | Update `typeof()` / `nameof()` references in `.cs` files | `ToolComponentRegistry.cs`, etc. | M1 |
| P1-6 | Replace `<PortalHost/>` with two-layer portals | `App.razor` or `MainLayout.razor` | M4 |

**Portal replacement (Spec §10.1 M4):**
```razor
<BbContainerPortalHost />
<BbOverlayPortalHost />
```

| ID | Task | Files | Change |
|----|------|-------|--------|
| P1-7 | Add `UpdateTiming="UpdateTiming.Immediate"` to `<BbInput>` in ChatInput | `ChatInput.razor` | M5 |
| P1-8 | Migrate `ChartDisplay.razor` from ApexCharts → ECharts | `GenerativeUI/ChartDisplay.razor` | M3 |
| P1-9 | Update icon references if BB v3 prefixes icons (`LucideIcon` → `BbLucideIcon`) | All icon usages | M1 |
| P1-10 | Build and test; fix compilation errors | — | — |

### Gotchas (Spec §10.4)

1. BB components as root elements do **not** receive Blazor's `b-XXXX` scope attribute → wrap in plain `<div>` for scoped CSS targeting
2. `BbScrollArea` renders `display: table` internally → use `<div style="overflow-y: auto">` in grid layouts
3. `BbTabs` renders intermediate `<div>` with no class → target with `::deep .class > div` for flex propagation

### Acceptance Criteria

- [ ] `dotnet build` passes with BB v3 packages
- [ ] All existing features render correctly with `Bb`-prefixed components
- [ ] Charts render with ECharts (if chart feature is exercised)
- [ ] Portal overlays (dialogs, tooltips, dropdowns) render correctly

---

## 7. Phase 2 — Unified Server Endpoint

> **Design Section:** [01-unified-endpoint.md](design-sections/01-unified-endpoint.md)  
> **Spec Section:** §4  
> **Risk:** Medium  
> **Can run in parallel with Phase 1**

### Wrapper Composition Order (Spec §4.2)

| Position | Wrapper | Intercepts | Rationale |
|----------|---------|-----------|-----------|
| ① Outermost | `ServerFunctionApprovalAgent` | `FunctionApprovalRequest/ResponseContent` | Must intercept approval events before inner wrappers process tool results |
| ② | `AgenticUIAgent` | `FunctionResultContent` from `create_plan`/`update_plan_step` | Must be outer to SharedStateAgent to avoid double-invocation seeing plan events twice |
| ③ | `PredictiveStateUpdatesAgent` | `FunctionCallContent` for `write_document` | Tool-name-scoped; safe to compose |
| ④ Innermost | `SharedStateAgent` | `ag_ui_state` from `AdditionalProperties` | Double-invokes inner agent; placed innermost so double-invocation only affects base agent |

### 8-Tool Registry (Spec §4.3)

| # | Tool | Category | HITL? | Wrapper Interaction |
|---|------|----------|-------|---------------------|
| 1 | `get_weather` | Data query | No | `ToolResultStreamingChatClient` |
| 2 | `send_email` | Action | **Yes** | `ServerFunctionApprovalAgent` |
| 3 | `write_document` | Content creation | No | `PredictiveStateUpdatesAgent` |
| 4 | `create_plan` | Planning | No | `AgenticUIAgent` |
| 5 | `update_plan_step` | Planning | No | `AgenticUIAgent` |
| 6 | `show_chart` | Visualization | No | `ToolResultStreamingChatClient` |
| 7 | `show_data_grid` | Visualization | No | `ToolResultStreamingChatClient` |
| 8 | `show_form` | UI generation | No | `ToolResultStreamingChatClient` |

### Tasks

| ID | Task | Files |
|----|------|-------|
| P2-1 | Create `CreateUnifiedAgent()` in `ChatClientAgentFactory` | `ChatClientAgentFactory.cs` |
| P2-2 | Register all 8 tools in unified tool array (see 01-unified §5.2) | `ChatClientAgentFactory.cs` |
| P2-3 | Apply `ToolResultStreamingChatClient` globally at IChatClient level | `ChatClientAgentFactory.cs` |
| P2-4 | Write unified system prompt (~250 tokens) | `ChatClientAgentFactory.cs` |
| P2-5 | Add invocation context flag to `SharedStateAgent` | `SharedState/SharedStateAgent.cs` |
| P2-6 | Add phase-awareness to 3 outer wrappers | `ServerFunctionApprovalAgent.cs`, `AgenticUIAgent.cs`, `PredictiveStateUpdatesAgent.cs` |
| P2-7 | Compose wrappers via `AIAgentBuilder` | `ChatClientAgentFactory.cs` |
| P2-8 | Map `POST /chat` → unified agent | `Program.cs` (server) |
| P2-9 | Keep legacy endpoints temporarily (parallel testing) | `Program.cs` (server) |

### Builder Composition Code (01-unified §3.3)

```csharp
// IChatClient pipeline
IChatClient wrappedClient = new ToolResultStreamingChatClient(chatClient.AsIChatClient());

// Base agent with ALL tools and unified system prompt
AIAgent baseAgent = wrappedClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "UnifiedAgent",
    Description = "Unified agentic chat agent with all AG-UI capabilities",
    ChatOptions = new ChatOptions
    {
        Instructions = unifiedSystemPrompt,
        Tools = allTools
    }
});

// Wrapper composition: first Use() = outermost wrapper at runtime
AIAgent unifiedAgent = baseAgent
    .AsBuilder()
    .Use(inner => new ServerFunctionApprovalAgent(inner, jsonOptions)) // ① outermost
    .Use(inner => new AgenticUIAgent(inner, jsonOptions))             // ②
    .Use(inner => new PredictiveStateUpdatesAgent(inner, jsonOptions)) // ③
    .Use(inner => new SharedStateAgent(inner, jsonOptions))           // ④ innermost
    .UseOpenTelemetry(SourceName)
    .Build();

app.MapAGUI("/chat", unifiedAgent);
```

### SharedStateAgent Phase-Awareness Contract (Spec §4.2)

SharedStateAgent runs the inner agent **twice** per request. To prevent outer wrappers from processing both invocations:

1. `SharedStateAgent` sets `AdditionalProperties["ag_ui_shared_state_phase"] = "state_update"` on the first invocation
2. All outer wrappers (①–③) **MUST** check for this flag and pass through without interception when `phase == "state_update"`
3. The flag exists only on `ChatOptions`, never serialized to the AG-UI event stream
4. Any new wrapper added to the pipeline MUST implement phase-awareness or be placed below ④

### Phase-Awareness Guard Pattern

```csharp
// In each outer wrapper's RunStreamingAsync:
if (options?.AdditionalProperties?.TryGetValue("ag_ui_shared_state_phase", out var phase) == true
    && phase?.ToString() == "state_update")
{
    // Pass through — don't intercept during SharedStateAgent's state-update invocation
    await foreach (var update in InnerAgent.RunStreamingAsync(messages, options, ct))
        yield return update;
    yield break;
}
// ... normal interception logic below
```

### Unified System Prompt (01-unified §6.2, ~250 tokens)

```text
You are a versatile AI assistant that helps users with conversations, data queries,
document editing, planning, and data visualization. Format responses in markdown.

## Tool Usage Guidelines

Choose tools based on user intent:
- **Email**: Use `send_email` when asked to send an email. The tool will prompt the user
  for approval automatically — do NOT ask for confirmation in chat.
- **Documents**: Use `write_document` for writing or editing content. Always write the full
  document in markdown. Keep content concise.
  Do NOT use italic or strike-through formatting in documents.
- **Planning**: Use `create_plan` to start a new plan, then `update_plan_step` to progress
  each step. Only one plan can be active at a time. Do NOT summarize the plan in chat —
  the UI renders it automatically. Continue updating steps until all are completed.
- **Visualization**: Use `show_chart` for trends/comparisons/distributions, `show_data_grid`
  for tabular data, `show_form` for user input collection.
- **Weather**: Use `get_weather` when asked about weather conditions.
- **General chat**: For questions that don't require tools, respond conversationally.

## Rules
- When planning, use tools only without additional chat messages.
- After tool execution, provide a brief summary of the result.
- Do NOT repeat tool output in your text response — the UI renders tool results directly.
```

### HITL Warning (Spec §4.2)

`ServerFunctionApprovalAgent` depends on `FunctionApprovalRequestContent` and `ApprovalRequiredAIFunction` — **evaluation-only types** requiring `#pragma warning disable MEAI001`. They may change before GA. Isolate HITL code behind abstractions for easier migration.

### Acceptance Criteria

- [ ] Single `/chat` endpoint serves all 7 AG-UI features
- [ ] Each feature works through the unified pipeline (manual test)
- [ ] SharedStateAgent double-invocation doesn't cause duplicate events in outer wrappers
- [ ] Legacy endpoints still work (parallel operation)

---

## 8. Phase 3 — DataContent $type Envelope

> **Design Section:** [06-agui-feature-integration.md §5](design-sections/06-agui-feature-integration.md)  
> **Spec Section:** §9.2  
> **Risk:** Medium  
> **Depends on:** Phase 2

### Typed Envelope Convention (Spec §9.2)

Three wrappers emit `DataContent` with `application/json`. The v2 strategy uses a `$type` discriminator:

```json
{ "$type": "plan_snapshot",     "data": { "steps": [...] } }
{ "$type": "recipe_snapshot",   "data": { "title": "...", "ingredients": [...] } }
{ "$type": "document_preview",  "data": { "document": "..." } }
```

Plan deltas (`application/json-patch+json`) are unchanged — media type alone is sufficient.

### Tasks

| ID | Task | Files |
|----|------|-------|
| P3-1 | Wrap plan snapshots in `AgenticUIAgent` | `AgenticUI/AgenticUIAgent.cs` |
| P3-2 | Wrap recipe snapshots in `SharedStateAgent` | `SharedState/SharedStateAgent.cs` |
| P3-3 | Wrap document previews in `PredictiveStateUpdatesAgent` | `PredictiveStateUpdates/PredictiveStateUpdatesAgent.cs` |
| P3-4 | Update client DataContent routing: parse `$type` → dispatch | `Services/AgentStreamingService.cs` |
| P3-5 | Retain JSON heuristics as fallback for backward compat | `Services/AgentStreamingService.cs` |
| P3-6 | Remove endpoint-awareness guards (`IsSharedStateEndpoint`, etc.) | `Services/AgentStreamingService.cs` |

### Client Routing Logic (06-agui §5.4)

```
DataContent received:
├─ MediaType == "application/json-patch+json"
│  └─ Plan delta → dispatch ApplyPlanDeltaAction
├─ Parse JSON → read "$type" field
│  ├─ "plan_snapshot"     → extract "data" → dispatch SetPlanAction
│  ├─ "recipe_snapshot"   → extract "data" → dispatch SetRecipeAction
│  ├─ "document_preview"  → extract "data" → dispatch SetDocumentAction
│  └─ Unknown $type       → Log warning, skip
└─ No "$type" field (legacy fallback)
   └─ JSON structure heuristics for backward compatibility
```

### Acceptance Criteria

- [ ] All DataContent types include `$type` discriminator
- [ ] Client correctly routes each type to the correct store target
- [ ] Legacy heuristics still work as fallback

---

## 9. Phase 4 — Fluxor Multi-Session State

> **Design Section:** [02-multi-session-state.md](design-sections/02-multi-session-state.md)  
> **Spec Section:** §5  
> **Risk:** **High** — fundamental architecture change  
> **Depends on:** Phase 1 (BB v3 complete)  
> **This is the largest and most impactful phase.**

### Before → After (Spec §5.1)

| Aspect | Before | After |
|--------|--------|-------|
| Stores | 4 independent features | 1 `SessionManagerState` |
| Session concept | None | `SessionMetadata` + `SessionState` per session |
| Actions | Global (no session ID) | All carry `string SessionId` |
| Switching | Destroy + rebuild | Atomic `ActiveSessionId` pointer update |
| Background streaming | Impossible | Per-session `SessionStreamingContext` |

### Tasks

| ID | Task | New/Modified File |
|----|------|-------------------|
| P4-1 | Create `SessionStatus` enum (7 values) | **New:** `Models/SessionStatus.cs` |
| P4-2 | Create `SessionMetadata` record | **New:** `Models/SessionMetadata.cs` |
| P4-3 | Create `SessionState` record (consolidated from 4 stores) | **New:** `Store/SessionManager/SessionState.cs` |
| P4-4 | Create `SessionEntry` record | **New:** `Store/SessionManager/SessionEntry.cs` |
| P4-5 | Create `SessionManagerState` record | **New:** `Store/SessionManager/SessionManagerState.cs` |
| P4-6 | Create `SessionManagerFeature` | **New:** `Store/SessionManager/SessionManagerFeature.cs` |
| P4-7 | Create `SessionActions` (all actions carry SessionId) | **New:** `Store/SessionManager/SessionActions.cs` |
| P4-8 | Create `SessionReducers` with helpers | **New:** `Store/SessionManager/SessionReducers.cs` |
| P4-9 | Create `SessionSelectors` static helper | **New:** `Store/SessionManager/SessionSelectors.cs` |
| P4-10 | Create `SessionTitleEffect` | **New:** `Store/SessionManager/SessionTitleEffect.cs` |
| P4-11 | Migrate component data bindings from global → session-keyed selectors | All `.razor` using Fluxor |
| P4-12 | Remove old stores: `Store/AgentState/`, `ChatState/`, `PlanState/`, `ArtifactState/` | **Remove:** 16 files |
| P4-13 | Update Fluxor DI registration if needed | `AGUIDojoClient/Program.cs` |

### SessionStatus Enum (03-session-lifecycle §1)

```csharp
public enum SessionStatus
{
    Created,     // Session exists but no messages sent
    Active,      // Foreground, idle
    Streaming,   // Foreground, agent generating
    Background,  // Agent generating while user views another session
    Completed,   // Agent finished
    Error,       // Stream failure
    Archived     // Soft-deleted (= destroyed for in-memory MVP)
}
```

### SessionState Record (02-multi-session §3)

```csharp
public sealed record SessionState
{
    // Chat (from ChatState)
    public ImmutableList<ChatMessage> Messages { get; init; } = ImmutableList<ChatMessage>.Empty;
    public ChatMessage? CurrentResponseMessage { get; init; }
    public string? ConversationId { get; init; }
    public int StatefulMessageCount { get; init; }
    public PendingApproval? PendingApproval { get; init; }

    // Agent Run (from AgentState)
    public bool IsRunning { get; init; }
    public string? CurrentAuthorName { get; init; }

    // Plan (from PlanState)
    public Plan? Plan { get; init; }
    public DiffState? PlanDiff { get; init; }

    // Artifacts (from ArtifactState)
    public Recipe? CurrentRecipe { get; init; }
    public DocumentState? CurrentDocumentState { get; init; }
    public bool IsDocumentPreview { get; init; } = true;
    public bool HasInteractiveArtifact { get; init; }
    public ArtifactType ActiveArtifactType { get; init; } = ArtifactType.None;
    public DiffPreviewData? DiffPreview { get; init; }
    public DataGridResult? CurrentDataGrid { get; init; }
    public ImmutableHashSet<ArtifactType> VisibleTabs { get; init; } = ImmutableHashSet<ArtifactType>.Empty;
}
```

### Reducer Pattern (02-multi-session §6.2)

All reducers follow a consistent session-scoped pattern:

```csharp
// Helper: mutate a specific session's state
private static SessionManagerState UpdateSession(
    SessionManagerState state, string sessionId,
    Func<SessionState, SessionState> mutateState)
{
    if (!state.Sessions.TryGetValue(sessionId, out var entry))
        return state;
    var newEntry = entry with { State = mutateState(entry.State) };
    return state with { Sessions = state.Sessions.SetItem(sessionId, newEntry) };
}

// Example: AddMessage
[ReducerMethod]
public static SessionManagerState OnAddMessage(
    SessionManagerState state, SessionActions.AddMessageAction action) =>
    UpdateSession(state, action.SessionId,
        s => s with { Messages = s.Messages.Add(action.Message) });
```

### Data Binding Migration (Spec §8.4)

| Component | v1 Source | v2 Source |
|-----------|-----------|-----------|
| Message list | `ChatStore.Value.Messages` | `SessionSelectors.ActiveMessages(state)` |
| Plan display | `PlanStore.Value.Plan` | `Sessions[ActiveSessionId].State.Plan` |
| Recipe editor | `ArtifactStore.Value.CurrentRecipe` | `Sessions[ActiveSessionId].State.CurrentRecipe` |
| Canvas tabs | `ArtifactStore.Value.VisibleTabs` | `Sessions[ActiveSessionId].State.VisibleTabs` |

### Session Eviction (Spec §5.4)

- `Archived` sessions immediately removed from dictionary (in-memory MVP)
- Maximum **20 active sessions** per circuit
- If cap reached, oldest `Completed` session auto-archived

### Acceptance Criteria

- [ ] `SessionManagerState` replaces all 4 old stores
- [ ] All components bind to session-keyed selectors
- [ ] Session switching is atomic (single dispatch, no flicker)
- [ ] Old `Store/AgentState/`, `Store/ChatState/`, `Store/PlanState/`, `Store/ArtifactState/` deleted
- [ ] Build passes with no references to old store types

---

## 10. Phase 5 — AgentStreamingService Refactoring

> **Design Section:** [02-multi-session-state.md §8–§9](design-sections/02-multi-session-state.md)  
> **Spec Section:** §5.6  
> **Risk:** High  
> **Depends on:** Phase 3 + Phase 4

### Tasks

| ID | Task | Files |
|----|------|-------|
| P5-1 | Define `SessionStreamingContext` class | **New:** `Services/SessionStreamingContext.cs` |
| P5-2 | Refactor `AgentStreamingService` to `ConcurrentDictionary<string, SessionStreamingContext>` | `Services/AgentStreamingService.cs` |
| P5-3 | Implement per-session SSE management (start, cancel, cleanup) | `Services/AgentStreamingService.cs` |
| P5-4 | Add session routing: events routed by `sessionId` | `Services/AgentStreamingService.cs` |
| P5-5 | Replace global Fluxor dispatches with session-scoped | `Services/AgentStreamingService.cs` |
| P5-6 | Implement `InvokeAsync` marshalling for background → circuit | `Services/AgentStreamingService.cs` |
| P5-7 | Implement concurrent stream cap (3 active, 5 queued) | `Services/AgentStreamingService.cs` |
| P5-8 | Implement session cleanup on Archive | `Services/AgentStreamingService.cs` |
| P5-9 | Update `IAgentStreamingService` interface | `Services/IAgentStreamingService.cs` |

### SessionStreamingContext (02-multi-session §8)

```csharp
public sealed class SessionStreamingContext : IDisposable
{
    public CancellationTokenSource? ResponseCancellation { get; set; }
    public HashSet<string> SeenFunctionCallIds { get; } = new();
    public HashSet<string> SeenFunctionResultCallIds { get; } = new();
    public Dictionary<string, string> FunctionCallIdToToolName { get; } = new();
    public ChatOptions ChatOptions { get; } = new();
    public TaskCompletionSource<bool>? ApprovalTaskSource { get; set; }
    public ChatMessage? StreamingMessage { get; set; }
    public object? LastDiffBefore { get; set; }
    public object? LastDiffAfter { get; set; }
    public string LastDiffTitle { get; set; } = "State Diff";
    public void Reset() { /* cancel, clear, dispose */ }
    public void Dispose() { /* cancel, dispose CTS */ }
}
```

### Thread Safety (Spec §5.6, 02-multi-session §8)

Use `ConcurrentDictionary<string, SessionStreamingContext>` — accessed from both Blazor circuit thread and background SSE tasks.

### InvokeAsync Marshalling (Spec §7.2)

```csharp
// Background thread dispatches MUST marshal to circuit sync context:
await InvokeAsync(() => Dispatcher.Dispatch(new SessionCompletedAction(sessionId)));
```

`AgentStreamingService` (not a component) uses a callback pattern: components register an `Action<Action>` dispatcher delegate during initialization that wraps `InvokeAsync`.

### Concurrent Stream Cap (Spec §6.4)

- Max 3 concurrent SSE streams (HTTP/2 multiplexing)
- New messages queued (FIFO, max depth 5) when cap reached
- Session shows `Active` with "(waiting...)" indicator
- When stream completes, next queued message auto-sends
- If queue full (5 pending), "Send" button disabled with tooltip

### Acceptance Criteria

- [ ] Multiple sessions can stream concurrently
- [ ] Background sessions update state correctly
- [ ] Stream cancellation per-session (doesn't affect other sessions)
- [ ] 3-stream cap enforced with queuing behavior

---

## 11. Phase 6 — Session Lifecycle

> **Design Section:** [03-session-lifecycle.md](design-sections/03-session-lifecycle.md)  
> **Spec Section:** §6  
> **Risk:** Medium  
> **Depends on:** Phase 4 + Phase 5

### State Machine (Spec §6.1)

```
                    ┌──────────┐
        ┌──────────>│ Created  │◄──── "New Chat" click
        │           └────┬─────┘
        │                │ First message sent
        │                ▼
        │           ┌──────────┐
        │      ┌───>│ Active   │◄───────────────────────┐
        │      │    └────┬─────┘                         │
        │      │         │ Agent starts streaming        │
        │      │         ▼                               │
        │      │    ┌───────────┐  Switch away  ┌───────────┐
        │      │    │ Streaming ├──────────────>│ Background │
        │      │    └─────┬─────┘               └─────┬─────┘
        │      │          │ Finishes                   │ Finishes
        │      │          ▼                            ▼
        │      │    ┌───────────┐              ┌───────────┐
        │      │    │ Completed │◄─────────────│ Completed │
        │      │    └─────┬─────┘              └───────────┘
        │      │          │ Follow-up message
        │      │          └────────────────────────────┘
        │      │
        │      │    ┌───────────┐
        │      └────│   Error   │ ◄── Stream error
        │           └─────┬─────┘
        │                 │ Retry → back to Active
        │           ┌───────────┐
        └───────────│ Archived  │ ◄── Delete (from any state)
                    └───────────┘
```

### Tasks

| ID | Task | Files |
|----|------|-------|
| P6-1 | Implement session creation ("New Chat" → `CreateSessionAction`) | `Chat.razor`, `SessionReducers.cs` |
| P6-2 | Implement cold start auto-creation | `Chat.razor` `OnInitializedAsync` |
| P6-3 | Implement all status transitions per table | `SessionReducers.cs`, `AgentStreamingService.cs` |
| P6-4 | Implement switching (atomic `SetActiveSessionAction`) | `SessionReducers.cs` |
| P6-5 | Implement Background transition on switch-away during streaming | `SessionReducers.cs` |
| P6-6 | Implement Completed + IncrementUnread for background sessions | `AgentStreamingService.cs` |
| P6-7 | Implement Error status on stream failure + retry | `AgentStreamingService.cs` |
| P6-8 | Implement session archive (soft-delete → immediate cleanup) | `SessionReducers.cs` |
| P6-9 | Implement eviction policy (max 20, auto-archive oldest Completed) | `SessionReducers.cs` |

### Status Transition Reference (03-session-lifecycle §2.2)

| From | Trigger | To |
|------|---------|-----|
| — | "New Chat" click / cold start | `Created` |
| `Created` | First message sent | `Active` → `Streaming` |
| `Streaming` | Agent finishes | `Completed` |
| `Streaming` | User switches away | `Background` |
| `Streaming` | Stream error | `Error` |
| `Streaming` | "Stop Generation" click | `Active` |
| `Background` | Agent finishes | `Completed` + `IncrementUnreadAction` |
| `Background` | Stream error | `Error` + notification |
| `Background` | User switches back | `Streaming` |
| `Completed` | Follow-up message | `Active` → `Streaming` |
| `Error` | User retries | `Active` → `Streaming` |
| Any | User deletes | `Archived` |

### Key Invariants (03-session-lifecycle §2.3)

1. Only one `Active`/`Streaming` foreground session at a time
2. `Background` only from `Streaming` (idle sessions don't become Background)
3. `Background` → `Completed` is automatic (no user interaction)
4. `Error` is recoverable (retry → Active → Streaming)

### Title Generation — MVP (03-session-lifecycle §4.2)

First-message truncation: `"New Chat"` → first 50 chars of first user message, ellipsis if truncated.

### Acceptance Criteria

- [ ] All 7 status transitions work correctly
- [ ] Cold start creates a default session
- [ ] Switching from streaming session → Background; stream continues
- [ ] Background completion increments unread count
- [ ] Session archive cleans up resources
- [ ] Max 20 session eviction works

---

## 12. Phase 7 — Push Notifications

> **Design Section:** [04-realtime-sync-notifications.md](design-sections/04-realtime-sync-notifications.md)  
> **Spec Section:** §7  
> **Risk:** Low–Medium  
> **Can run in parallel with Phase 8**

### Core Insight (Spec §7.1)

Push notification "infrastructure" = dispatch Fluxor action from background thread → component re-render → browser receives DOM diff via existing SignalR circuit. **Zero new infrastructure.**

### Four-Stage Pipeline (Spec §7.2)

```
Stage 1: Background SSE stream (Task.Run per session)
Stage 2: InvokeAsync → Fluxor Dispatch on circuit sync context
Stage 3: IState<T>.StateChanged fires on subscribed components
Stage 4: Blazor DOM diff pushed via existing SignalR circuit
```

### Notification Types (Spec §7.3)

| Event | Notification? | Toast Variant | Duration |
|-------|--------------|---------------|----------|
| Text token | ❌ | — | — |
| Tool call started/completed | ❌ | — | — |
| `FunctionApprovalRequestContent` | ✅ **ApprovalRequired** | Warning (amber, persistent) | Until dismissed |
| `RUN_FINISHED` (background) | ✅ **SessionCompleted** | Success (green) | 5s auto-dismiss |
| `RUN_ERROR` (background) | ✅ **SessionError** | Destructive (red) | 8s auto-dismiss |

### Tasks

| ID | Task | Files |
|----|------|-------|
| P7-1 | Create `SessionNotification` record + `NotificationType` enum | **New:** `Models/SessionNotification.cs` |
| P7-2 | Add notification triggers in `AgentStreamingService` | `Services/AgentStreamingService.cs` |
| P7-3 | Integrate BB v3 toast service for rendering | **New:** `Components/NotificationToast.razor` |
| P7-4 | Implement toast → session switch (click toast = `SetActiveSessionAction`) | Toast component |
| P7-5 | Implement persistent amber toast for HITL approval | Toast component |
| P7-6 | Implement concurrent HITL: one dialog at a time, FIFO toasts | `AgentStreamingService.cs` |

### HITL in Background (Spec §7.4–§7.5)

- Background session needs approval → pulsing amber badge in sidebar + persistent toast
- Clicking toast switches to that session → approval dialog renders immediately
- Only one approval dialog visible at a time (in active session)
- Sessions with pending approvals block on `TaskCompletionSource<bool>`
- Toast ordering is FIFO by arrival time

### Acceptance Criteria

- [ ] Background session completion shows green toast
- [ ] Background error shows red toast
- [ ] Background HITL shows persistent amber toast
- [ ] Clicking toast switches to correct session
- [ ] Multiple concurrent HITL toasts stack correctly

---

## 13. Phase 8 — Session Sidebar UI

> **Design Section:** [05-chat-ui-session-switcher.md](design-sections/05-chat-ui-session-switcher.md)  
> **Spec Section:** §8  
> **Risk:** Medium  
> **Can run in parallel with Phase 7**

### Tasks

| ID | Task | Files |
|----|------|-------|
| P8-1 | Restructure `Chat.razor` sidebar: endpoint selector → session list | `Chat.razor` |
| P8-2 | Create `SessionListItem.razor` with status icons + badges | **New:** `Components/Pages/Chat/SessionListItem.razor` |
| P8-3 | Implement sidebar header (brand + optional Cmd+K search trigger) | `Chat.razor` |
| P8-4 | Implement sidebar footer ("New Chat" button + theme toggle) | `Chat.razor` |
| P8-5 | Replace inset header: endpoint name → session title + agent status | `ChatHeader.razor` |
| P8-6 | Wire `BbSidebar` collapsible (280px → 48px icon-only) | Layout component |
| P8-7 | Implement session ordering (reverse-chronological by `LastActivityAt`) | `SessionSelectors.cs` |
| P8-8 | Remove endpoint selector UI + `SelectedEndpointPath` references | `ChatHeader.razor` |
| P8-9 | Simplify `AGUIChatClientFactory` to single `/chat` endpoint | `Services/AGUIChatClientFactory.cs` |

### Session List Item States (Spec §8.2)

| State | Icon (Lucide 16px) | Badges |
|-------|-------------------|--------|
| Active (current) | `circle` (primary) | Highlighted background |
| Streaming (background) | `loader` (spinning, muted) | Unread count |
| Completed | `check-circle` (green) | Unread count |
| Error | `alert-triangle` (red) | Error badge |
| Needs Approval | `bell` (pulsing amber) | "!" destructive badge |

### BB v3 Component Mapping (Spec §10.2)

| UI Element | BB v3 Component | Key Parameters |
|------------|----------------|----------------|
| Session sidebar | `BbSidebar` | `Collapsible="icon"` |
| Session item | `BbSidebarMenuButton` | `IsActive="@isActive"` |
| Unread badge | `BbBadge` | `Variant="BadgeVariant.Secondary"` |
| Approval badge | `BbBadge` | `Variant="BadgeVariant.Destructive"`, `animate-pulse` |
| Session delete | `DialogService.Confirm()` | Programmatic confirmation |
| Status icons | `BbLucideIcon` | `Name`, `Size="16"` |

### Acceptance Criteria

- [ ] Session sidebar lists all sessions with correct status indicators
- [ ] "New Chat" creates and activates a new session
- [ ] Clicking a session switches to it (atomic swap)
- [ ] Session deletion with confirmation dialog works
- [ ] Sidebar collapses to icon-only mode
- [ ] Endpoint selector fully removed

---

## 14. Phase 9 — Integration & Legacy Removal

> **Spec Section:** §11 (Migration Guide)  
> **Risk:** Medium  
> **Depends on:** All prior phases

### Tasks

| ID | Task | Files |
|----|------|-------|
| P9-1 | Wire all 7 AG-UI features to unified endpoint + session-keyed state | All feature components |
| P9-2 | Update `ToolComponentRegistry` for canvas components | `Services/ToolComponentRegistry.cs` |
| P9-3 | Update `CanvasPane.razor` tab management per session | `Layout/CanvasPane.razor` |
| P9-4 | Extract shared `JsonSerializerOptions` to static instance | Multiple files |
| P9-5 | **Remove legacy 10-endpoint `MapAGUI` calls** | `AGUIDojoServer/Program.cs` |
| P9-6 | Remove `EndpointInfo.cs` and multi-endpoint methods | `Services/EndpointInfo.cs`, `Services/IAGUIChatClientFactory.cs` |
| P9-7 | Refactor `StateManager` to stateless utility | `Services/StateManager.cs`, `Services/IStateManager.cs` |
| P9-8 | Remove unused per-endpoint factory methods from `ChatClientAgentFactory` | `ChatClientAgentFactory.cs` |
| P9-9 | E2E testing: all 7 features through unified endpoint | Manual + automated |

### AG-UI Feature Integration Matrix (Spec §9.1)

| Feature | Tool(s) | Wrapper | Canvas Component | Fluxor Target |
|---------|---------|---------|-----------------|---------------|
| F1: Agentic Chat | *(none)* | None | None (inline) | `Messages` |
| F2: Backend Tool Render | `get_weather` | `ToolResultStreamingChatClient` | `WeatherInfo` (inline) | `Messages` |
| F3: HITL | `send_email` | `ServerFunctionApprovalAgent` | `ApprovalDialog` (modal) | `PendingApproval` |
| F4: Agentic Gen UI | `create_plan`, `update_plan_step` | `AgenticUIAgent` | `PlanDisplay` (tab) | `Plan`, `PlanDiff` |
| F5: Tool-Based Gen UI | `show_chart`, `show_data_grid`, `show_form` | `ToolResultStreamingChatClient` | `ChartDisplay`, `DataTable`, `DynamicForm` (tabs) | `CurrentDataGrid`, `VisibleTabs` |
| F6: Shared State | *(ag_ui_state)* | `SharedStateAgent` | `RecipeEditor` (tab) | `CurrentRecipe` |
| F7: Predictive State | `write_document` | `PredictiveStateUpdatesAgent` | `DocumentEditor`/`DiffPreview` (tab) | `CurrentDocumentState`, `DiffPreview` |

### Acceptance Criteria

- [ ] All 7 features work through single `/chat` endpoint
- [ ] No references to old 10-endpoint architecture remain
- [ ] No references to old 4 Fluxor stores remain
- [ ] `EndpointInfo.cs` deleted
- [ ] Application builds and runs cleanly

---

## 15. Phase 10 — Polish & Verification

| ID | Task |
|----|------|
| P10-1 | `dotnet build AGUIDojo.slnx` — clean build |
| P10-2 | `dotnet test AGUIDojoServer.Tests` — all tests pass |
| P10-3 | Manual: all 7 AG-UI features through single endpoint |
| P10-4 | Manual: create 3+ sessions, switch between, background streaming |
| P10-5 | Manual: HITL approval in background session |
| P10-6 | Manual: session archive + eviction policy (create 21 sessions) |
| P10-7 | Manual: toast notifications for background completion/error/HITL |
| P10-8 | Update `README.md` with new architecture description |

---

## 16. File Inventory

### New Files (Created)

| Phase | Path (under `AGUIDojoClient/`) | Purpose |
|-------|-------------------------------|---------|
| P4 | `Models/SessionStatus.cs` | 7-value session lifecycle enum |
| P4 | `Models/SessionMetadata.cs` | Lightweight session descriptor |
| P4 | `Store/SessionManager/SessionState.cs` | Consolidated per-session state |
| P4 | `Store/SessionManager/SessionEntry.cs` | Metadata + State container |
| P4 | `Store/SessionManager/SessionManagerState.cs` | Root Fluxor state |
| P4 | `Store/SessionManager/SessionManagerFeature.cs` | Fluxor feature registration |
| P4 | `Store/SessionManager/SessionActions.cs` | All session-keyed actions |
| P4 | `Store/SessionManager/SessionReducers.cs` | Session-scoped reducers |
| P4 | `Store/SessionManager/SessionSelectors.cs` | Convenience selectors |
| P4 | `Store/SessionManager/SessionTitleEffect.cs` | Auto-title from first message |
| P5 | `Services/SessionStreamingContext.cs` | Per-session streaming state |
| P7 | `Models/SessionNotification.cs` | Notification record + enum |
| P7 | `Components/NotificationToast.razor` | Toast notification component |
| P8 | `Components/Pages/Chat/SessionListItem.razor` | Session sidebar item |

### Removed Files

| Phase | Path (under `AGUIDojoClient/`) | Reason |
|-------|-------------------------------|--------|
| P4 | `Store/AgentState/AgentActions.cs` | Consolidated into `SessionActions` |
| P4 | `Store/AgentState/AgentFeature.cs` | Replaced by `SessionManagerFeature` |
| P4 | `Store/AgentState/AgentReducers.cs` | Consolidated into `SessionReducers` |
| P4 | `Store/AgentState/AgentState.cs` | Consolidated into `SessionState` |
| P4 | `Store/ChatState/ChatActions.cs` | Consolidated into `SessionActions` |
| P4 | `Store/ChatState/ChatFeature.cs` | Replaced by `SessionManagerFeature` |
| P4 | `Store/ChatState/ChatReducers.cs` | Consolidated into `SessionReducers` |
| P4 | `Store/ChatState/ChatState.cs` | Consolidated into `SessionState` |
| P4 | `Store/PlanState/PlanActions.cs` | Consolidated into `SessionActions` |
| P4 | `Store/PlanState/PlanFeature.cs` | Replaced by `SessionManagerFeature` |
| P4 | `Store/PlanState/PlanReducers.cs` | Consolidated into `SessionReducers` |
| P4 | `Store/PlanState/PlanState.cs` | Consolidated into `SessionState` |
| P4 | `Store/ArtifactState/ArtifactActions.cs` | Consolidated into `SessionActions` |
| P4 | `Store/ArtifactState/ArtifactFeature.cs` | Replaced by `SessionManagerFeature` |
| P4 | `Store/ArtifactState/ArtifactReducers.cs` | Consolidated into `SessionReducers` |
| P4 | `Store/ArtifactState/ArtifactState.cs` | Consolidated into `SessionState` |
| P4 | `Store/ArtifactState/ArtifactType.cs` | Moved to `Models/` (if needed) or kept |
| P9 | `Services/EndpointInfo.cs` | Unified endpoint eliminates endpoint list |

### Significantly Modified Files

| Phase | Path | Change Summary |
|-------|------|----------------|
| P1 | All ~20 `.razor` files | BB v2 → v3 prefix + API migration |
| P1 | `_Imports.razor` | 15 imports → 4 |
| P1 | `GenerativeUI/ChartDisplay.razor` | ApexCharts → ECharts rewrite |
| P2 | `AGUIDojoServer/ChatClientAgentFactory.cs` | New `CreateUnifiedAgent()` method |
| P2 | `AGUIDojoServer/Program.cs` | Single `MapAGUI("/chat")` |
| P2 | 3 wrapper agent `.cs` files | Phase-awareness guards |
| P3 | 3 wrapper agent `.cs` files | `$type` envelope emission |
| P5 | `Services/AgentStreamingService.cs` | Complete refactor: per-session contexts |
| P5 | `Services/IAgentStreamingService.cs` | Updated multi-session interface |
| P8 | `Components/Pages/Chat/Chat.razor` | Sidebar → session list |
| P8 | `Components/Pages/Chat/ChatHeader.razor` | Endpoint name → session title |
| P8 | `Services/AGUIChatClientFactory.cs` | Simplified to single endpoint |
| P9 | `Layout/CanvasPane.razor` | Session-keyed artifact tabs |
| P9 | `Services/StateManager.cs` | Refactored to stateless utility |

---

## 17. Risk Matrix

| Risk | Severity | Phase | Mitigation |
|------|----------|-------|------------|
| BB v3 breaking changes cascade | High | P1 | Isolated phase. Build-test after each M1–M6 step. |
| SharedStateAgent double-invocation breaks composed pipeline | Medium | P2 | Phase-awareness flag; test before removing legacy endpoints. |
| Fluxor state migration corrupts existing features | **High** | P4 | Keep old stores until P4 complete. Parallel validation. |
| `InvokeAsync` marshalling race conditions | Medium | P5 | Follow documented pattern (Spec §7.2). Blazor standard practice. |
| ECharts API mismatch with BB v3 | Medium | P1 | Consult BB v3 docs at implementation time. ChartDisplay is isolated. |
| MEAI001 types change before GA | Medium | P2 | Isolate HITL behind abstractions (Spec §4.2). |
| Concurrent stream cap UX confusion | Low | P5 | Clear sidebar indicators per spec design. |
| 20+ session memory pressure | Low | P6 | Eviction policy (max 20, auto-archive oldest Completed). |

---

## 18. Open Questions (Deferred)

These are explicitly deferred per Spec §13.1:

| ID | Question | Current Decision | Revisit When |
|----|----------|-----------------|-------------|
| OQ-1 | Session persistence (localStorage vs server) | In-memory MVP | When users need cross-circuit continuity |
| OQ-2 | LLM-generated titles | First-message truncation | After multi-session is stable |
| OQ-3 | Auto-generated system prompt from tool metadata | Hand-crafted | When tool count exceeds 15 |
| OQ-4 | `$type` vs vendor media types for DataContent | Envelope wrapper | If AG-UI adds native media type support |
| OQ-5 | SharedStateAgent single-invocation refactor | Keep two-invocation + phase flag | If prompt engineering reliably yields JSON + prose |
| OQ-6 | Context window management | Full history (MVP) | Monitor token costs at scale |

---

## 19. Spec Cross-Reference

This plan covers every section of the spec and all design sections:

| Spec Section | Plan Phase(s) |
|-------------|---------------|
| §1 Executive Summary | All phases |
| §2 Overview & Goals | Scope (§1 of this plan) |
| §3 Architecture Overview | Architecture Delta (§2 of this plan) |
| §4 Unified Agent Endpoint | **Phase 2** |
| §5 Multi-Session State | **Phase 4** |
| §6 Session Lifecycle | **Phase 6** |
| §7 Real-Time Sync & Push | **Phase 7** |
| §8 Chat UI & Session Switcher | **Phase 8** |
| §9 AG-UI Feature Integration | **Phase 3**, Phase 9 |
| §10 BB v3 Component Guide | **Phase 1** |
| §11 Migration Guide | All phases (migration steps distributed) |
| §12 Critique Resolution | Addressed across all phases |
| §13 Open Questions | Deferred (§18 of this plan) |
| Appendix A: Research Traceability | Covered by design section references |

| Design Section | Plan Phase(s) |
|---------------|---------------|
| 01-unified-endpoint.md | **Phase 2**, Phase 3 |
| 02-multi-session-state.md | **Phase 4**, Phase 5 |
| 03-session-lifecycle.md | **Phase 6** |
| 04-realtime-sync-notifications.md | **Phase 7** |
| 05-chat-ui-session-switcher.md | **Phase 8** |
| 06-agui-feature-integration.md | **Phase 3**, Phase 9 |
| 07-bb-v3-component-mapping.md | **Phase 1** |

---

*Implementation should proceed phase-by-phase following the dependency graph (§4). Each phase has clear acceptance criteria. Phases 1∥2 and 7∥8 can be parallelized.*
