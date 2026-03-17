# Spec Critique: Agentic UX Unified Spec — Blazor AG-UI Blueprint

> **Source Document:** `Agentic UX Unified Spec - Blazor AG-UI Blueprint.md` (19 sections, 1054 lines)
> **Created:** 2026-02-24T11:30:00+0700
> **References:** Q-SPEC-001 through Q-SPEC-008, R1–R13
> **Purpose:** Structured, section-by-section critique to inform the design of Unified Agentic Chat Spec v2

---

## Summary

The existing spec is a well-structured architectural document covering 6 agentic UX patterns with Blazor + AG-UI + BlazorBlueprint. However, it has **critical gaps** in multi-session support, push notifications, and unified endpoint design. Additionally, **all code samples target BB v2.1.1** which is now outdated (BB v3 requires `Bb` prefix, flattened namespaces, and replaced ApexCharts with ECharts). Several MAF API references predate the current RC surface. The spec also conflates conceptual UX patterns with implementation code in ways that make it difficult to evolve incrementally.

**Issue Totals:** 42 issues identified
- Critical: 12
- Major: 17
- Minor: 13

---

## Issue Legend

| Field | Values |
|-------|--------|
| **Severity** | Critical (blocks implementation), Major (significant rework needed), Minor (localized fix) |
| **Category** | Outdated (references superseded APIs/libs), Missing (feature/section absent), Incorrect (factual error), Gap (incomplete coverage) |

---

## Section 1: Introduction — The Paradigm Shift to Agentic Interfaces

### ISS-001
- **Severity:** Minor
- **Category:** Gap
- **Section:** §1.2 Scope
- **Description:** The scope claims to cover "all six patterns" but the actual codebase implements **7 AG-UI features** (agentic chat, backend tool rendering, HITL, agentic generative UI, tool-based generative UI, shared state, predictive state updates) plus **3 data-type features** (data grid, chart, dynamic form). The six UX patterns (Reflection, Tool Use, Planning, Multi-Agent, Artifact Editing, Generative UI) are conceptual categories that don't map 1:1 to the AG-UI feature endpoints. This mismatch confuses which "features" are being implemented.
- **Reference:** R1 (10 endpoints enumerated)

### ISS-002
- **Severity:** Minor
- **Category:** Gap
- **Section:** §1.1 Six Pillars
- **Description:** The "Multi-Agent Collaboration" pillar (§1.1.4) is described as a core pattern but the current codebase has **zero multi-agent orchestration**. All 10 endpoints use single-agent pipelines with decorator wrappers, not multi-agent handoffs. The spec should acknowledge this as a future capability rather than implying current support.
- **Reference:** R1, R2 (single agent per endpoint, decorator pattern only)

---

## Section 2: The Architectural Core — Decoupling Brain and Body

### ISS-003
- **Severity:** Critical
- **Category:** Missing
- **Section:** §2.1 Distributed Agent Pattern
- **Description:** The architecture diagram shows a single `MapAGUI()` endpoint, but the actual codebase has **10 separate MapAGUI endpoints** each with dedicated agent instances. The spec never describes the multi-endpoint architecture, the endpoint selection mechanism on the client, or the rationale for per-feature endpoint separation. This is the most fundamental architectural gap — the spec describes a different system than what exists.
- **Reference:** R1 (10 endpoints), R7 (AGUIChatClientFactory hardcoded endpoint list)

### ISS-004
- **Severity:** Critical
- **Category:** Missing
- **Section:** §2.1
- **Description:** **No unified endpoint design.** The spec does not address how to consolidate 10 agents into a single unified endpoint, how to compose multiple agent wrappers (ServerFunctionApprovalAgent, AgenticUIAgent, SharedStateAgent, PredictiveStateUpdatesAgent) on one agent, or how the LLM selects the right tool from a combined toolset. R2 confirms the MAF builder pattern supports stacking, but this is not specified.
- **Reference:** R2 (builder pattern composability), Q-UNIFY-002 answer

### ISS-005
- **Severity:** Major
- **Category:** Outdated
- **Section:** §2.2 Why Blazor Blueprint UI
- **Description:** The section describes BB as having "88 Components: 15 headless Primitives + 73 pre-styled Components" and "Tailwind/CSS Utility Integration." These counts and descriptions match BB v2.x. BB v3 has a different component count, restructured namespaces, new components (BbSidebar, BbCommandDialog, DialogService), and architectural changes (two-layer portal system, ECharts replacing ApexCharts). The rationale section should reference BB v3 capabilities.
- **Reference:** Q-SPEC-001, Q-SPEC-002, Q-SPEC-003, R8 (BB v2.1.1 confirmed)

