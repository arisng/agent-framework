<!-- MY CUSTOMIZATION POINT: realign the system design doc with the current full-history /chat behavior -->

# AGUIDojo system design

This document consolidates the legacy AGUIDojo design notes into one implementation-aligned view. It explains what the sample does today, the chat continuity invariants that matter now, and the server-owned session and model-selection architecture the current implementation plan is targeting next.

For the supporting model-picker, persistence, MAF boundary, and Copilot-overlap material that informs this design—including the repo-grounded validation pass plus the companion session-state schema reference, session-topology note, the focused continuity note, and the implemented server-owned lifecycle note—see [AGUIDojo LLM picker architecture and MAF alignment](./aguidojo-llm-picker-architecture-and-maf-alignment.md), [server-side primary persistence for AGUIDojo chat sessions](./server-side-persistence-for-chat-session.md), [server-owned chat session lifecycle and canonical ID flow](./server-owned-chat-session-lifecycle-and-canonical-id-flow.md), [chat continuity and re-entry invariants](./chat-continuity-and-re-entry-invariants.md), [Copilot CLI patterns relevant to AGUIDojo](../copilot/copilot-cli-session-context-and-instruction-patterns.md), [Copilot CLI public repo grounding for AGUIDojo](../copilot/copilot-cli-public-repo-grounding.md), [Copilot CLI session state schema reference](../../reference/copilot/copilot-cli-session-state-schema.md), and [Copilot CLI session topology and orchestration layer](../copilot/copilot-cli-session-topology.md).

## 1. Scope and status

**CURRENT**
- AGUIDojo is a unified `/chat` sample. `AGUIDojoClient` is a Blazor Server BFF, `AGUIDojoServer` is a Minimal API backend, and `AGUIDojo.AppHost` wires both together.
- `AGUIDojoClient` sends AG-UI traffic directly to `AGUIDojoServer /chat`. YARP proxies only `/api/*` business endpoints.
- Client session state lives in Fluxor and is hydrated from browser storage, while the server now owns a thin chat-session catalog for identity, lifecycle, and cross-browser list recovery.
- Model selection is process-wide today. `ChatClientAgentFactory` creates one provider-bound `ChatClient` from startup configuration and all sessions use it.
- Context handling is still transitional, but the baseline continuity invariant is already restored: the client sends the full active branch on each `/chat` turn, while the server still uses `ContextWindowChatClient` with a fixed 80 non-system-message cap rather than a model-aware token policy.
- Browser storage is a convenience cache, not the system of record, while the server now owns a thin chat-session catalog for list/detail/archive recovery.
- `/chat` is the canonical server shape. The older seven-endpoint AG-UI layout is legacy only.

**TARGET**
- Phase 0's continuity invariant is now the baseline: send full active-branch history on every `/chat` turn and keep prompt trimming as explicit server-owned policy.
- Phase 1+ moves primary session ownership into `AGUIDojoServer` via a Chat Sessions module backed by a SQL-first, relational-first store. SQLite may remain useful for local sample runs, but SQL Server or PostgreSQL are the natural modular-monolith targets.
- Server-owned session records are more than transcript storage: AGUIDojo needs both a read-optimized catalog/index surface for list/resume/search and a richer session-detail/workspace surface for approvals, plans, checkpoints, files/artifacts, audit facts, and compaction/debug history.
- Copilot CLI's recent public-doc research plus the companion schema reference and topology note are useful mainly as a topology lesson: separate a central session catalog/index from richer per-session detail/workspace surfaces, but do not copy the literal `~/.copilot/session-state/<id>/...` filesystem layout into AGUIDojo. In this sample, both surfaces stay server-owned and SQL-first.
- AGUIDojo remains a chat module inside a modular monolith: chat sessions link to business-module subjects (start with Todo), while business data stays owned by those modules.
- Per-session model preference becomes part of session metadata and later part of server-owned session records; requested/preferred model and effective model stay distinct facts.
- `/chat` remains the only AG-UI route; requested model should travel as per-request metadata on that route rather than spawning per-model endpoints.
- Context-window policy moves fully server-side: the client sends the full active branch, the server selects the effective model, and checkpointed compaction happens on invocation context without rewriting canonical history.
- Auth and ownership remain simulated only.
- `ConversationId`, `threadId`, `runId`, and workflow/Durable Task IDs remain correlation-only.

## 2. Current sample architecture

