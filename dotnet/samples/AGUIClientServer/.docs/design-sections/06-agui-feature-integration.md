# Design Section 06: AG-UI Feature Integration Matrix

> **Spec Section:** AG-UI Feature Integration — Unified Endpoint Coexistence  
> **Created:** 2026-02-24  
> **Status:** Design  
> **References:** Q-AGUI-001, Q-AGUI-002, Q-AGUI-003, Q-AGUI-004, Q-AGUI-005, Q-UNIFY-002, R1, R4, ISS-001, ISS-008, ISS-010, ISS-022, ISS-029, ISS-033  
> **Depends On:** 01-unified-endpoint.md, 02-multi-session-state.md  
> **Inherited By:** task-9

---

## Table of Contents

1. [Purpose](#1-purpose)
2. [The Seven AG-UI Features](#2-the-seven-ag-ui-features)
3. [Feature Integration Matrix](#3-feature-integration-matrix)
4. [Unified Tool Registry](#4-unified-tool-registry)
5. [DataContent Disambiguation Strategy](#5-datacontent-disambiguation-strategy)
6. [Multi-Feature Coexistence](#6-multi-feature-coexistence)
7. [Canvas Pane Tab Management](#7-canvas-pane-tab-management)
8. [Feature Activation & Routing](#8-feature-activation--routing)
9. [Potential Conflicts & Resolution Strategies](#9-potential-conflicts--resolution-strategies)
10. [Feature Interaction Sequences](#10-feature-interaction-sequences)
11. [Design Decisions Summary](#11-design-decisions-summary)

---

## 1. Purpose

This document is the authoritative quick-reference for how all **7 AG-UI features** operate within the unified single-endpoint architecture designed in [01-unified-endpoint.md](01-unified-endpoint.md). It addresses three core questions:

1. **How does each feature work?** — Tools, wrappers, data flow, and canvas rendering.
2. **How do features coexist?** — Disambiguation, tab management, and multi-artifact sessions.
3. **Where are the conflicts?** — Known interaction hazards and their resolution strategies.

> **Ref:** ISS-001 — The original spec conflated 6 conceptual UX patterns with the actual 7+3 AG-UI feature endpoints. This document uses the 7 AG-UI feature taxonomy from the codebase, with the 3 data-type features (data grid, chart, dynamic form) classified under "Tool-Based Generative UI."

---

## 2. The Seven AG-UI Features

Each feature is a distinct capability of the AG-UI protocol, now served through a single `/chat` endpoint (see [01-unified-endpoint.md §2](01-unified-endpoint.md#2-target-state-single-unified-endpoint-after)).

| # | Feature | Short Description | AG-UI Protocol Events Used |
|---|---------|-------------------|---------------------------|
| F1 | **Agentic Chat** | Base text streaming — conversational responses with markdown | `TEXT_MESSAGE_START`, `TEXT_MESSAGE_CONTENT`, `TEXT_MESSAGE_END` |
| F2 | **Backend Tool Rendering** | Tool execution results rendered as rich components in chat | `TOOL_CALL_START`, `TOOL_CALL_ARGS`, `TOOL_CALL_END`, `TOOL_CALL_RESULT` |
| F3 | **Human-in-the-Loop (HITL)** | User approval/rejection before tool execution | `TOOL_CALL_START` (approval tool), `TOOL_CALL_RESULT` (after approval) |
| F4 | **Agentic Generative UI** | LLM-driven plan state via DataContent snapshots/deltas | `STATE_SNAPSHOT`, `STATE_DELTA` (via `DataContent`) |
| F5 | **Tool-Based Generative UI** | Tool results rendered as custom canvas components (weather, chart, grid, form) | `TOOL_CALL_RESULT` → `ToolComponentRegistry` lookup |
| F6 | **Shared State** | Bidirectional state sync (recipe editor) between client and server | `STATE_SNAPSHOT` (via `DataContent`), `ag_ui_state` in request |
| F7 | **Predictive State Updates** | Progressive document preview via streaming tool arguments | `TOOL_CALL_ARGS` (chunked), `STATE_SNAPSHOT` (via `DataContent`) |

> **Ref:** R4 — AG-UI protocol defines 12 event types. Features F1–F7 use different subsets of these events.

---

## 3. Feature Integration Matrix

This is the **primary quick-reference table** — the central artifact of this document. Each row describes how one AG-UI feature integrates with the unified pipeline.

| Feature | Registered Tool(s) | Wrapper Agent | DataContent Type | Canvas Component | Activation Trigger | Fluxor Store Target |
|---------|-------------------|---------------|-----------------|-----------------|-------------------|-------------------|
| **F1: Agentic Chat** | *(none — base LLM response)* | None | None | None (inline chat) | Every user message (default path) | `SessionState.Messages` |
| **F2: Backend Tool Rendering** | `get_weather` | `ToolResultStreamingChatClient` (IChatClient-level) | None | `WeatherInfo` (inline in chat) | LLM selects `get_weather` tool | `SessionState.Messages` (FunctionResultContent) |
| **F3: Human-in-the-Loop** | `send_email` (approval-wrapped) | `ServerFunctionApprovalAgent` | None | `ApprovalDialog` (modal overlay) | LLM selects `send_email` → triggers approval | `SessionState.PendingApproval` |
| **F4: Agentic Generative UI** | `create_plan`, `update_plan_step` | `AgenticUIAgent` | `application/json` (snapshot), `application/json-patch+json` (delta) | `PlanDisplay` (canvas tab) | LLM selects `create_plan` | `SessionState.Plan`, `SessionState.PlanDiff` |
| **F5: Tool-Based Generative UI** | `show_chart`, `show_data_grid`, `show_form` | `ToolResultStreamingChatClient` (IChatClient-level) | None (result is FunctionResultContent) | `ChartDisplay`, `DataTable`, `DynamicForm` (canvas tabs) | LLM selects `show_chart` / `show_data_grid` / `show_form` | `SessionState.CurrentDataGrid`, `SessionState.VisibleTabs` |
| **F6: Shared State** | *(none — operates on `ag_ui_state`)* | `SharedStateAgent` | `application/json` (recipe snapshot) | `RecipeEditor` (canvas tab, bidirectional) | Client sends `ag_ui_state` with recipe data | `SessionState.CurrentRecipe` |
| **F7: Predictive State Updates** | `write_document` | `PredictiveStateUpdatesAgent` | `application/json` (document snapshot, chunked progressively) | `DocumentEditor` / `DiffPreview` (canvas tab) | LLM selects `write_document` | `SessionState.CurrentDocumentState`, `SessionState.DiffPreview` |

> **Ref:** R1 — 10 endpoints consolidated into single pipeline. Q-AGUI-001 — ToolComponentRegistry scales to unified agent (tool names are unique). ISS-010 — Spec lacked tool catalog.

### 3.1 Reading the Matrix

- **Registered Tool(s)**: The `AIFunction` name(s) registered in the unified tool set (see [01-unified-endpoint.md §5](01-unified-endpoint.md#5-unified-tool-registry)). Features F1 and F6 have no explicit tools — F1 is the base path, F6 operates via `ag_ui_state`.
- **Wrapper Agent**: The `DelegatingAIAgent` that intercepts and transforms the feature's data flow (see [01-unified-endpoint.md §3](01-unified-endpoint.md#3-wrapper-composition-order)).
- **DataContent Type**: The `DataContent.MediaType` emitted by the wrapper. Only F4, F6, F7 emit `DataContent`; F2, F3, F5 use `FunctionResultContent` directly.
- **Canvas Component**: The Blazor component rendered in the canvas pane (right side of dual-pane layout).
- **Activation Trigger**: What causes the feature to engage in a conversation.
- **Fluxor Store Target**: The `SessionState` field(s) updated when this feature activates (see [02-multi-session-state.md §3](02-multi-session-state.md#3-sessionstate-record)).

---

## 4. Unified Tool Registry

All tools are registered once in the unified agent. Tool names serve as the **primary router** — the LLM's native tool selection picks the right tool based on user intent (see [01-unified-endpoint.md §5.3](01-unified-endpoint.md#53-tool-as-router-strategy)).

### 4.1 Complete Tool Catalog

| # | Tool Name | Category | Description (LLM-Visible) | When to Use | Wrapper Interaction | HITL? |
|---|-----------|----------|--------------------------|-------------|-------------------|-------|
| 1 | `get_weather` | Data query | Get the weather for a given location. | User asks about weather conditions | `ToolResultStreamingChatClient` emits `FunctionResultContent` | No |
| 2 | `send_email` | Action | Send an email to a recipient. Requires user approval. | User asks to send an email | `ServerFunctionApprovalAgent` intercepts `FunctionApprovalRequestContent` | **Yes** |
| 3 | `write_document` | Content creation | Write or edit a document. Use markdown formatting. Always write the full document. | User asks to write, draft, edit, or compose any document/article/story | `PredictiveStateUpdatesAgent` intercepts `FunctionCallContent`, streams progressive chunks | No |
| 4 | `create_plan` | Planning | Create a plan with multiple steps for task execution. | User asks to plan, organize, break down a task | `AgenticUIAgent` intercepts `FunctionResultContent`, emits `DataContent` snapshot | No |
| 5 | `update_plan_step` | Planning | Update a step in an existing plan with new description or status. | Used internally after `create_plan` to progress each step | `AgenticUIAgent` intercepts `FunctionResultContent`, emits `DataContent` delta | No |
| 6 | `show_chart` | Visualization | Show data as a chart (bar, line, pie, area). Use for trends, comparisons, distributions. | User asks for a chart, graph, or visual comparison | `ToolResultStreamingChatClient` emits `FunctionResultContent` | No |
| 7 | `show_data_grid` | Visualization | Show structured data in a rich table view. Use for lists, inventories, tabular data. | User asks for a table, list, or tabular view | `ToolResultStreamingChatClient` emits `FunctionResultContent` | No |
| 8 | `show_form` | UI generation | Show a dynamic form for user input. Use for registrations, feedback, orders. | User asks the agent to collect structured input | `ToolResultStreamingChatClient` emits `FunctionResultContent` | No |

> **Ref:** ISS-010 — "The spec does not catalog the actual tools." This table serves as the authoritative tool catalog.

### 4.2 Tool Name Uniqueness Guarantee

Every tool name is globally unique across the unified agent. The `ToolComponentRegistry` (client-side) maps tool names directly to Blazor components. No disambiguation is needed at the tool level.

> **Ref:** Q-AGUI-001 — "ToolComponentRegistry scales to unified agent — tool names are unique, registry works as-is."

### 4.3 Tools vs. Non-Tool Features

Two features operate **without** registered tools:

| Feature | Mechanism | Why No Tool? |
|---------|-----------|-------------|
| **F1: Agentic Chat** | Base LLM text response | Chat is the default path — no tool invocation needed |
| **F6: Shared State** | `SharedStateAgent` reads `ag_ui_state` from `AdditionalProperties` | State sync is client-initiated via the AG-UI protocol's `state` field, not via tool calls. The LLM receives state context and responds naturally. |

This distinction is important: F6 is activated by the **client sending state**, not by the LLM choosing a tool. The `SharedStateAgent` wrapper detects `ag_ui_state` in `AdditionalProperties` and runs its two-invocation pattern regardless of which tools the LLM selects.

> **Ref:** Q-AGUI-003 — "SharedState activation is client-driven via ag_ui_state — LLM handles topic routing."

---

## 5. DataContent Disambiguation Strategy

### 5.1 The Problem

Three wrapper agents emit `DataContent` with `application/json` media type, but with semantically different payloads. The client must route each `DataContent` to the correct Fluxor store target.

| Source Wrapper | DataContent Purpose | Media Type | JSON Shape |
|----------------|-------------------|------------|------------|
| `AgenticUIAgent` | Plan state snapshot | `application/json` | `{ "steps": [...], "title": "..." }` |
| `AgenticUIAgent` | Plan state delta | `application/json-patch+json` | `[{ "op": "replace", "path": "...", "value": "..." }]` |
| `SharedStateAgent` | Recipe state snapshot | `application/json` | `{ "title": "...", "ingredients": [...], "instructions": [...] }` |
| `PredictiveStateUpdatesAgent` | Document preview snapshot | `application/json` | `{ "document": "..." }` |

> **Ref:** ISS-008 — "DataContent disambiguation is fragile." Q-AGUI-002 — "DataContent disambiguation is fragile — needs typed envelope or distinct MediaTypes."

### 5.2 Current Disambiguation: JSON Structure Heuristics

The existing client (`AgentStreamingService`) uses a **cascading if-else chain** with structural checks:

```
DataContent received:
│
├─ MediaType == "application/json-patch+json"
│  └─ Plan delta → dispatch ApplyPlanDeltaAction
│
├─ Has "steps" property
│  └─ Plan snapshot → dispatch SetPlanAction
│
├─ Has "ingredients" or "instructions" property
│  └─ Recipe snapshot → dispatch SetRecipeAction
│
├─ Has "document" property
│  └─ Document snapshot → dispatch SetDocumentAction
│
└─ Fallback → Log warning, add raw to message
```

**Current additional guard**: The client uses endpoint-awareness (`IsSharedStateEndpoint`, `IsPredictiveStateEndpoint`) to gate recipe and document parsing. In the unified endpoint model, these endpoint guards are removed — all features share one path.

### 5.3 Proposed v2 Strategy: Typed Envelope Convention

For the unified endpoint, adopt a **typed envelope convention** where each wrapper includes a discriminator in the `DataContent`:

**Option B (selected): Envelope wrapper with `$type` discriminator**

```json
// Plan snapshot (from AgenticUIAgent)
{
  "$type": "plan_snapshot",
  "data": { "steps": [...], "title": "..." }
}

// Recipe snapshot (from SharedStateAgent)
{
  "$type": "recipe_snapshot", 
  "data": { "title": "...", "ingredients": [...], "instructions": [...] }
}

// Document preview (from PredictiveStateUpdatesAgent)
{
  "$type": "document_preview",
  "data": { "document": "..." }
}
```

**Rationale for Option B over alternatives:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| A: Vendor MediaTypes | `application/vnd.agui.plan+json` | Clean HTTP semantics | Requires wrapper changes to emit custom media types; some AG-UI clients may not propagate custom media types |
| **B: Envelope `$type`** | `{ "$type": "plan_snapshot", "data": {...} }` | Explicit, extensible, self-documenting JSON; trivial client parsing; no media type changes | Adds one wrapping layer; requires wrapper modifications |
| C: AdditionalProperties metadata | `DataContent.AdditionalProperties["ag_ui_content_type"]` | No JSON structure change | Relies on AG-UI transport preserving AdditionalProperties; fragile |

### 5.4 v2 Client Routing Logic

With the typed envelope, the client's `DataContent` router becomes explicit:

```
DataContent received:
│
├─ MediaType == "application/json-patch+json"
│  └─ Plan delta → dispatch ApplyPlanDeltaAction
│
├─ Parse JSON → read "$type" field
│  ├─ "plan_snapshot"     → extract "data" → dispatch SetPlanAction
│  ├─ "recipe_snapshot"   → extract "data" → dispatch SetRecipeAction
│  ├─ "document_preview"  → extract "data" → dispatch SetDocumentAction
│  └─ Unknown $type       → Log warning, skip
│
└─ No "$type" field (legacy fallback)
   └─ Apply JSON structure heuristics (§5.2) for backward compatibility
```

### 5.5 Fallback Strategy

The JSON structure heuristics (§5.2) are retained as a **fallback** for backward compatibility. If the `$type` field is absent (e.g., during incremental migration), the client falls back to structural matching. This ensures no feature breaks during the transition from per-endpoint guards to typed envelopes.

> **Ref:** [01-unified-endpoint.md §9](01-unified-endpoint.md#9-datacontent-disambiguation-strategy) — "Keep JSON structure heuristics (proven, working) and add logging for unrecognized shapes. Plan for Option B (envelope wrapper) in a future iteration."

### 5.6 Server-Side Changes Required

Each wrapper that emits `DataContent` must be modified to include the `$type` discriminator:

| Wrapper | Current Emission | v2 Emission |
|---------|-----------------|-------------|
| `AgenticUIAgent` (snapshot) | `{ "steps": [...] }` | `{ "$type": "plan_snapshot", "data": { "steps": [...] } }` |
| `AgenticUIAgent` (delta) | *(no change — uses `application/json-patch+json`)* | *(no change)* |
| `SharedStateAgent` | `{ "title": "...", "ingredients": [...] }` | `{ "$type": "recipe_snapshot", "data": { "title": "...", ... } }` |
| `PredictiveStateUpdatesAgent` | `{ "document": "..." }` | `{ "$type": "document_preview", "data": { "document": "..." } }` |

---

## 6. Multi-Feature Coexistence

### 6.1 The Value Proposition

The key benefit of the unified agent is that **multiple features can activate within a single conversation**. A user can ask the agent to:

1. "Plan a dinner party" → F4 (plan with steps)
2. "Show me a chart of budget breakdown" → F5 (chart visualization)
3. "Draft the invitation email" → F7 (document editor)
4. "Send the invitation to James" → F3 (HITL approval)
5. "Here's the recipe, adjust the servings to 8" → F6 (shared state, recipe editor)

All five interactions use different tools and wrappers, but share one conversation thread, one SSE connection, and one `SessionState`.

> **Ref:** Q-AGUI-005 — "Multi-feature conversations are the key value proposition — Canvas tabs handle multiple artifacts."

### 6.2 Feature Coexistence Rules

| Combination | Compatible? | Notes |
|-------------|-------------|-------|
| F1 (chat) + any feature | ✅ Yes | Chat text interleaves naturally with tool calls |
| F2 (tool render) + F5 (tool UI) | ✅ Yes | Different tool names route to different components |
| F3 (HITL) + any feature | ✅ Yes | Approval dialog is modal overlay — blocks only the pending tool, not the conversation |
| F4 (plan) + F5 (chart/grid/form) | ✅ Yes | Separate canvas tabs — plan in one tab, chart/grid in another |
| F4 (plan) + F7 (document) | ✅ Yes | Separate canvas tabs and separate `SessionState` fields |
| F6 (shared state) + F4 (plan) | ⚠️ Conditional | SharedStateAgent double-invocation sees both streams. Outer wrappers skip during `state_update` phase (see [01-unified-endpoint.md §4.3](01-unified-endpoint.md#43-proposed-solution-invocation-context-flag)) |
| F6 (shared state) + F7 (document) | ⚠️ Conditional | Same double-invocation caveat applies |
| F7 (document) + F5 (chart) | ✅ Yes | PredictiveStateUpdatesAgent is tool-name-scoped to `write_document` only — does not interfere with chart/grid/form tools |
| F4 (plan) + F4 (plan) | ❌ Exclusive | Only one plan active at a time per session. `create_plan` replaces the current plan. |
| F6 (recipe) + F6 (recipe) | ❌ Exclusive | Only one recipe state per session. Each `ag_ui_state` update replaces the previous recipe. |

> **Ref:** Q-AGUI-004 — "PredictiveStateUpdates is tool-name-scoped to write_document — no interference." Q-UNIFY-002 — "SharedStateAgent caveat — outer wrappers see both invocation streams."

### 6.3 Multi-Artifact Session State

A single `SessionState` (see [02-multi-session-state.md §3](02-multi-session-state.md#3-sessionstate-record)) can hold **all** artifact types simultaneously:

```
SessionState
├── Messages: [chat messages interleaved with tool calls]
├── Plan: { steps: [...], title: "Dinner Party Plan" }
├── CurrentRecipe: { title: "Pasta Recipe", ingredients: [...] }
├── CurrentDocumentState: { document: "# Invitation\n..." }
├── CurrentDataGrid: { columns: [...], rows: [...] }
├── DiffPreview: { before: ..., after: ..., title: "..." }
├── ActiveArtifactType: Chart  (which tab is currently visible)
└── VisibleTabs: { Plan, RecipeEditor, DocumentEditor, Chart }
```

Each field is independently nullable/settable. Tool calls progressively populate these fields during a conversation.

---

## 7. Canvas Pane Tab Management

### 7.1 Tab Registration

The canvas pane uses a tab-based layout to display multiple artifacts. Tabs are **dynamically added** when a feature first produces output:

| Feature | Tab Name | Added When | Removed When |
|---------|----------|------------|-------------|
| F4 | "Plan" | `create_plan` result dispatches `SetPlanAction` | Plan completed or session reset |
| F5 (chart) | "Chart" | `show_chart` result dispatches `SetChartAction` | Session reset |
| F5 (grid) | "Data" | `show_data_grid` result dispatches `SetDataGridAction` | Session reset |
| F5 (form) | "Form" | `show_form` result dispatches `SetFormAction` | Session reset |
| F6 | "Recipe" | Recipe state received from `SharedStateAgent` | Session reset |
| F7 | "Document" | `write_document` starts streaming chunks | Session reset |
| F7 (diff) | "Changes" | Document editing completed → diff available | Diff accepted/rejected |

### 7.2 Tab Auto-Focus Rules

When a new artifact is produced, the canvas pane **auto-switches** to its tab:

1. **New artifact type**: If the tool produces a canvas artifact type that has no existing tab, create the tab and switch to it.
2. **Existing artifact updated**: If the tool updates an existing artifact (e.g., `update_plan_step` updating the plan), the tab stays focused if already active. If another tab is active, show an **update badge** on the plan tab but do NOT auto-switch (avoids disruptive tab flipping).
3. **Document streaming**: During `write_document` progressive preview, the Document tab is auto-focused on the first chunk. Subsequent chunks update in-place.

> **Ref:** Q-AGUI-005 — "ArtifactState.VisibleTabs tracks which tabs are active in the canvas pane."

### 7.3 VisibleTabs State

The `VisibleTabs` field in `SessionState` (type: `ImmutableHashSet<ArtifactType>`) tracks which tabs are currently displayed. The `ArtifactType` enum defines:

```csharp
public enum ArtifactType
{
    None,
    Plan,
    RecipeEditor,
    DocumentEditor,
    DiffPreview,
    Chart,
    DataGrid,
    DynamicForm
}
```

Reducers add to `VisibleTabs` when artifacts are created and remove on session reset.

### 7.4 Tab Limit Strategy

For MVP, there is **no hard limit** on visible tabs. With 7 possible artifact types, the maximum is 7 simultaneous tabs. The tab bar uses horizontal scrolling (via `BbScrollArea`) when tabs exceed the canvas width.

Future iterations may introduce tab grouping, tab overflow menus, or stacking behaviors for >5 simultaneous artifacts.

---

## 8. Feature Activation & Routing

### 8.1 Server-Side: LLM-Driven Tool Selection

The unified agent relies on the LLM's native tool selection as the router. The system prompt (see [01-unified-endpoint.md §6.2](01-unified-endpoint.md#62-proposed-unified-system-prompt)) provides category-level guidance:

```
User: "Show me a chart of monthly sales"
  → LLM selects: show_chart (visualization category)
  → ToolResultStreamingChatClient emits FunctionResultContent
  → Client: ToolComponentRegistry maps "show_chart" → ChartDisplay component
  → Canvas: Chart tab appears with rendered chart
```

```
User: "Draft a resignation letter"  
  → LLM selects: write_document (content creation category)
  → PredictiveStateUpdatesAgent intercepts FunctionCallContent
  → PredictiveStateUpdatesAgent emits progressive DataContent chunks
  → Client: DataContent router → "$type": "document_preview" → SetDocumentAction
  → Canvas: Document tab appears with live preview
```

### 8.2 Client-Side: Content Type Routing

The client's `AgentStreamingService` routes incoming content to the correct feature handler:

```
AgentResponseUpdate received:
│
├── TextContent
│   └─ F1: Agentic Chat → append to streaming message
│
├── FunctionCallContent
│   ├─ Name == "write_document"
│   │  └─ F7: PredictiveStateUpdatesAgent activates (server-side)
│   └─ Other tools
│      └─ F2/F5: Standard tool execution
│
├── FunctionApprovalRequestContent
│   └─ F3: HITL → dispatch SetPendingApprovalAction → show ApprovalDialog
│
├── FunctionResultContent
│   ├─ ToolName in ToolComponentRegistry?
│   │  └─ F5: Tool-Based Generative UI → render component in canvas
│   └─ Standard result
│      └─ F2: Backend Tool Rendering → show result inline
│
├── DataContent
│   ├─ MediaType == "application/json-patch+json"
│   │  └─ F4: Plan delta → dispatch ApplyPlanDeltaAction
│   ├─ "$type" == "plan_snapshot"
│   │  └─ F4: Plan snapshot → dispatch SetPlanAction
│   ├─ "$type" == "recipe_snapshot"
│   │  └─ F6: Recipe state → dispatch SetRecipeAction
│   ├─ "$type" == "document_preview"
│   │  └─ F7: Document snapshot → dispatch SetDocumentAction
│   └─ Unknown → fallback to JSON structure heuristics
│
└── FunctionApprovalResponseContent
    └─ F3: HITL approval/rejection → clear pending approval
```

### 8.3 SharedState Activation: Client-Initiated

Unlike other features, F6 (Shared State) is **not** triggered by LLM tool selection. It is activated when the client includes `ag_ui_state` in the AG-UI request:

```
Client sends request with state:
  RunAgentInput { 
    messages: [...], 
    state: { "title": "Pasta", "ingredients": [...] }  ← ag_ui_state
  }
    → MapAGUI places state in AdditionalProperties["ag_ui_state"]
    → SharedStateAgent detects ag_ui_state
    → First invocation: state update (JSON schema response)
    → Second invocation: natural language summary
    → Emits DataContent: {"$type": "recipe_snapshot", "data": {...}}
```

The recipe editor component (`RecipeEditor`) in the canvas pane sends updated state with each user edit. The `SharedStateAgent` processes it server-side and returns the reconciled state.

> **Ref:** Q-AGUI-003 — "SharedState activation is client-driven via ag_ui_state."

---

## 9. Potential Conflicts & Resolution Strategies

### 9.1 Conflict Registry

| # | Conflict | Features | Severity | Resolution |
|---|----------|----------|----------|------------|
| C1 | **DataContent collision** | F4, F6, F7 | High | Typed envelope (`$type` discriminator) — see §5.3 |
| C2 | **SharedStateAgent double-invocation** | F6 + any outer wrapper (F3, F4, F7) | High | Invocation context flag (`ag_ui_shared_state_phase`) — see [01-unified-endpoint.md §4.3](01-unified-endpoint.md#43-proposed-solution-invocation-context-flag) |
| C3 | **Single active plan** | F4 (multiple `create_plan` calls) | Medium | `create_plan` replaces previous plan. System prompt: "Only one plan can be active at a time." |
| C4 | **Document overwrite** | F7 (multiple `write_document` calls) | Medium | Each `write_document` replaces the previous document state. DiffPreview shows before/after. |
| C5 | **Approval blocking** | F3 + F1 (text continues during approval wait) | Low | Approval dialog is non-blocking for text streaming. The agent can continue chatting while waiting for approval. The `send_email` tool execution is paused, but the LLM can issue other tool calls or text in subsequent turns. |
| C6 | **Tab auto-focus race** | Any two features producing artifacts simultaneously | Low | Last-write-wins for `ActiveArtifactType`. In practice, the LLM rarely invokes two artifact-producing tools in parallel. Sequential tool calls produce sequential tab switches. |
| C7 | **Tool argument streaming overlap** | F7 (`write_document` args) + other tool args | Low | `PredictiveStateUpdatesAgent` is scoped to `FunctionCallContent` where `Name == "write_document"`. Other tools' arguments are not intercepted. |
| C8 | **Endpoint guard removal** | F6, F7 (currently use `IsSharedStateEndpoint`, `IsPredictiveStateEndpoint`) | Medium | Replace endpoint guards with `$type`-based routing (§5.4). Remove `IsSharedStateEndpoint` / `IsPredictiveStateEndpoint` from `AgentStreamingService`. |

> **Ref:** ISS-008 — DataContent disambiguation. ISS-029 — Spec conflated tool-based and agentic generative UI.

### 9.2 C2 Deep Dive: SharedStateAgent Double-Invocation

This is the **hardest integration challenge**. When `SharedStateAgent` (F6) is composed with outer wrappers:

**Problem**: `SharedStateAgent` runs `InnerAgent.RunStreamingAsync()` twice. During the first invocation (state update with JSON schema), the LLM may theoretically issue tool calls that outer wrappers would incorrectly intercept.

**Resolution**: The invocation context flag approach (see [01-unified-endpoint.md §4.3](01-unified-endpoint.md#43-proposed-solution-invocation-context-flag)):

1. `SharedStateAgent` sets `AdditionalProperties["ag_ui_shared_state_phase"] = "state_update"` for invocation 1
2. Outer wrappers check this flag:
   - `ServerFunctionApprovalAgent`: If phase == `state_update`, skip approval interception
   - `AgenticUIAgent`: If phase == `state_update`, skip plan result interception
   - `PredictiveStateUpdatesAgent`: If phase == `state_update`, skip document streaming
3. Second invocation (`summary` phase) operates normally — all wrappers are active

**Safety guarantee**: The first invocation uses a JSON schema response format. The LLM is constrained to return structured JSON matching the recipe schema — it cannot issue tool calls. Therefore, outer wrappers would never actually intercept content during phase 1 regardless. The flag is a **defense-in-depth** measure.

### 9.3 C8 Deep Dive: Endpoint Guard Removal

The current `AgentStreamingService` uses boolean flags to guard feature-specific DataContent processing:

```csharp
// Current (per-endpoint):
else if (content is DataContent recipeDc && IsSharedStateEndpoint && ...)
else if (content is DataContent docDc && IsPredictiveStateEndpoint && ...)
```

In the unified endpoint, `IsSharedStateEndpoint` and `IsPredictiveStateEndpoint` are always `false` (there's only one endpoint). These guards must be replaced by `$type`-based routing:

```csharp
// Unified (v2):
else if (content is DataContent dc && TryRouteDataContent(dc, out var action))
{
    _dispatcher.Dispatch(action);
}
```

Where `TryRouteDataContent` reads the `$type` discriminator (§5.4) and returns the appropriate Fluxor action.

---

## 10. Feature Interaction Sequences

### 10.1 Multi-Feature Conversation Example

This sequence shows a natural conversation that exercises 5 features:

```
Turn 1: User → "Help me plan a dinner party for 6 people"
  └─ LLM → F4: create_plan(steps: ["Menu selection", "Shopping list", "Cooking timeline", ...])
  └─ Canvas: Plan tab appears with steps

Turn 2: User → "Here's a pasta recipe I want to use" + [edits recipe in RecipeEditor]
  └─ Client sends ag_ui_state with recipe JSON
  └─ F6: SharedStateAgent processes recipe, adjusts servings
  └─ Canvas: Recipe tab appears alongside Plan tab

Turn 3: User → "Show me a budget breakdown for the party"
  └─ LLM → F5: show_chart(type: "pie", data: [...])
  └─ Canvas: Chart tab added; auto-focused

Turn 4: User → "Draft an invitation for the guests"
  └─ LLM → F7: write_document(content: "# Dinner Party Invitation\n...")
  └─ F7: Progressive chunks stream into Document tab
  └─ Canvas: Document tab added; live preview

Turn 5: User → "Send the invitation to james@example.com"
  └─ LLM → F3: send_email(to: "james@example.com", subject: ..., body: ...)
  └─ F3: ApprovalDialog appears as modal overlay
  └─ User approves → email sent
  └─ Chat: "Email sent to james@example.com"
```

**Session state after Turn 5:**
- 5 canvas tabs visible: Plan, Recipe, Chart, Document, (Changes if diff available)
- `VisibleTabs`: `{ Plan, RecipeEditor, Chart, DocumentEditor }`
- All artifact state fields populated in `SessionState`

### 10.2 Feature Transition Cleanness

Each turn activates a different feature. The transitions are clean because:
1. Tool names are unique — no ambiguity in routing.
2. Wrapper agents are scoped — each only intercepts its specific content types.
3. DataContent is typed — the `$type` discriminator routes to the correct store.
4. Canvas tabs accumulate — no feature displaces another.

---

## 11. Design Decisions Summary

| # | Decision | Rationale | Alternatives Considered | Reference |
|---|----------|-----------|------------------------|-----------|
| D1 | Typed envelope (`$type`) for DataContent disambiguation | Explicit, extensible, backward-compatible with fallback heuristics | Vendor MediaTypes (A), AdditionalProperties metadata (C) | ISS-008, Q-AGUI-002 |
| D2 | ToolComponentRegistry unchanged for unified agent | Tool names are already unique; registry maps tool name → component without modification | Per-feature component registries | Q-AGUI-001 |
| D3 | SharedState activation via `ag_ui_state` (client-driven) | Follows AG-UI protocol design; LLM handles conversational context naturally | Dedicated tool for state sync; server-side state polling | Q-AGUI-003 |
| D4 | PredictiveStateUpdates scoped by tool name only | `write_document` is the only tool that benefits from progressive preview; explicit name scoping avoids false matches | Content-type-based scoping; all tools get progressive preview | Q-AGUI-004 |
| D5 | Canvas tabs accumulate (no displacement) | Multi-feature conversations need all artifacts visible; tabs let users switch between artifacts freely | Single-artifact canvas (replace on each new artifact); split-pane canvas | Q-AGUI-005 |
| D6 | LLM native tool selection as router | 8 tools with clear descriptions is within LLM capability; no explicit routing logic needed | Workflow-based routing; keyword-based pre-routing; user selects feature manually | [01-unified-endpoint.md §5.3] |
| D7 | Invocation context flag for SharedStateAgent composition | Preserves proven two-invocation pattern; defense-in-depth for wrapper safety | Refactor to single invocation; skip composition entirely | [01-unified-endpoint.md §4.3] |
| D8 | Replace endpoint guards with `$type`-based routing | Endpoint guards don't work with unified endpoint; `$type` is explicit and extensible | Separate DataContent processing pipeline per wrapper; wrapper-tagged metadata | ISS-008, §9.3 |

---

## References

- **R1**: Server Architecture — 10 endpoints & agent factory (research.md)
- **R4**: AG-UI Protocol — 12 event types, `STATE_SNAPSHOT`/`STATE_DELTA` (research.md)
- **Q-AGUI-001**: ToolComponentRegistry scales to unified agent (brainstorm.md)
- **Q-AGUI-002**: DataContent disambiguation is fragile (brainstorm.md)
- **Q-AGUI-003**: SharedState activation is client-driven (brainstorm.md)
- **Q-AGUI-004**: PredictiveStateUpdates is tool-name-scoped (brainstorm.md)
- **Q-AGUI-005**: Multi-feature conversations with Canvas tabs (brainstorm.md)
- **Q-UNIFY-002**: Builder pattern supports stacking — SharedStateAgent caveat (brainstorm.md)
- **ISS-001**: Spec scope mismatch — 6 UX patterns vs 7+3 AG-UI features (spec-critique.md)
- **ISS-008**: DataContent disambiguation fragility (spec-critique.md)
- **ISS-010**: No tool catalog in spec (spec-critique.md)
- **ISS-022**: Plan state data flow not specified (spec-critique.md)
- **ISS-029**: Spec conflates tool-based and agentic generative UI (spec-critique.md)
- **ISS-033**: STATE_SNAPSHOT routing from multiple DataContent producers (spec-critique.md)