---

## Section 3: The AG-UI Protocol — Transport and Streaming

### ISS-006
- **Severity:** Minor
- **Category:** Gap
- **Section:** §3.1 Protocol Architecture
- **Description:** The SSE vs SignalR comparison table is accurate but omits a critical nuance: **concurrent SSE streams** for multi-session support. HTTP/2 multiplexing resolves the HTTP/1.1 6-connection limit, but practical limits (3-5 concurrent active streams recommended) and the impact on server resources are not discussed.
- **Reference:** R13 (concurrent SSE analysis)

### ISS-007
- **Severity:** Major
- **Category:** Missing
- **Section:** §3.2 AgentResponseUpdate Envelope
- **Description:** The content type table lists `TextContent`, `FunctionCallContent`, `FunctionApprovalRequestContent`, and `DataContent` but does **not** describe `FunctionResultContent` or how tool results flow back to the client. The `ToolResultStreamingChatClient` wrapper (R11) is essential for the client to receive tool completion events, yet is not mentioned anywhere in the spec.
- **Reference:** R11 (ToolResultStreamingChatClient analysis)

### ISS-008
- **Severity:** Major
- **Category:** Gap
- **Section:** §3.2
- **Description:** **DataContent disambiguation is fragile.** The spec says `DataContent` with `application/json` triggers `UpdateStateAction`, but in reality multiple features use `DataContent` with different semantic meanings: plan state snapshots (AgenticUIAgent), recipe state snapshots (SharedStateAgent), document previews (PredictiveStateUpdatesAgent), and AG-UI `STATE_SNAPSHOT`/`STATE_DELTA` events. The spec does not describe how the client distinguishes between these different `DataContent` payloads. The current codebase uses JSON structure heuristics which is fragile.
- **Reference:** Q-AGUI-002 answer, R1 (4 wrapper agents all emit DataContent)

---

## Section 4: Server-Side Implementation Strategy

### ISS-009
- **Severity:** Major
- **Category:** Outdated
- **Section:** §4.1 Middleware Configuration
- **Description:** The code sample shows `IAgentFactory.Create("primary")` and `app.MapAGUI("/agent", agent)` — neither matches the actual codebase. The real implementation uses `ChatClientAgentFactory` (a custom class, not an interface) with 10 dedicated factory methods (`CreateAgenticChat()`, `CreateBackendToolRendering()`, etc.) and 10 `MapAGUI()` calls. The simplified example is misleading.
- **Reference:** R1 (actual factory pattern)

### ISS-010
- **Severity:** Major
- **Category:** Gap
- **Section:** §4.2 Tool Registration
- **Description:** The spec describes tool registration abstractly but does not catalog the **actual tools** registered across the 10 agents: `get_weather`, `send_email`, `write_document`, `create_plan`, `update_plan_step`, `show_chart`, `show_data_grid`, `show_form`, `get_recipe`, `update_recipe`, plus approval-wrapped tools. A unified agent needs all tools listed with their signatures, categories, and which wrapper agents they interact with.
- **Reference:** R1 (tool sets per agent)

### ISS-011
- **Severity:** Major
- **Category:** Outdated
- **Section:** §4.3 HITL Middleware
- **Description:** The HITL flow description is correct conceptually but does not mention that `FunctionApprovalRequestContent` and `ApprovalRequiredAIFunction` are still **experimental** (require `#pragma warning disable MEAI001`). The spec should note the API stability status so implementers are not surprised.
- **Reference:** R12 (MEAI001 pragma), Q-SPEC-006 answer

---

## Section 5: Client-Side Integration — AGUIChatClient

### ISS-012
- **Severity:** Major
- **Category:** Outdated
- **Section:** §5.1 DI and Lifecycle
- **Description:** The code sample shows direct `AGUIChatClient` registration via `AddHttpClient<AGUIChatClient>` and `AddScoped<IChatClient>`. The actual codebase uses `AGUIChatClientFactory` with `IHttpClientFactory` to create per-endpoint clients. With a unified endpoint the sample simplifies, but the current spec doesn't reflect either the actual multi-endpoint pattern or the target unified pattern.
- **Reference:** R7 (AGUIChatClientFactory)

