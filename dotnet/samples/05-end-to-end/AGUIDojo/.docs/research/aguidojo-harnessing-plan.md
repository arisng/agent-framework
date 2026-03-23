# Grounding the AGUIDojo harnessing plan

> Historical background note: this document remains useful for context, but the active rollout guidance now lives in the [AGUIDojo implementation plan](../how-to/implementation-plan.md).

## Executive summary

Today, `dotnet/samples/05-end-to-end/AGUIDojo` is already a unified, full-stack sample built around a single `POST /chat` AG-UI route, a Blazor Server BFF, per-session artifact state, governance/approval workflows, predictive document updates, shared state, observability, and focused tests. The remaining work is therefore not just “implement tools in `AGUIDojoServer/Tools`”; it is a sample-wide hardening and realism roadmap that must cover server tool maturity, client artifact/rendering integrity, operational plumbing, documentation refresh, and validation.[^1][^2]

In other words: the issue should stay focused on AGUIDojo, but it should be rewritten as a **sample-wide roadmap** rather than a **server-only AG-UI feature checklist**.[^1][^2]

## Research method

This report is grounded in:

- the current issue note,
- the sample root README and project files,
- live server/client/AppHost runtime code,
- AG-UI and MAF framework source used by the sample,
- current test projects and validation notes,
- and older internal docs treated as historical context rather than source-of-truth when they disagree with current code.[^1][^2][^3][^4][^5]

## Architecture / system overview

```text
Browser
  │
  ▼
AGUIDojoClient (Blazor Server BFF)
  ├─ Fluxor session store, approvals, artifacts, notifications
  ├─ direct SSE/HTTP to AGUIDojoServer /chat
  └─ YARP proxy for /api/*
        │
        ▼
AGUIDojoServer (Minimal API)
  ├─ POST /chat      unified AG-UI pipeline
  ├─ /api/*          business/dev endpoints
  ├─ /health         readiness/liveness
  └─ OpenTelemetry + ProblemDetails + auth plumbing
        │
        ▼
AG-UI / MAF framework
  ├─ RunAgentInput + MapAGUI transport
  ├─ SSE event conversion
  └─ AIAgent builder pipeline + wrapper composition
```

The Aspire AppHost wires the client and server together, injects the backend URL into both the client’s direct SSE path (`SERVER_URL`) and the YARP cluster address, and waits for the backend health check before starting the client. That makes orchestration part of the sample story, not an optional extra.[^3]

## Detailed findings

### 1. Why the current issue must be reframed

The issue currently says, in effect:

1. several server tools are still not “real,”
2. each tool should be correlated to AG-UI features,
3. AG-UI + MAF should be re-researched,
4. and the AGUIDojoServer folder structure should be reconsidered.[^1]

That is directionally correct, but incomplete. The current sample root README describes a system that already includes:

- one unified `/chat` route,
- client-owned session state,
- plan rendering,
- approvals,
- shared recipe state,
- document previews / Monaco diff,
- charts, forms, and data grids,
- background-session notifications,
- and YARP-backed business APIs.[^2]

Because those surfaces already exist, “harnessing” cannot be scoped to server-side tool implementations alone. Any meaningful tool promotion or AG-UI capability work will cascade into:

- wrapper-agent behavior,
- typed event payloads,
- client state routing,
- artifact rendering rules,
- governance UX,
- persistence / session behavior,
- docs,
- and tests.[^4][^5][^17][^19][^20][^21]

### 2. Unified runtime architecture is already in place

#### Server

The server already registers AG-UI services, scoped business services, DI-backed tool wrappers, health checks, business API endpoints, and one unified `app.MapAGUI("/chat", ...)` route. It also exposes `/api/*` endpoints and `/health`, and explicitly notes that SSE endpoints surface errors through AG-UI `RunErrorEvent` rather than normal REST error middleware.[^4]

The unified agent is not theoretical. `ChatClientAgentFactory.CreateUnifiedAgent(...)` builds it today by:

- wrapping the LLM client with `ContextWindowChatClient` and `ToolResultStreamingChatClient`,
- creating one tool set,
- and layering `MultimodalAttachmentAgent`, `ServerFunctionApprovalAgent`, `AgenticUIAgent`, `PredictiveStateUpdatesAgent`, `SharedStateAgent`, and OpenTelemetry in a single builder pipeline.[^5]

The builder semantics matter here: `AIAgentBuilder` applies wrappers in reverse order so the first `Use(...)` call becomes the outermost layer. This means wrapper order is intentional and already part of the sample’s architecture, not a future design exercise.[^6]