```text
Browser
  |
  v
AGUIDojoClient (Blazor Server BFF)
  |- Fluxor session store + browser persistence
  |- direct AG-UI streaming to AGUIDojoServer /chat
  `- YARP proxy for /api/*
        |
        v
AGUIDojoServer (Minimal API)
  |- POST /chat              unified agent route
  |- /api/weather/*          shared business APIs
  |- /api/email
  |- /api/title
  |- /api/files              attachment upload/retrieval
  |- /api/auth/dev/token     dev helper only
  `- /health
```

**CURRENT**
- `AGUIDojoClient` is intentionally a BFF. It owns UI composition and forwards business API traffic through YARP, but `/chat` bypasses YARP so SSE streaming is not buffered.
- `AGUIDojoServer` maps `/api/*` business endpoints and one `app.MapAGUI("/chat", ...)` endpoint backed by a single unified agent.
- `AGUIDojo.AppHost` injects the server URL twice: once for the direct `/chat` client and once for the YARP backend destination.
- Multimodal support is thin but real: files are uploaded through `/api/files`, stored durably in the server SQLite database with retention cleanup, and resolved into `DataContent` by `MultimodalAttachmentAgent` before model execution.

**CURRENT AG-UI feature coverage**
AGUIDojo folds the legacy feature demos into one route. Today that unified pipeline covers:
1. agentic chat
2. backend tool rendering
3. human-in-the-loop approval
4. agentic generative UI
5. tool-based UI rendering
6. shared state
7. predictive state updates

That is the main reason `/chat` exists: the sample is demonstrating one combined agent surface, not a menu of separate protocol endpoints. `MapAGUI` is the transport boundary; the execution model behind it is a MAF `AIAgent` pipeline.

## 3. Request and streaming flow

**CURRENT**
1. `Chat.razor` adds the user message to the active session and asks `AgentStreamingService` to process the response.
2. `AgentStreamingService` flattens the active branch from the session's `ConversationTree`, reuses `ChatOptions.ConversationId` when present, and calls `AGUIChatClient.GetStreamingResponseAsync(...)` against `SERVER_URL/chat`.
3. `AGUIDojoServer/Program.cs` maps `/chat` to `ChatClientAgentFactory.CreateUnifiedAgent(...)`.
4. The unified pipeline currently includes:
   - `ContextWindowChatClient` with a fixed 80-message non-system cap (simple sliding window, not model-aware)
   - `ToolResultStreamingChatClient`, which emits `FunctionResultContent` into the stream instead of hiding it inside tool invocation
   - `MultimodalAttachmentAgent`
   - `ServerFunctionApprovalAgent`
   - `AgenticUIAgent`
   - `PredictiveStateUpdatesAgent`
   - `SharedStateAgent`
   - OpenTelemetry instrumentation
5. The server streams text, tool calls, tool results, approval requests/responses, plan snapshots/deltas, recipe/shared-state snapshots, document preview snapshots, author metadata, and `ConversationId`.
6. The client turns those updates into session-state mutations and renders them in either the message list or the artifact canvas.

### Model-selection and context-policy direction

**TARGET**
- The client remains a session-oriented UI shell, but the model picker is only a preference/control surface. It should not become the place where token budgets are enforced.
- The transport seam stays on the existing `/chat` request. In MAF terms, the intended shape is AG-UI forwarded/request metadata (`RunAgentInput.ForwardedProperties` on the wire, then `ChatOptions.AdditionalProperties["ag_ui_forwarded_properties"]` at the server boundary).
- Because the current `AGUIChatClient` does not automatically forward arbitrary `ChatOptions.AdditionalProperties`, AGUIDojo will likely need a thin client-side extension or wrapper when the model picker is added.
- The server-side model-routing seam is per-request chat-client selection. `ChatOptions.ModelId` is useful companion metadata, but provider-bound OpenAI/Azure clients still require swapping the underlying `IChatClient`; the relevant MAF seam is `ChatClientAgentRunOptions.ChatClientFactory` or equivalent per-request factory logic around the agent run.
- Requested/preferred model should travel with the session/request, but the effective model is the provider/model that actually serves a turn and should be streamable/persistable as turn or audit metadata when available.
- The current `ContextWindowChatClient` is a transitional wrapper. The research-backed target is a server-side compaction pipeline (`CompactionProvider` plus `PipelineCompactionStrategy`) that builds model-invocation context with checkpointed summaries and background compaction rather than destructive transcript trimming.
- AGUIDojo should keep more reserve than Copilot CLI's public ~95% token-limit behavior; exact thresholds are a model-tier implementation detail after subtracting output and system/tool reserves.
- The canonical active branch remains the durable business record. Compaction checkpoints and audit facts make context decisions explainable without flattening session history.
- If AGUIDojo adopts MAF compaction, it should choose the `AIContextProviders` mount point deliberately so behavior during tool-call loops is explicit and testable.
- Client-side history slicing is explicitly the wrong long-term boundary. The server needs the full active branch before it can decide whether to summarize, slide, or truncate.

**Why this wrapper stack exists**
- Tool execution is server-side, but tool results still need to be visible in the AG-UI stream.
- Approval requests need protocol translation between server-side function approval content and the client-visible approval tool pattern.
- Shared state and predictive UI need `DataContent` projections, not just final text.
- The sample is modeling one end-to-end agent pipeline, not a thin chat relay.

## 4. Client session model and artifact surfaces

### Session model

**CURRENT**
- The root Fluxor store (`SessionManagerState`) holds many `SessionEntry` objects plus the active session ID and global autonomy level.
- Each `SessionEntry` splits into:
  - `SessionMetadata`: title, status, created/last-activity timestamps, unread count, pending-approval badge, and a legacy `EndpointPath` hint.
  - `SessionState`: branching `ConversationTree`, current active-branch messages, in-progress assistant message, `ConversationId`, approval state, artifact state, audit trail, and undo state.
- Session switching is first-class. The sidebar changes the active session, clears unread count for the foreground session, and lets other sessions continue streaming in the background.
- Status values such as `Created`, `Active`, `Streaming`, `Background`, `Completed`, `Error`, and `Archived` are UI/session lifecycle markers, not server-owned records.
- Concurrency is managed client-side today: up to three active streams, with a small queue for overflow.

**TARGET**
- Session metadata should gain model-preference information (for example model ID and display name) without changing the single `/chat` route shape.
- The client may cache and render that preference, but once server-owned sessions exist the server becomes the authority for both the selected model and the effective model that actually served each turn.
- Once server-owned sessions exist, the client remains a renderer/cache for session state while the durable record moves server-side: session identity, approvals, effective-model facts, compaction checkpoints, and other support/debug artifacts belong there.
- The client keeps its session-scoped rendering role; it does not become the owner of token budgeting, compaction thresholds, or downgrade safety.

### Artifact rendering model

**CURRENT**
- The chat transcript renders the active branch only, but the underlying conversation structure is a client-side tree so branch switching and edit/regenerate are possible.
- `ChatMessageItem` sorts content as function calls -> function results -> data content -> text -> errors, then splits it into:
  - **inline visual content** in the message list
  - **non-visual thought/debug content** in `AssistantThought`
  - **plain text** rendered as markdown
- `ToolComponentRegistry` decides whether a tool result is shown inline or moved to the canvas pane.

### Artifact surfaces

**CURRENT**
- **Plans**: compact progress in the canvas header plus a full `PlanSheet`
- **Diff preview**: canvas tab
- **Recipe/shared state**: canvas tab with `RecipeEditor`
- **Document preview**: canvas tab with markdown preview or Monaco diff
- **Data grid**: canvas tab
- **Audit trail**: canvas tab
- **Weather / charts / forms**: inline visual tool results in the message list
- **Approvals**: inline approval UI plus background-session notifications

### Notification behavior

**CURRENT**
- Background sessions can finish, fail, or pause for approval while another session is active.
- `AgentStreamingService` emits session-scoped notifications for completion, approval required, and streaming error.
- `NotificationToast` opens the related session when the user clicks the toast.
- The session list also surfaces unread counts and pending-approval badges.

### BlazorBlueprint v3/UI role

**CURRENT**
- BlazorBlueprint is the rendering shell, not the protocol. Its Tabs, Sheet, Badge, Button, Sidebar, and dialog primitives turn session projections into artifact surfaces.
- The important design point is not the specific control library; it is that AG-UI/tool events are projected into stable client-side session artifacts instead of being treated as raw SSE text.

## 5. Current persistence boundary and limitations

**CURRENT source of truth**
- The live session state is the Fluxor store plus the current server stream.
- Browser persistence is a convenience layer.

**CURRENT browser persistence**
- **localStorage**
  - session metadata list
  - active session ID
- **IndexedDB**
  - conversation tree per session

**CURRENT limitations**
- Persistence is lossy by design:
  - `ConversationNodeDto` stores role, author name, text, parent/child links, and timestamps, but not full-fidelity AI content.
  - Approval state is not durably restored.
  - Audit history is not durably restored.
  - Plan state, diff preview, recipe state, document state, data grids, and undo state are not durably restored.
  - Hydrated sessions reset transient statuses back to `Completed`, clear unread counts, and clear pending approvals.
- Session archive is now mirrored into a server-owned lifecycle API and removed from the server session catalog; client cleanup still deletes the local cached conversation tree afterward.
- Browser storage is therefore **not** a system of record. It cannot provide authoritative history, cross-device resume, or durable governance/audit data.
- Attachment persistence now survives ordinary server restarts: uploaded files live in a SQLite-backed attachment table, while browser-stored message markers continue to reference them through `/api/files/{id}` until the server-side retention window expires.
- There is no per-session model preference or compaction state to restore; the effective model is simply whatever the server process was configured with at startup.

**TARGET**
- Browser persistence becomes cache, draft, and best-effort import support only.
- Durable session ownership moves to the server, and the durable session becomes a support/debug artifact as well as resume state.
- The current intermediate state is intentional: the server already owns the chat-session catalog/index while richer branching/detail durability is still phased in behind that catalog.
- Server-owned session records eventually hold catalog fields for list/resume/search plus durable detail/workspace surfaces for requested-model metadata, effective-model facts, plan state, checkpoints, files/artifacts, runtime correlations, and audit/support artifacts rather than only a flattened transcript.

## 6. Chat continuity invariant and why it matters

**CURRENT baseline**
`AgentStreamingService` now sends the session's full active branch on every `/chat` turn:

```csharp
chatClient.GetStreamingResponseAsync(
    session.Messages,
    context.ChatOptions,
    responseCancellation.Token)
```

`SessionState.Messages` is derived from `ConversationTree.GetActiveBranchMessages()`, so the outbound history is always the current root-to-leaf branch rather than a client-side tail slice. That same rule applies after edit-and-regenerate branch swaps, checkpoint restores, approval submit/reject flows, and ordinary same-session continuation.

**Why this matters**
- In AGUIDojo's `/chat` contract, `ConversationId` is correlation metadata, not proof that the backend already owns complete conversational memory.
- The same is true for AG-UI `threadId`, AG-UI `runId`, and downstream workflow/runtime identifiers: they are useful links, not permission to omit history.
- The server-side agent wrappers still need full active-branch history to interpret follow-up questions, previous tool results, approval decisions, and plan/recipe/document state already discussed in the branch.
- Safe prompt-size management depends on the server seeing canonical history first. Only then can the server choose whether to summarize, slide, truncate, or otherwise compact invocation context.

**Required invariant**
- Always send the full active-branch history on every `/chat` turn.
- Keep context-window trimming as an explicit server policy, not a client-side skip heuristic.
- Apply the same rule to ordinary follow-ups plus edit/regenerate, retry/restart, approval submit/reject, and other re-entry paths.
- Treat future compaction as checkpointed server behavior: it may shrink invocation context, but it must not silently rewrite the canonical branch history.
- Guard the invariant with regression tests before layering server-owned sessions or model-routing behavior on top.

## 7. Target session architecture

**TARGET**
The next real architecture step is not "more tools." It is a server-owned Chat Sessions module inside `AGUIDojoServer`.

### Target model-selection and context policy

- Per-session model selection is part of the intended chat-session experience, but it stays inside the same unified `/chat` contract.
- AGUIDojo will need an application-owned model catalog / registry because MAF does not provide model-to-context-window metadata.
- The client-side model picker should read from a small server model catalog, store the user's requested/preferred model per session, and send that preference with each `/chat` turn.
- The server remains authoritative: it routes the request to the effective provider client, the effective model may differ from the requested model because of routing/availability/policy, and that effective `ModelId` should be streamable and persistable per assistant turn or audit event when available.
- When a session switches to a smaller-context model, the server must re-evaluate the current branch against that model's safe input budget before the provider call.
- Ordered server-side compaction (tool-result collapse -> summarization -> sliding window -> truncation, or equivalent) is the intended policy shape for invocation context, but the canonical branching conversation and its checkpoints stay durable.
- Compaction should be inspectable and background-friendly where possible: summaries/checkpoints become audit/support artifacts rather than silent transcript mutation.
- AGUIDojo should keep more headroom than Copilot CLI's public ~95% trigger because tool outputs, approvals, and richer artifacts share the same session envelope; exact thresholds remain a model-tier implementation detail.
- Client UX can warn about likely downgrades, but the client should not count tokens or perform the actual compaction.

### Target instruction visibility and trust boundaries

- If workspace/project-sourced instructions, prompt augmentation, or repo guidance become part of the sample later, AGUIDojo should define one deterministic merge/order rule instead of relying on emergent file precedence.
- Server policy/system instructions stay above workspace/project/user-authored content. Workspace/project inputs can enrich context only after trust is established; they do not become implicit policy.
- Active instruction sources and trust decisions should be visible in diagnostics/UI and, when they materially affect behavior, persisted as session/audit facts.
- Uploaded files, fetched URLs, and future workspace/project instruction content are untrusted inputs until explicitly approved.

### Target durable session topology

- The paired [Copilot CLI session state schema reference](../../reference/copilot/copilot-cli-session-state-schema.md) and [session-topology note](../copilot/copilot-cli-session-topology.md) support a useful framing already hinted at by the public-repo and context-pattern research: the product appears to combine a central session catalog/index with richer per-session workspace artifacts. AGUIDojo should borrow that separation of concerns, not the on-disk folder layout.
- For AGUIDojo, that means two related server-owned surfaces:
  1. **Catalog/index surface** — read-optimized summary data for list/resume/search/ops views. This is where session identity, title/summary, created/updated timestamps, archive state, primary business-subject link, preferred model, and lightweight status/count fields belong.
  2. **Session detail/workspace surface** — the richer durable record needed to reconstruct and explain the session: canonical branching conversation history, plan snapshots, compaction checkpoints, approval records, file/artifact references, audit facts/events, and runtime correlations.
- Plans, checkpoints, files/artifacts, audit facts, and correlation links are not optional extras around the transcript. They are part of the durable session topology because they explain how the session reached its current state and what support/debug tooling must be able to recover later.
- In a SQL-first sample, the catalog/index surface can be a projection or subset of the richer detail/workspace surface. If a summary/search projection drifts, AGUIDojo should be able to rebuild it from durable detail records rather than from browser state.
- The operational split should follow the same shape: list/search/resume APIs read the catalog, while session detail/debug/export surfaces read the richer workspace view. That keeps the system supportable without importing Copilot CLI's local filesystem conventions into the server design.

### Phase shape

**Phase 0**
- Preserve and regression-test full-history turns on `/chat`.

**Phase 1**
- Add a Chat Sessions module with list/get summary-detail/archive APIs and implicit creation on the first persisted `/chat` turn.
- Move primary business session ID issuance from the client to the server.
- Use a SQL-first relational store for sample scope. SQLite may remain a local convenience, while SQL Server or PostgreSQL are the natural modular-monolith targets.
- Start with a small session catalog projection for list/resume/search and a richer detail contract for session inspection rather than treating transcript storage as the only API shape.
- Add room for model metadata in the server-owned session contract and expose a small model catalog for the future picker.
- Start with a primary business-subject link (for example `Todo`) so one Todo/business flow can have many related chat sessions without turning chat into an isolated persistence island.

**Phase 2**
- Persist the canonical branching conversation graph on the server.
- Store the active leaf/branch explicitly.
- Preserve richer message payloads than the current browser DTOs.
- Make requested model part of the `/chat` request contract via forwarded/request metadata on the single route.
- Use per-request chat-client selection on the server so different sessions can target different models without separate endpoints.
- Persist enough metadata to distinguish requested/preferred model from the effective model that actually served assistant turns.

**Phase 3**
- Persist approvals, audit entries, support/debug artifacts, plan/checkpoint surfaces, file/artifact references, correlation links, and session artifact projections/snapshots.
- Replace the fixed message-cap wrapper with a checkpointed, model-aware compaction pipeline when the sample is ready to make context policy durable and explainable.
- Capture model-switch, effective-model, compaction, and trust/approval events in audit/projection state where that materially helps the sample.
- If workspace/project-sourced guidance becomes a feature, surface active instruction sources and trust decisions before allowing that guidance to influence execution.

**Phase 4**
- Add simulated current-user/current-tenant ownership context on server records.
- Link sessions to workflows or business entities where that makes the sample more realistic.
- Keep runtime IDs as links and correlations, not as the primary session key.

**Phase 5**
- Fully demote browser storage to cache/import behavior.
- Keep the Chat Sessions model SQL-first, relational, and cloud-vendor agnostic; SQLite may remain a local convenience, while SQL Server or PostgreSQL are the natural modular-monolith targets.
- Keep README and internal design notes aligned with the architecture that is actually implemented.

### Practical target data ownership

| Concern | CURRENT | TARGET |
| --- | --- | --- |
| Session identity | client-generated, effectively browser-owned | server-issued business session ID plus durable support/audit surface |
| Session catalog / resume surface | browser metadata list only | server-owned SQL summary/read model for list/resume/search/ops views |
| Session detail / workspace surface | client conversation tree plus transient canvas/session state | durable server detail surface for branch history, plans, checkpoints, files/artifacts, approvals, audit facts, and runtime correlations |
| Model preference | no per-session setting; one server startup model for all sessions | per-session requested/preferred model owned by server records and cached in the client UI |
| Effective model | implicit process-wide startup model for all turns | per-turn or audit fact recorded by the server when known |
| Conversation graph | client-only `ConversationTree` | server-owned branching graph in SQL-backed Chat Sessions storage |
| Context-window policy | full active-branch submission plus fixed 80-message server cap | full-history submission plus server-owned, checkpointed model-aware compaction |
| Message fidelity | good live state, lossy browser persistence | durable message payloads with tool/state context |
| Approval history | transient UI/session state | durable approval records |
| Audit trail | transient UI/session state | durable audit/support records for approvals, model switches, compaction, and trust |
| Artifact state | client projection only | server snapshots/projections with client cache |
| Correlation links | transient `ConversationId` and runtime IDs in live client state | durable linked records for `ConversationId`, `threadId`, `runId`, workflow IDs, and related logs/traces when useful |
| Instruction sources / trust | not modeled as durable behavior | deterministic, visible server-owned metadata if workspace/project guidance is added |
| Browser storage | convenience persistence | cache/import only |

### Practical sample-scope store

A pragmatic SQL-backed Chat Sessions module can stay small and still demonstrate the right boundary. SQLite is still fine for local/sample execution, but the model should read naturally against SQL Server or PostgreSQL too. Following the catalog-plus-workspace framing in the [Copilot CLI session state schema reference](../../reference/copilot/copilot-cli-session-state-schema.md) and [Copilot CLI session topology note](../copilot/copilot-cli-session-topology.md), think in terms of a read-optimized session catalog plus a richer detail/workspace surface, but keep both server-owned and relational instead of copying a per-session local folder tree:
- **Catalog/index records or projections**
  - `ChatSession` (server-issued identity, title/summary, requested/preferred model metadata, archive state, last-activity fields, and primary business-subject link fields)
  - optional search/read-model projection if list/search needs more than raw `ChatSession`
- **Detail/workspace records**
  - `ConversationNode` or equivalent parent-linked message record
  - `PlanSnapshot` / `PlanState`
  - `ApprovalDecision`
  - `AuditEvent` (including effective-model, trust/approval, and support/debug facts)
  - `CompactionCheckpoint` or equivalent summary/checkpoint record tied to the canonical branch
  - `ArtifactProjection` / `ArtifactSnapshot`
  - `SessionFile` / artifact-file metadata for uploaded or generated files
  - `RuntimeLink`

Start simple: a session can point at a primary business subject (for example `Todo`), and many sessions can point at the same subject so one Todo/business flow can have many related chats.

The important point is not the exact table names. The important point is that the server, not the browser, becomes the owner of the durable business session, while Todo/business data remains owned by its module. The catalog/detail split should make support, resume, and debugging first-class without forcing AGUIDojo to mimic Copilot CLI's local filesystem layout.

## 8. Integration boundaries

**CURRENT**
- `/api/*` endpoints are shared business endpoints. The client reaches them through YARP.
- `/chat` is the direct AG-UI stream path.
- The server has auth plumbing and a dev token endpoint, but `/chat` is not protected by default.
- The wider repo contains Durable Task/workflow capabilities, but AGUIDojo does not currently use them as the chat session system of record.

**TARGET**
- Ownership/auth remains simulated only. If the sample needs user or tenant identity, use seeded/fake current-user and current-tenant context on server records.
- AGUIDojo should behave as a chat module inside the modular monolith: business entities such as Todo stay in their owning modules, and chat-linked agent actions go through module/application services on behalf of the current user.
- Start with Todo as the anchor example: one Todo/business flow can have many related chat sessions, while the Todo aggregate remains the source of truth for Todo data.
- Durable Task/workflow integration should be additive: link sessions to workflow/entity records when useful, but do not let orchestration runtime IDs become the primary session key.
- `ConversationId`, `threadId`, `runId`, Durable Task IDs, and workflow IDs stay correlation-only.
- If workspace/project-sourced guidance or imports are added later, keep them observable, trust-gated inputs to the chat module rather than implicit policy.
- Keep one `/chat` route even after model picker exists; the transport seam is per-request metadata, not a per-model endpoint matrix.
- The relevant MAF routing seam is `ChatClientAgentRunOptions.ChatClientFactory`; `ChatOptions.ModelId` can accompany the request but does not replace provider-specific client selection for OpenAI/Azure.
- The relevant MAF context seam is `CompactionProvider` plus ordered compaction strategies; keeping both that and `ContextWindowChatClient` long-term would double-trim the history.

### Identifier boundaries

| Identifier | Meaning now | Meaning later |
| --- | --- | --- |
| `sessionId` | client session key used by Fluxor and browser persistence | primary business session ID issued by server |
| `ConversationId` | AG-UI conversation correlation stored in client session state | runtime correlation only |
| `threadId` / `runId` | AG-UI runtime identifiers when surfaced by the transport | runtime correlation only |
| Durable Task / workflow IDs | downstream orchestration/runtime IDs | linked records only, never the business session key |

## 9. Non-goals and deferred items

**Not current**
- a server-owned Chat Sessions API
- server-owned SQL-backed primary session store
- durable attachment/blob storage
- cross-device session authority
- real auth or real tenant isolation
- a client-side tokenizer or client-side model-aware compaction policy

**Still deferred after the next session milestones**
- replay/collaboration features such as mid-run join, participant cursors, or fine-grained event journaling
- making browser storage lossless
- treating orchestration runtime IDs as product-facing session identity
- reviving the old multi-endpoint AG-UI server shape
- multiplying `/chat` into one endpoint per model

## 10. Key code touchpoints

- `README.md` - live sample summary and current topology
- [Roadmap issue](../../../.issues/260323_aguidojo-implementation-plan.md) - active phase ordering, rollout decisions, and resolved design choices
- `AGUIDojo.AppHost/AppHost.cs` - Aspire wiring between client and server
- `AGUIDojoClient/Program.cs` - Blazor Server BFF setup, direct `/chat` client, YARP proxy setup
- `AGUIDojoClient/Services/AGUIChatClientFactory.cs` - current AG-UI transport client creation; future forwarded model metadata likely attaches here or in a thin wrapper around it
- `AGUIDojoServer/Program.cs` - `/api/*` endpoints and unified `app.MapAGUI("/chat", ...)`
- `AGUIDojoServer/ChatClientAgentFactory.cs` - unified tool registry and wrapper pipeline
- `AGUIDojoServer/ContextWindowChatClient.cs` - current fixed message-cap wrapper that the research points toward replacing with server-side compaction
- `AGUIDojoServer/ToolResultStreamingChatClient.cs` - tool-result streaming bridge
- `AGUIDojoServer/Multimodal/MultimodalAttachmentAgent.cs` - attachment resolution into multimodal model input
- `AGUIDojoClient/Services/AgentStreamingService.cs` - direct AG-UI streaming, approvals, notifications, and the full-history submission invariant
- `AGUIDojoClient/Store/SessionManager/SessionState.cs` - per-session state model
- `AGUIDojoClient/Store/SessionManager/SessionHydrationEffect.cs` - browser hydration behavior
- `AGUIDojoClient/Store/SessionManager/SessionPersistenceEffect.cs` and `AGUIDojoClient/Services/SessionPersistenceService.cs` - localStorage/IndexedDB persistence boundary
- `AGUIDojoClient/wwwroot/js/sessionPersistence.js` - tiered browser persistence implementation
- `AGUIDojoClient/Components/Layout/CanvasPane.razor` - canvas artifact surface
- `AGUIDojoClient/Services/ToolComponentRegistry.cs` - tool-result to UI-component routing
- `AGUIDojoClient/Components/NotificationToast.razor` - background session notification UX