### ISS-013
- **Severity:** Major
- **Category:** Gap
- **Section:** §5.2 Streaming Consumption
- **Description:** The streaming code sample uses `_client.GetStreamingResponseAsync()` directly but the actual codebase wraps this in `AgentStreamingService` which handles: cancellation token management, function call deduplication, tool name tracking, streaming message accumulation, HITL approval flow, diff preview state, and Fluxor dispatch. None of this complexity is specified. The spec's simplified loop would not produce a working implementation.
- **Reference:** R6 (AgentStreamingService full analysis)

---

## Section 6: Dual-Pane Layout Architecture

### ISS-014
- **Severity:** Outdated
- **Category:** Outdated
- **Section:** §6.1 Splitter Component
- **Description:** The section references `Resizable` from Blazor Blueprint UI and compares it against Telerik, Syncfusion, and Blazorise alternatives. The actual codebase uses BB's `BbResizable` (in BB v3: `BbResizablePanelGroup`, `BbResizablePanel`, `BbResizableHandle`). The comparison table with competitor libraries is filler — the decision has already been made.
- **Reference:** Q-SPEC-001 (BB v3 prefix), actual codebase components

### ISS-015
- **Severity:** Minor
- **Category:** Outdated
- **Section:** §6.2 State Persistence
- **Description:** References `Blazored.LocalStorage` for splitter state persistence. The actual codebase may or may not use this. The more pressing concern is that the dual-pane layout must accommodate a **session sidebar** (new requirement) which fundamentally changes the layout from 2-pane to 3-pane (sidebar + chat + canvas). This is not addressed.
- **Reference:** Q-UX-001 (session list placement)

---

## Section 7: Visual Design System — Project "Claude"

### ISS-016
- **Severity:** Minor
- **Category:** Gap
- **Section:** §7.1–§7.4
- **Description:** The design system is comprehensive and well-documented. Minor gap: no specification of **notification toast styles** (needed for push notifications), **session status indicator colors** (active/idle/streaming/error), or **unread message badge styling**.
- **Reference:** Q-NOTIF-002, Q-UX-002

---

## Section 8: UX Pattern I — Reflection

### ISS-017
- **Severity:** Major
- **Category:** Outdated
- **Section:** §8.2, §8.4
- **Description:** Code sample uses BB v2 non-prefixed components: `<Accordion>`, `<AccordionItem>`, `<AccordionTrigger>`, `<AccordionContent>`, `<Badge>`. BB v3 requires: `<BbAccordion>`, `<BbAccordionItem>`, `<BbAccordionTrigger>`, `<BbAccordionContent>`, `<BbBadge>`.
- **Reference:** Q-SPEC-001

### ISS-018
- **Severity:** Minor
- **Category:** Outdated
- **Section:** §8.2
- **Description:** Component tree references `Accordion`, `Badge`, `Card` without `Bb` prefix.
- **Reference:** Q-SPEC-001

---

## Section 9: UX Pattern II — Tool Use

### ISS-019
- **Severity:** Major
- **Category:** Outdated
- **Section:** §9.2, §9.3
- **Description:** Code samples use BB v2 non-prefixed components: `<Command>`, `<Dialog>`, `<Table>`, `<TableHeader>`, `<TableRow>`, `<TableHead>`, `<TableBody>`, `<TableCell>`. All need `Bb` prefix for BB v3.
- **Reference:** Q-SPEC-001

### ISS-020
- **Severity:** Major
- **Category:** Gap
- **Section:** §9.2 Authorization
- **Description:** The approval dialog description is UX-level only. It does not address: (a) HITL in **background sessions** — what happens when an approval is needed in a session the user isn't viewing? (b) timeout behavior if the user doesn't respond, (c) the `ServerFunctionApprovalAgent` wrapper that translates `FunctionApprovalRequestContent` to/from AG-UI format. The spec treats HITL as purely a UI concern but it has significant server-side middleware implications.
- **Reference:** Q-NOTIF-004 (background HITL), R1 (ServerFunctionApprovalAgent)

---

## Section 10: UX Pattern III — Planning