#### Client / BFF

The client already operates as a Blazor Server BFF. It:

- registers Fluxor,
- creates a dedicated direct `HttpClient` for AG-UI SSE,
- bypasses YARP for `/chat`,
- uses YARP for `/api/*`,
- registers governance, observability, patching, shared-state, streaming, markdown, and viewport services,
- and mounts Fluxor initialization, command palette, notification toast, and portal hosts in the main layout.[^17][^18][^21][^22]

The direct-SSE / YARP split is reinforced in both `Program.cs` and `appsettings.json`, and the AppHost injects both URLs so local orchestration remains consistent with the BFF design.[^3][^17]

#### Framework transport

At the framework layer:

- `RunAgentInput` carries `threadId`, `runId`, `state`, `messages`, `tools`, `context`, and `forwardedProps`;
- `MapAGUI` converts it into `ChatClientAgentRunOptions` and forwards AG-UI context/state into `AdditionalProperties`;
- `AGUIServerSentEventsResult` emits native SSE ids and attempts to stream a `RunErrorEvent` on failures;
- `AGUIChatClient` always sends full message history, extracts JSON state from the final message, and preserves AG-UI thread identity via temporary additional properties;
- `ChatResponseUpdateAGUIExtensions` converts streaming tool calls, tool results, text, state snapshots, and state deltas to and from AG-UI events.[^9][^10][^11][^12]

That framework split is important for the issue rewrite: **AG-UI/MAF already provides the transport/event substrate; AGUIDojo’s remaining work is mostly about how the sample composes, consumes, documents, and validates it.**[^9][^10][^11][^12]

### 3. AG-UI / MAF feature mapping in the current sample

| Capability | Current server implementation | Current client implementation | Why this broadens the issue |
| --- | --- | --- | --- |
| Agentic chat | Unified tool-bearing agent at `/chat` with one system prompt and one tool registry | `AgentStreamingService` drives the single streaming loop | Baseline chat is already unified; future work should preserve that shape, not regress to per-feature endpoints |
| Backend tool rendering | `ToolResultStreamingChatClient` surfaces `FunctionResultContent` to the stream | `ToolComponentRegistry` + `ChatMessageItem` render visual tool results | Tool realism is tied to rendering fidelity, not only server code |
| Human-in-the-loop | `ApprovalRequiredAIFunction` + `ServerFunctionApprovalAgent` transform approvals in and out of AG-UI-compatible content | risk assessment, autonomy policy, pending approval UI, notifications, audit trail | Sensitive tools need governance and audit, not just implementation |
| Agentic generative UI | `AgenticUIAgent` emits typed plan snapshots and JSON Patch deltas | plan state parsing, `JsonPatchApplier`, diff previews, plan sheet | Planning is already a cross-cutting end-to-end flow |
| Tool-based UI rendering | unified tool registry includes chart/data-grid/form tools | tool registry + dynamic component rendering + canvas placement rules | some tools are demos because they exist to exercise client rendering patterns |
| Shared state | `SharedStateAgent` consumes `ag_ui_state`, performs a structured state update pass, emits a state snapshot, then asks for a summary | `StateManager`, recipe snapshot extraction, recipe editor artifact, session-scoped state | shared-state realism includes schema/design decisions, not just tool code |
| Predictive state updates | `PredictiveStateUpdatesAgent` intercepts `write_document` calls and streams document preview snapshots | document preview / diff tab, preview finalization, Monaco rendering | document tools are coupled to predictive UX and undo/checkpoint behavior |

This is the strongest reason the issue should no longer be server-centric: every major AG-UI feature already spans server wrappers, client routing/rendering, and framework transport assumptions.[^5][^7][^13][^14][^15][^16][^20][^21][^23]

### 4. Tool maturity: the issue is right to call out gaps, but the gaps are not all the same

The current tool inventory splits into three distinct categories.

#### A. DI-backed wrappers with simulated business services

`WeatherTool`, `EmailTool`, and `DocumentTool` are already structured like “real” tools:

- they are DI-backed,
- resolve scoped services through `IHttpContextAccessor`,
- and are intended to be called inside HTTP request scope.[^24]

But their backing services remain simulated:

- `WeatherService` sleeps and returns hardcoded weather,
- `EmailService` returns a success string,
- `DocumentService` returns `"Document written successfully"` and relies on predictive state wrappers for the visible UX.[^25]

This means the issue should not simply say “implement tools.” It should distinguish **tool wrapper maturity** from **backend/service realism**.

#### B. Synthetic demo tools for visualization / UI patterns

`ChartTool`, `DataGridTool`, and `DynamicFormTool` are currently sample-data generators by design. They delay briefly, normalize inputs, and return canned chart, table, or form payloads meant to exercise the client rendering surfaces.[^26]

That raises an important product question for the issue rewrite:

- Should these remain intentionally synthetic demo tools?
- Or should the roadmap promote some/all of them into “real” domain-backed flows?

Those are different goals and should not be mixed into one vague implementation task.

#### C. Capability-scaffolding tools

`create_plan` and `update_plan_step` are not business integrations at all; they are scaffolding for the agentic planning feature and its client UI. Treating them the same way as weather/email/document tools would blur the distinction between protocol/UI capability work and backend realism work.[^5][^13]

### 5. The client already contains substantial capability that the issue should explicitly acknowledge

The sample is not waiting for a client implementation. It already has one.

#### Multi-session state and background behavior

`SessionManagerState` is explicitly multi-session, initializes a default session, and caps retained sessions. `SessionState` tracks per-session plans, approvals, artifacts, documents, audit trail, and undo state. Reducers promote data grid, diff, recipe, document, and audit artifacts into the active session model and distinguish foreground vs background streaming status.[^19]

`AgentStreamingService` enforces a queue/concurrency model, tracks metrics, handles approval requests, writes audit entries, applies state updates, promotes data-grid artifacts, and surfaces background-session approval notifications. That is already much broader than “tool implementation.”[^20]

#### Artifact rendering and dual-pane UX

`CanvasPane.razor` already renders:

- plan indicators and a plan sheet trigger,
- diff preview artifacts,
- data grid artifacts,
- recipe editor,
- document preview / Monaco diff,
- and audit trail tabs.[^21]

`PlanSheet.razor` already provides a dedicated overlay for the plan. `CommandPalette.razor` already supports global keyboard invocation, session switching, tool discovery, new-chat, theme toggle, and sidebar toggle. `ChatMessageList.razor` already supports virtualization, and `ChatMessageItem.razor` already renders dynamic visual tool results and confidence badges.[^22]

#### State decoding is already non-trivial

The client is already doing meaningful typed-state work:

- `ChatHelpers` disambiguates plan snapshots, plan deltas, document previews, typed envelopes, and rejection responses;
- `StateManager` heuristically extracts recipe snapshots from JSON content;
- `JsonPatchApplier` applies incremental plan updates to the local model.[^23]

This means any issue rewrite should explicitly call out **client-side state routing and artifact integrity** as first-class concerns.

### 6. Some adjacent, non-AG-UI surfaces are also part of the real scope

The user explicitly asked that “harnessing” not be limited to AG-UI feature implementation. The current code supports that broader reading.

#### Business APIs and BFF plumbing

The sample still exposes plain business APIs. The BFF has a typed weather client that calls `/api/weather/{location}` through YARP, and the sample root README still documents curl checks for backend and proxied weather endpoints. That means some backend realism work may belong in normal HTTP APIs as much as in AG-UI tool wrappers.[^2][^27]

#### Auth, health, and operational UX

The server includes health checks and a development-only JWT token endpoint. The AppHost depends on `/health` to gate startup, and the root README treats health/API checks as part of the sample’s normal operating model. That makes reliability and environment setup part of the roadmap, not peripheral concerns.[^2][^3][^4][^28]

#### Observability

Both server and client wire OpenTelemetry. On the client, SSE-specific meters record first-token latency, stream duration, event counts, and retry attempts. Observability is therefore already present as sample behavior and should be part of any “harnessing” acceptance story.[^4][^17][^20]

### 7. Documentation reliability: not all current docs are equally trustworthy

The codebase contains both current and stale internal documents.

#### Current or current-ish

- `README.md` at the sample root is the most accurate high-level description of the current architecture: one `/chat` route, direct SSE, YARP for `/api/*`, multi-session client state, artifact rendering, and AppHost orchestration.[^2]
- `.docs/implementation-plan-v3/AGUIDOJO_V3_VALIDATION_INDEX.md` is useful for status/correction context and cites recent commits and passing test counts, but those test totals were not re-run during this research pass, so it should be treated as a validated historical note rather than live execution proof.[^30]

#### Stale or historical-context only