### ISS-021
- **Severity:** Major
- **Category:** Outdated
- **Section:** §10.2, §10.3
- **Description:** Code samples use BB v2 components: `<Sheet>`, `<SheetContent>`, `<SheetHeader>`, `<SheetTitle>`, `<SheetDescription>`, `<Progress>`, `<Card>`, `<CardHeader>`, `<CardTitle>`, `<CardDescription>`. All need `Bb` prefix.
- **Reference:** Q-SPEC-001

### ISS-022
- **Severity:** Major
- **Category:** Gap
- **Section:** §10.2
- **Description:** The plan is described as a persistent Sheet but does not explain how plan state flows from server to client. The actual mechanism: `AgenticUIAgent` intercepts `create_plan`/`update_plan_step` FunctionResultContent and emits `DataContent` (snapshots with `application/json`, deltas with `application/json-patch+json`). The client must detect these specific DataContent patterns and dispatch to PlanState. This critical data flow is not specified.
- **Reference:** R1 (AgenticUIAgent interceptor pattern)

---

## Section 11: UX Pattern IV — Multi-Agent Orchestration

### ISS-023
- **Severity:** Major
- **Category:** Incorrect
- **Section:** §11 (entire section)
- **Description:** This section describes a multi-agent "Council" interface with agent avatars, role badges, and handoff visualization. **The codebase has zero multi-agent orchestration.** All endpoints use single agents with decorator wrappers. The MAF Workflow API (R9) exists for future multi-agent support but is not currently integrated. This section describes a feature that doesn't exist and isn't planned for the unified chat spec.
- **Reference:** R1 (single agent per endpoint), R9 (Workflow API is separate)

### ISS-024
- **Severity:** Major
- **Category:** Outdated
- **Section:** §11.3
- **Description:** Code sample uses BB v2 components: `<Avatar>`, `<AvatarImage>`, `<AvatarFallback>`, `<Badge>`. Need `Bb` prefix.
- **Reference:** Q-SPEC-001

---

## Section 12: UX Pattern V — Artifact Editing (Monaco Editor)

### ISS-025
- **Severity:** Minor
- **Category:** Gap
- **Section:** §12.2
- **Description:** The diff editor section describes the "Review Changes" pattern but does not connect it to the `PredictiveStateUpdatesAgent` which actually drives progressive document previews via chunked DataContent snapshots with 50ms delays. The spec treats artifact editing as user-initiated diff review, but the actual implementation streams progressive edits from the agent. This is a fundamental behavioral difference.
- **Reference:** R1 (PredictiveStateUpdatesAgent chunking behavior)

### ISS-026
- **Severity:** Minor
- **Category:** Gap
- **Section:** §12.4
- **Description:** The workflow (`Agent proposes edit → DiffEditor renders → Accept/Reject`) doesn't account for the **streaming diff preview** pattern where the document is being written progressively (character by character) by the agent. The user sees a live preview while the agent is still generating. Accept/reject only applies after completion.
- **Reference:** R1 (PredictiveStateUpdatesAgent)

---

## Section 13: UX Pattern VI — Generative UI

### ISS-027
- **Severity:** Major
- **Category:** Outdated
- **Section:** §13.1
- **Description:** The `ComponentRegistry` code sample uses tool names (`ShowWeatherWidget`, `ShowDataGrid`, `ShowChart`, `ShowForm`) that don't match the actual codebase tool names (`get_weather`, `show_chart`, `show_data_grid`, `show_form`). The registry pattern is correct but the sample is misleading.
- **Reference:** R1 (actual tool names in ChatClientAgentFactory)

### ISS-028
- **Severity:** Minor
- **Category:** Outdated
- **Section:** §13.2
- **Description:** The `RenderTreeBuilder` code sample references `InputText` and `DataTable<object>` — BB v2 types. BB v3 would use `BbInput` and `BbDataTable`.
- **Reference:** Q-SPEC-001

### ISS-029
- **Severity:** Major
- **Category:** Gap
- **Section:** §13 (entire section)
- **Description:** The generative UI section does not distinguish between **tool-based generative UI** (server tool returns data → client renders component via registry) and **agentic generative UI** (agent maintains plan state → client renders plan components via DataContent). These are two different AG-UI features with different data flows. The spec conflates them under a single "Generative UI" umbrella.
- **Reference:** R1 (separate endpoints: `/tool_based_generative_ui` vs `/agentic_generative_ui`)

---

## Section 14: State Management and Conversation Branching

### ISS-030
- **Severity:** Critical
- **Category:** Incorrect
- **Section:** §14.1 Fluxor Store Architecture
- **Description:** The Fluxor store table lists `AgentState`, `ChatState`, `PlanState`, `ArtifactState` with abstract descriptions that don't match the actual store implementations. The actual stores have richer state: `ChatState` includes `ConversationId`, `StatefulMessageCount`, `PendingApproval`; `ArtifactState` includes `CurrentRecipe`, `CurrentDocumentState`, `CurrentDataGrid`, `ActiveArtifactType`, `VisibleTabs`. The action dispatch table references actions (`AgentStartedThinkingAction`, `ToolExecutionRequestedAction`) that don't exist in the codebase.
- **Reference:** R5 (actual Fluxor stores)

### ISS-031
- **Severity:** Critical
- **Category:** Missing
- **Section:** §14.1
- **Description:** **No multi-session state design.** All 4 Fluxor stores are global singletons per Blazor circuit. There is ZERO session concept in the state model. The spec must design session-keyed state management: a `SessionState` record wrapping per-session data, a `SessionManagerState` with `Dictionary<string, SessionState>` and `ActiveSessionId`, and atomic session switching to avoid partial state updates.
- **Reference:** Q-SPEC-004, R5 (global singleton stores), R6 (per-circuit streaming state)

### ISS-032
- **Severity:** Critical
- **Category:** Missing
- **Section:** §14.2
- **Description:** The "Temporal State Graph" (DAG-based conversation branching) is described as a core feature but **the codebase does not implement it**. `ChatState.Messages` is a flat `ImmutableList<ChatMessage>`, not a DAG. The `ChatNode` record with `BranchId` and `ParentId` from §18.4 code samples does not exist in the actual codebase. The spec describes a design that was never built. The new spec should either commit to implementing DAG branching or remove it.
- **Reference:** R5 (ChatState uses ImmutableList, no branching)

### ISS-033
- **Severity:** Major
- **Category:** Gap
- **Section:** §14.3 AG-UI State Snapshots
- **Description:** The description of STATE_SNAPSHOT synchronization is oversimplified. In reality, state snapshots arrive as `DataContent` from multiple sources (AgenticUIAgent for plans, SharedStateAgent for recipes, PredictiveStateUpdatesAgent for documents) and must be routed to different Fluxor stores based on content structure. A single `UpdateAgentStateAction` dispatcher cannot handle this polymorphism.
- **Reference:** R1 (4 different DataContent producers), ISS-008

---

## Section 15: Orchestration and Observability

### ISS-034
- **Severity:** Minor
- **Category:** Gap
- **Section:** §15.1 Golden Triangle
- **Description:** The "Golden Triangle" (DevUI + AG-UI + OpenTelemetry) is well-described but doesn't mention the **Aspire MCP** tooling which provides resource management, structured logs, and trace inspection. Since the codebase uses Aspire for orchestration, MCP is a relevant observability addition.
- **Reference:** R1 (Aspire-based orchestration)

---

## Section 16: Mobile Responsiveness

### ISS-035
- **Severity:** Minor
- **Category:** Gap
- **Section:** §16
- **Description:** The responsive design table is reasonable but does not account for the **session sidebar** that will be added for multi-session support. On mobile, the sidebar + chat + canvas 3-pane layout needs additional responsive breakpoints. The "Single pane with Tab/Drawer toggle" approach needs extending to include session management.
- **Reference:** Q-UX-001 (session list placement)

---

## Section 17: Conclusion

### ISS-036
- **Severity:** Minor
- **Category:** Gap
- **Section:** §17
- **Description:** The conclusion's bullet points accurately summarize the spec's claims but do not reflect the actual state of the codebase. Claims like "DAG-based conversation branching" (not implemented) and "orchestration and observability" (partially implemented) overstate completion. The new spec's conclusion should distinguish between designed, implemented, and planned features.
- **Reference:** ISS-032 (DAG not implemented)

---

## Section 18: Implementation Guide — Core Code Structures

### ISS-037
- **Severity:** Critical
- **Category:** Outdated
- **Section:** §18.1 NuGet Packages
- **Description:** Package references use generic names without versions. Missing critical packages: `Fluxor.Blazor.Web.ReduxDevTools` (used in codebase), `Microsoft.Extensions.Http` (for IHttpClientFactory). The `BlazorBlueprint.Components` reference is BB v2; needs version bump to BB v3. `BlazorBlueprint.Icons` should be `BlazorBlueprint.Icons.Lucide` to match actual package name.
- **Reference:** R8 (actual package versions)