- `AGUIDojoClient/README.md` still describes a client that switches between **7 AG-UI endpoints**, still documents old endpoint names, and still claims a `WeatherDisplay` limitation that is contradicted by the current `ToolResultStreamingChatClient` + dynamic component rendering path.[^7][^21][^29]
- `.docs/ag-ui-dojo-server.md` still analyzes the older `dotnet/samples/AGUIDojo/...` path and enumerates multiple AG-UI endpoints instead of the current single `/chat` pipeline.[^31]
- `.docs/ag-ui-maf-research.md` likewise describes the older multi-endpoint sample topology.[^32]
- `.docs/spec-critique.md` is useful as a record of earlier architectural critique, but many of its observations explicitly target the pre-unified-endpoint shape and other earlier assumptions; it should not be used as present-state truth without code verification.[^33]

This matters for the issue rewrite: one of the concrete deliverables should be **documentation consolidation around the unified `/chat` architecture**.

## Grounding each aspect of the current plan

### `inventory-current-state`

Grounded conclusion: the current state is already a full end-to-end sample with:

- AppHost orchestration,
- unified `/chat`,
- shared business APIs,
- typed tool/state wrappers,
- multi-session Fluxor state,
- governance,
- artifacts,
- persistence helpers,
- and focused tests.[^2][^3][^4][^5][^17][^19][^20][^30]

This part of the plan is now well-supported by live code.

### `reframe-issue-scope`

Grounded conclusion: the issue should be reframed as a **sample-wide roadmap**. The title/path can stay if desired, but the body should stop implying that the work is only about `AGUIDojoServer/Tools` or only about AG-UI protocol features. The real scope includes:

- backend realism,
- client event/rendering integrity,
- operational plumbing,
- docs refresh,
- and validation.[^1][^2][^4][^17][^20]

### `enrich-architecture-and-gap-analysis`

Grounded conclusion: the gap analysis should be split into at least these tracks:

1. **Backend realism gaps**
   - simulated weather/email/document services,
   - unclear intent for chart/data-grid/form tools (demo vs real).[^25][^26]
2. **Client/event integrity gaps**
   - every promoted tool must still route correctly into existing visual/artifact surfaces,
   - state payload typing/disambiguation remains a meaningful concern.[^20][^21][^23]
3. **Documentation gaps**
   - multiple internal docs still describe old endpoint topology or outdated limitations.[^29][^31][^32][^33]
4. **Validation gaps**
   - useful coverage exists, but the validation docs themselves list remaining gaps such as hydration/circuit reconnect, attachment UX coverage, deeper approval lifecycle coverage, and observability assertions.[^30]

### `define-phased-roadmap`

Research-backed phase ordering:

1. **Clarify tool intent**
   - explicitly mark each tool as either `real-backend target`, `sample/demo generator`, or `feature scaffolding`.
2. **Promote real-backend tools**
   - weather/email/document first, because they already have DI/service seams and clearer real-world analogues.
3. **Preserve end-to-end UI behavior**
   - validate tool result rendering, approvals, plan/document/recipe state routing, and artifact placement for each promoted tool.
4. **Refresh docs to unified `/chat`**
   - root docs, client/server docs, issue body, and AG-UI/MAF feature explanation.
5. **Expand validation where promoted behavior changed**
   - focused server tests, client tests, and—if later needed—higher-level end-to-end checks.[^5][^20][^21][^25][^26][^30]

### `add-acceptance-criteria-and-open-questions`

#### Candidate acceptance criteria for the future issue rewrite

- The issue body explicitly describes the sample as a unified `/chat` architecture rather than multiple feature endpoints.[^2][^4][^29][^31][^32]
- Every tool is classified as either production-like, intentionally demo/synthetic, or scaffolding-only.[^25][^26]
- Every promoted tool has a documented end-to-end path: server execution, streamed/event payload shape, client render surface, and validation target.[^5][^20][^21][^23]
- Sensitive tools (for example email) keep approval/audit behavior intact.[^14][^20]
- Documentation no longer relies on stale multi-endpoint descriptions or obsolete client limitations.[^29][^31][^32][^33]
- Validation notes are refreshed after material behavior changes, and the relevant focused tests exist or are updated.[^30]

#### Candidate open questions

1. Which tools are supposed to become truly “real,” and which should remain deliberately synthetic sample generators?
2. Should `show_chart`, `show_data_grid`, and `show_form` be promoted into domain-backed workflows, or preserved as protocol/UI demonstrations?
3. Should `/chat` remain open by default for demo convenience, or should optional auth become part of the sample story once more business-realistic tools are added?
4. How much persistence/reconnect behavior should be guaranteed for artifacts, approvals, and in-flight streams?
5. Is the primary folder-organization goal **teaching AG-UI capabilities** or **supporting maintainable domain-backed integrations**?

## Folder-organization recommendation

The current server layout is already capability-aware:

- `AgenticUI/`
- `HumanInTheLoop/`
- `Multimodal/`
- `PredictiveStateUpdates/`
- `SharedState/`
- `Api/`
- `Tools/`
- `Services/`[^34]

That is a strong signal that AGUIDojoServer already separates:

- **protocol/capability wrappers** from
- **tool/service implementations**.

Because the runtime is now a single unified pipeline, I would **not** recommend collapsing everything into per-feature endpoint folders. That model reflects the old sample, not the current one.

A grounded refinement would be:

- keep wrapper/cross-cutting protocol directories as-is (`AgenticUI`, `HumanInTheLoop`, `PredictiveStateUpdates`, `SharedState`, `Multimodal`);
- reorganize `Tools/` and `Services/` by **domain or maturity**, for example:
  - `Tools/Operational` or `Domains/Weather`, `Domains/Email`, `Domains/Documents`
  - `Tools/Demo` or `Domains/Visualization`
  - shared contracts/models colocated with the domain they serve.

That preserves the sample’s current architectural truth: **capabilities are layered in the agent pipeline, while tools/services are the domain seams that can become more realistic over time.**[^5][^24][^25][^26]

## Key repositories summary

| Repository / project | Purpose | Key files |
| --- | --- | --- |
| [arisng/agent-framework](https://github.com/arisng/agent-framework) — `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer` | Unified AG-UI backend, business APIs, health/auth, tool/service seams | `Program.cs`, `ChatClientAgentFactory.cs`, `ToolResultStreamingChatClient.cs`, `ContextWindowChatClient.cs`, wrapper directories |
| [arisng/agent-framework](https://github.com/arisng/agent-framework) — `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient` | Blazor Server BFF, multi-session state, approvals, artifacts, rendering, persistence | `Program.cs`, `AGUIChatClientFactory.cs`, `AgentStreamingService.cs`, `ToolComponentRegistry.cs`, `CanvasPane.razor`, `SessionState.cs` |
| [arisng/agent-framework](https://github.com/arisng/agent-framework) — `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojo.AppHost` | Local orchestration for sample startup and endpoint wiring | `AppHost.cs`, `AGUIDojo.AppHost.csproj` |
| [arisng/agent-framework](https://github.com/arisng/agent-framework) — `dotnet/src/Microsoft.Agents.AI.AGUI` | AG-UI client transport, event conversion, request envelope | `AGUIChatClient.cs`, `Shared/RunAgentInput.cs`, `Shared/ChatResponseUpdateAGUIExtensions.cs` |
| [arisng/agent-framework](https://github.com/arisng/agent-framework) — `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | ASP.NET Core `MapAGUI` endpoint mapping and SSE emission | `AGUIEndpointRouteBuilderExtensions.cs`, `AGUIServerSentEventsResult.cs` |
| [arisng/agent-framework](https://github.com/arisng/agent-framework) — `dotnet/samples/05-end-to-end/AGUIDojo/*Tests` | Focused server and client validation surfaces | `AGUIDojoServer.Tests`, `AGUIDojoClient.Tests`, validation docs under `.docs/implementation-plan-v3/` |

## Confidence assessment

- **High confidence** on the current runtime architecture, wrapper composition, client artifact/governance surfaces, and the conclusion that the issue must be broadened to sample scope. Those findings are grounded in current code and the current root README.[^2][^3][^4][^5][^17][^20][^21]
- **Medium confidence** on the exact current passing test totals because this research pass did not rerun the suite; those numbers come from the latest validation notes rather than fresh command execution.[^30]
- **High confidence** that several internal docs are stale, because they directly describe the older multi-endpoint topology and outdated client behavior that current code contradicts.[^29][^31][^32][^33]

## Footnotes

[^1]: `dotnet/samples/05-end-to-end/AGUIDojo/.issues/260321_harness-agui-features-in-aguisever.md:7-12`.
[^2]: `dotnet/samples/05-end-to-end/AGUIDojo/README.md:3-5`, `7-15`, `16-72`, `98-118`, `134-136`.
[^3]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojo.AppHost/AppHost.cs:3-24`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojo.AppHost/AGUIDojo.AppHost.csproj:6-18`.
[^4]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Program.cs:283-340`, `342-352`, `453-477`.
[^5]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/ChatClientAgentFactory.cs:45-72`, `152-226`.
[^6]: `dotnet/src/Microsoft.Agents.AI/AIAgentBuilder.cs:49-68`, `72-93`.
[^7]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/ToolResultStreamingChatClient.cs:14-38`, `52-107`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Components/Pages/Chat/ChatMessageItem.razor:115-126`.
[^8]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/ContextWindowChatClient.cs:13-25`, `59-148`.
[^9]: `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs:31-88`.
[^10]: `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIServerSentEventsResult.cs:30-92`.
[^11]: `dotnet/src/Microsoft.Agents.AI.AGUI/AGUIChatClient.cs:90-137`, `190-276`; `dotnet/src/Microsoft.Agents.AI.AGUI/Shared/RunAgentInput.cs:13-38`.
[^12]: `dotnet/src/Microsoft.Agents.AI.AGUI/Shared/ChatResponseUpdateAGUIExtensions.cs:25-141`, `340-506`.
[^13]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/AgenticUI/AgenticUIAgent.cs:34-109`.
[^14]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/HumanInTheLoop/ServerFunctionApprovalAgent.cs:49-69`, `140-252`.
[^15]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/PredictiveStateUpdates/PredictiveStateUpdatesAgent.cs:47-119`.
[^16]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/SharedState/SharedStateAgent.cs:34-148`, `171-205`.
[^17]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Program.cs:78-124`, `141-229`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/appsettings.json:14-47`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/AGUIChatClientFactory.cs:8-44`.
[^18]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/AGUIDojoClient.csproj:3-34`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Components/Layout/MainLayout.razor:4-17`.
[^19]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Store/SessionManager/SessionManagerState.cs:8-61`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Store/SessionManager/SessionState.cs:57-122`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Store/SessionManager/SessionReducers.cs:323-523`.
[^20]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/AgentStreamingService.cs:16-23`, `27-47`, `430-617`, `873-1085`.
[^21]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/ToolComponentRegistry.cs:180-233`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Components/Pages/Chat/ChatMessageItem.razor:100-163`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Components/Layout/CanvasPane.razor:10-176`.
[^22]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Components/Layout/PlanSheet.razor:12-85`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Components/SharedState/CommandPalette.razor:18-166`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Components/Pages/Chat/ChatMessageList.razor:1-18`, `32-110`.
[^23]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/StateManager.cs:65-126`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Helpers/ChatHelpers.cs:19-79`, `107-160`, `195-275`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/JsonPatchApplier.cs:27-123`.
[^24]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Tools/WeatherTool.cs:8-50`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Tools/EmailTool.cs:8-54`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Tools/DocumentTool.cs:8-50`.
[^25]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Services/WeatherService.cs:5-35`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Services/EmailService.cs:5-20`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Services/DocumentService.cs:5-20`.
[^26]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Tools/ChartTool.cs:7-107`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Tools/DataGridTool.cs:7-124`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Tools/DynamicFormTool.cs:7-109`.
[^27]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/IWeatherApiClient.cs:5-47`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/WeatherApiClient.cs:30-54`.
[^28]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Api/AuthEndpoints.cs:11-48`, `57-132`.
[^29]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/README.md:3-4`, `27-32`, `41-48`, `57-60`, `170-220`.
[^30]: `dotnet/samples/05-end-to-end/AGUIDojo/.docs/implementation-plan-v3/AGUIDOJO_V3_VALIDATION_INDEX.md:1-23`, `40-48`, `51-119`, `123-140`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer.Tests`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient.Tests`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient.Tests/Services/AgentStreamingServiceTests.cs:13-75`; `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient.Tests/Services/SessionPersistenceServiceTests.cs:10-104`.
[^31]: `dotnet/samples/05-end-to-end/AGUIDojo/.docs/ag-ui-dojo-server.md:1-16`, `60-92`.
[^32]: `dotnet/samples/05-end-to-end/AGUIDojo/.docs/ag-ui-maf-research.md:37-59`.
[^33]: `dotnet/samples/05-end-to-end/AGUIDojo/.docs/spec-critique.md:30-55`, `82-118`, `123-137`, `208-219`.
[^34]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer` directory listing in current workspace session: `AgenticUI`, `Api`, `HumanInTheLoop`, `Multimodal`, `PredictiveStateUpdates`, `Services`, `SharedState`, `Tools`, and related runtime files.