### ISS-038
- **Severity:** Critical
- **Category:** Outdated
- **Section:** §18.2 Service Registration
- **Description:** The code sample uses `AddBlazorBlueprint()` for DI registration. The actual codebase uses `AddBlazorBlueprintPrimitives()` (BB v2). BB v3 changes this to `AddBlazorBlueprintComponents()`. The sample also registers `AGUIChatClient` directly instead of through `AGUIChatClientFactory`. It references `AddScoped<IChatClient>` which bypasses the multi-endpoint factory pattern.
- **Reference:** Q-SPEC-002 (namespace/DI changes), R7 (factory pattern)

### ISS-039
- **Severity:** Critical
- **Category:** Incorrect
- **Section:** §18.3 Agent Service Wrapper
- **Description:** The `AgentService` class does not exist in the codebase. The actual streaming handler is `AgentStreamingService` which has fundamentally different responsibilities: it manages cancellation, deduplication, tool name mapping, HITL approval flows, and streaming message accumulation. The spec's simplified wrapper would lose all of this. Additionally, the sample references `ChatResponseItem` and `ResponseType` types that don't exist.
- **Reference:** R6 (AgentStreamingService analysis)

### ISS-040
- **Severity:** Critical
- **Category:** Incorrect
- **Section:** §18.4 Fluxor State Definitions
- **Description:** The Fluxor state definitions define `ChatNode` with `BranchId`/`ParentId` for DAG branching — a feature that doesn't exist in the codebase. The `AgentStatus` enum (`Idle`, `Thinking`, `ExecutingTool`, `WaitingForApproval`) doesn't match the actual `AgentState` record which tracks `SelectedEndpointPath`, `IsRunning`, `CurrentAuthorName`. The `PlanTask` record uses `TaskStatus` as a nested enum which conflicts with `System.Threading.Tasks.TaskStatus`.
- **Reference:** R5 (actual Fluxor stores), ISS-032

---

## Section 19: Works Cited

### ISS-041
- **Severity:** Minor
- **Category:** Gap
- **Section:** §19
- **Description:** References are comprehensive but missing key sources: (a) MAF RC blog post (API stability confirmation), (b) BB v3 migration guide (breaking changes), (c) BB v3 FAQ, (d) the agent-framework GitHub repository itself. The new spec should cite these as primary references.
- **Reference:** R12 (MAF RC status), R8 (BB version)

---

## Cross-Cutting Issues (Not Section-Specific)

### ISS-042
- **Severity:** Critical
- **Category:** Missing
- **Section:** (entire spec)
- **Description:** **No push notification architecture.** The spec's only real-time mechanism is SSE for agent token streaming. There is no design for: (a) notifying the user when a background session completes agent response, (b) alerting the user when a background session needs HITL approval, (c) Blazor Server callback mechanism for server→client push, (d) toast notification integration for session events. R10 identifies Blazor Server InvokeAsync callbacks as the MVP approach.
- **Reference:** Q-SPEC-005, R10 (push notification analysis), Q-NOTIF-001 through Q-NOTIF-004

### ISS-043
- **Severity:** Critical
- **Category:** Missing
- **Section:** (entire spec)
- **Description:** **No multi-session architecture.** The spec assumes a single active conversation throughout. Missing: (a) session data model and lifecycle (create, title, switch, close), (b) session-keyed Fluxor state with atomic switching, (c) session-aware AgentStreamingService with per-session CancellationTokenSource, dedup sets, ChatOptions, (d) session list UI component, (e) concurrent SSE stream management, (f) session persistence strategy (in-memory vs localStorage vs server-side). This is the largest architectural gap.
- **Reference:** Q-SPEC-004, R5 (global singleton stores), R6 (per-circuit streaming state), Q-STATE-001 through Q-STATE-006

### ISS-044
- **Severity:** Critical
- **Category:** Missing
- **Section:** (entire spec)
- **Description:** **No session lifecycle design.** No specification for: when sessions are created (explicit "New Chat" button vs auto-create), how sessions get titles (manual vs LLM-generated from first message), when sessions are destroyed (explicit close vs timeout vs circuit disconnect), whether closed sessions can be reopened, state cleanup on session close.
- **Reference:** Q-STATE-004, Q-UX-003, Q-UX-005

### ISS-045
- **Severity:** Major
- **Category:** Outdated
- **Section:** §8, §9, §10, §11, §13 (all code samples)
- **Description:** **Every code sample in the spec uses BB v2 non-prefixed component tags.** BB v3 requires the `Bb` prefix on ALL component tags. Affected components across the spec: `Accordion`, `AccordionItem`, `AccordionTrigger`, `AccordionContent`, `Badge`, `Card`, `CardHeader`, `CardTitle`, `CardDescription`, `Command`, `Dialog`, `Table`, `TableHeader`, `TableRow`, `TableHead`, `TableBody`, `TableCell`, `Sheet`, `SheetContent`, `SheetHeader`, `SheetTitle`, `SheetDescription`, `Progress`, `Avatar`, `AvatarImage`, `AvatarFallback`, `Separator`, `InputText`, `DataTable`. This is a systemic issue affecting every code sample.
- **Reference:** Q-SPEC-001, R8 (BB v2.1.1 confirmed in codebase)

### ISS-046
- **Severity:** Major
- **Category:** Outdated
- **Section:** §18.2
- **Description:** **BB namespace imports outdated.** The spec's service registration implies sub-namespace imports (e.g., `@using BlazorBlueprint.Components.Button`). BB v3 flattened all namespaces into `@using BlazorBlueprint.Components` + `@using BlazorBlueprint.Primitives`. DI registration changes from `AddBlazorBlueprintPrimitives()` to `AddBlazorBlueprintComponents()`.
- **Reference:** Q-SPEC-002

### ISS-047
- **Severity:** Major
- **Category:** Outdated
- **Section:** §13, §14 (chart references)
- **Description:** **ApexCharts chart API is obsolete.** BB v3 replaced ApexCharts with ECharts. Any chart rendering (data grid visualization, generative charts) must use the new `<BbBarChart>`, `<BbLineChart>`, `<BbAreaChart>` APIs with `DataKey` string properties instead of lambda expressions. The `ChartDisplay` component and chart tools need complete rewriting.
- **Reference:** Q-SPEC-003

### ISS-048
- **Severity:** Major
- **Category:** Outdated
- **Section:** §5 (Portal references)
- **Description:** **BB v3 portal architecture is different.** BB v3 introduces a two-layer portal system: `PortalCategory.Container` for layout portals and `PortalCategory.Overlay` for floating portals. Components using portals (Dialog, Sheet, Select, Tooltip, Popover) need the host structure updated: `<BbContainerPortalHost>` and `<BbOverlayPortalHost>` in the root layout.
- **Reference:** Q-SPEC-007

### ISS-049
- **Severity:** Minor
- **Category:** Outdated
- **Section:** All code samples with `AsChild`
- **Description:** **BB v3 defaults `AsChild` to `true`.** Existing code with explicit `AsChild="true"` on Trigger/Close components is now redundant (minor cleanup, not breaking).
- **Reference:** Q-SPEC-008

### ISS-050
- **Severity:** Major
- **Category:** Gap
- **Section:** (entire spec)
- **Description:** **No AgentStreamingService specification.** The most complex piece of client-side logic — `AgentStreamingService` — is not described anywhere in the spec. It handles: SSE stream consumption, token buffering and throttled rendering, function call/result dedup, tool name tracking, streaming message accumulation, HITL approval task source, diff preview state, Fluxor action dispatch, and cancellation. For multi-session support, every one of these concerns must become session-scoped.
- **Reference:** R6 (full AgentStreamingService analysis)

### ISS-051
- **Severity:** Major
- **Category:** Gap
- **Section:** (entire spec)
- **Description:** **No endpoint selection or client factory specification.** The `AGUIChatClientFactory` which manages endpoint routing on the client is not described. With a unified endpoint, this factory simplifies but the spec should describe the client's connection strategy, HttpClient configuration via IHttpClientFactory, and Aspire service discovery integration.
- **Reference:** R7 (AGUIChatClientFactory analysis)

### ISS-052
- **Severity:** Minor
- **Category:** Gap
- **Section:** (entire spec)
- **Description:** **No chat input specification.** The spec describes agent output rendering extensively but has no specification for the chat input component: message composition, send button, stop generation button, attachment support, keyboard shortcuts, or typing indicators. BB v3's `UpdateTiming` default change from `Immediate` to `OnChange` affects input responsiveness.
- **Reference:** Q-BB-005

---

## Issue Summary by Section

| Section | Issues | Critical | Major | Minor |
|---------|--------|----------|-------|-------|
| §1 Introduction | ISS-001, ISS-002 | 0 | 0 | 2 |
| §2 Architectural Core | ISS-003, ISS-004, ISS-005 | 2 | 1 | 0 |
| §3 AG-UI Protocol | ISS-006, ISS-007, ISS-008 | 0 | 2 | 1 |
| §4 Server-Side | ISS-009, ISS-010, ISS-011 | 0 | 3 | 0 |
| §5 Client-Side | ISS-012, ISS-013 | 0 | 2 | 0 |
| §6 Dual-Pane Layout | ISS-014, ISS-015 | 0 | 0 | 2 |
| §7 Design System | ISS-016 | 0 | 0 | 1 |
| §8 Reflection | ISS-017, ISS-018 | 0 | 1 | 1 |
| §9 Tool Use | ISS-019, ISS-020 | 0 | 2 | 0 |
| §10 Planning | ISS-021, ISS-022 | 0 | 2 | 0 |
| §11 Multi-Agent | ISS-023, ISS-024 | 0 | 2 | 0 |
| §12 Artifact Editing | ISS-025, ISS-026 | 0 | 0 | 2 |
| §13 Generative UI | ISS-027, ISS-028, ISS-029 | 0 | 2 | 1 |
| §14 State Management | ISS-030, ISS-031, ISS-032, ISS-033 | 2 | 1 | 0 |† |
| §15 Observability | ISS-034 | 0 | 0 | 1 |
| §16 Mobile | ISS-035 | 0 | 0 | 1 |
| §17 Conclusion | ISS-036 | 0 | 0 | 1 |
| §18 Implementation | ISS-037, ISS-038, ISS-039, ISS-040 | 4 | 0 | 0 |
| §19 Works Cited | ISS-041 | 0 | 0 | 1 |
| Cross-Cutting | ISS-042–ISS-052 | 4 | 6 | 2† |

† ISS-031 counted in §14 Critical; ISS-049 counted in Cross-Cutting Minor.

---

## Issue Summary by Category

| Category | Count | Key Issues |
|----------|-------|------------|
| **Outdated** | 16 | ISS-005, ISS-009, ISS-011, ISS-012, ISS-014, ISS-017–019, ISS-021, ISS-024, ISS-027–028, ISS-037–038, ISS-045–049 |
| **Missing** | 11 | ISS-003, ISS-004, ISS-031, ISS-042–044, ISS-050–051 |
| **Gap** | 18 | ISS-001–002, ISS-006, ISS-008, ISS-010, ISS-013, ISS-015–016, ISS-020, ISS-022, ISS-025–026, ISS-029, ISS-033–036, ISS-041, ISS-052 |
| **Incorrect** | 5 | ISS-023, ISS-030, ISS-032, ISS-039–040 |

---

## Top Priority Issues for v2 Spec

The following issues MUST be resolved in the Unified Agentic Chat Spec v2:

1. **ISS-043** (Critical/Missing) — Multi-session architecture: session-keyed state, concurrent streams, session lifecycle
2. **ISS-042** (Critical/Missing) — Push notification architecture: background session completion, HITL alerts
3. **ISS-004** (Critical/Missing) — Unified endpoint design: wrapper composition, tool consolidation, system prompt engineering
4. **ISS-003** (Critical/Missing) — Accurate architecture diagram reflecting actual 10-endpoint → unified endpoint evolution
5. **ISS-045** (Major/Outdated) — BB v2→v3 migration: all code samples need Bb prefix, namespace flattening, chart rewrite
6. **ISS-039** (Critical/Incorrect) — Replace fictional AgentService with actual AgentStreamingService specification
7. **ISS-031** (Critical/Missing) — Session-keyed Fluxor state design with atomic switching
8. **ISS-008** (Major/Gap) — DataContent disambiguation strategy for plan/recipe/document/state payloads
9. **ISS-032** (Critical/Incorrect) — Remove or commit to DAG conversation branching (currently fiction)
10. **ISS-050** (Major/Gap) — Full AgentStreamingService specification with session-scoping design
