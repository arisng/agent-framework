# Research: Server-side Primary Persistence for AGUIDojo Chat Sessions

## Executive summary

The best fit for AGUIDojo is **not** to keep browser storage as the source of truth, and it is also **not** to make Durable Task entity state the product-level chat database.

The best fit is a **server-owned Chat Sessions module** inside `AGUIDojoServer` today, and later inside the target modular monolith, backed by the monolith's **primary relational database** plus JSON columns for rich AI payloads and an external blob store for binaries. The browser should become a **cache/offline convenience layer**, while Durable Task / workflow persistence should remain an **execution-layer concern** for agent runs and workflow checkpoints rather than the long-term business record of conversations.[1][2][3][4][5][6][7][8][9][10][11]

This also matches the framework boundary visible in the repo: AG-UI transport carries `threadId` and `runId`, but application code is expected to own persistence, and the hosting layer already exposes an `AgentSessionStore` concept instead of baking storage directly into AG-UI transport.[18]

That recommendation matters because AGUIDojo sessions are already broader than plain text chat. A session currently carries:

- a branching conversation tree
- AG-UI conversation metadata
- pending approvals
- audit trail entries
- plans and diff previews
- recipe/shared state
- document preview state
- data grid artifacts
- per-session status and unread state

All of that needs a server-authoritative home if the same user must resume across devices and if sessions must later attach cleanly to workflows and business entities.[1][5]

## Current AGUIDojo baseline

Today the sample is a unified `/chat` architecture:

- `AGUIDojoClient` is a Blazor Server BFF with a session sidebar, chat UI, notifications, and YARP for `/api/*`
- `AGUIDojoServer` hosts business APIs plus a single `POST /chat` AG-UI route
- the client currently keeps session state locally and rehydrates it from browser storage on reload
- the live source of truth for sessions is the client Fluxor store, not the server
- session ids are currently generated in the client UI layer rather than issued by a server-side session API[1][12][13][17]

The current persistence model is explicitly browser-local:

- metadata is stored in `localStorage`
- active session id is stored in `localStorage`
- conversation trees are stored in IndexedDB
- hydration reconstructs only a subset of the original session state[2][3][4]

This local persistence is also **lossy**:

- `SessionMetadataDto` persists only `Id`, `Title`, `EndpointPath`, `Status`, `CreatedAt`, and `LastActivityAt`
- `ConversationNodeDto` reduces `ChatMessage` to `role`, `authorName`, `text`, and `createdAt`
- rich `AIContent` payloads are not durably preserved in the browser format
- hydration resets transient statuses and clears unread/pending approval surfaces
- hydration returns early if metadata is missing, so IndexedDB-only session trees are not recovered even though the JS layer exposes `loadAllSessionIds()`
- plans, approvals, audit trail, diff previews, recipe state, document state, and data grid state are not reconstructed from the persisted tree model[2][4][5]

So the current design is useful as a local UX cache, but it is not a viable primary store for:

- cross-device resume
- server-side authorization and ownership
- workflow and business-entity association
- analytics and audit
- multi-module integration
- reliable replay of the full session experience

## Why browser-local persistence is not enough

If server-side persistence becomes the primary data store for all users, AGUIDojo needs more than "save chat text in a table".

The server-side model must preserve:

1. **Canonical conversation history**
   - including branching/edit-and-regenerate behavior, not just a flat transcript.

2. **Rich message payloads**
   - not only text, but function calls, function results, structured tool payloads, AG-UI metadata, and multimodal references.

3. **Session-scoped artifacts and governance state**
   - approvals, audit trail, plans, diff previews, recipe/shared-state artifacts, data grids, and documents.

4. **Business context**
   - owner user, tenant, module/entity association, workflow linkage, and an authoritative server-side session index.

5. **Operational metadata**
   - status, timestamps, model/tool usage, durable workflow correlation ids, retry/edit lineage, and stream resume metadata where appropriate.

The current browser model proves that AGUIDojo already has these concepts at the UI layer, but it does not provide a durable, queryable, authoritative server representation of them.[2][3][4][5]

## Why Durable Task should not be the primary chat database

The local repo already contains a strong clue here.

`Microsoft.Agents.AI.DurableTask` is designed for:

- stateful durable execution
- conversation history management for agent execution
- long-running orchestrations
- runtime observability and recovery[6]

That is highly relevant, but it is still an **execution/runtime persistence model**, not a product/domain chat store.

Important constraints visible in the code:

- durable agent state stores `conversationHistory`
- durable agent entities can apply TTL expiration and delete themselves when idle
- workflow checkpoints are keyed by session/checkpoint id and are built for restore/rehydrate semantics[7][8][9][10]

Those are excellent properties for orchestration and recovery, but they are weak as the long-term, business-queryable source of truth for AGUIDojo sessions:

- runtime state is optimized for agent/workflow continuation, not broad business queries
- TTL-driven deletion is incompatible with product-grade conversation retention unless you disable or wrap it carefully
- workflow checkpoint data is opaque and internal compared with the shape business modules need
- other modules should not have to depend on Durable Task internals just to inspect or link a chat session

So the right boundary is:

- **application database = system of record for chat sessions**
- **Durable Task / workflow runtime = system of record for execution progress and recovery of long-running runs**

This aligns with common workflow guidance: business records should usually live in the application database, while workflow engines retain process state and orchestration metadata.[14]

## Recommended target architecture

### Core decision

Create a dedicated **Chat Sessions module** on the server side and make it the primary owner of:

- chat session metadata
- conversation graph
- approvals and audit
- artifact state/snapshots
- business/workflow links

Then let the AG-UI route and any future durable workflows **read from and write to that module**.

In framework terms, persistence belongs in application/hosting infrastructure around the agent, not inside the AG-UI transport layer and not inside unrelated business modules.[18]

### Recommended storage stack

Use the modular monolith's primary relational database.

If the future monolith already standardizes on SQL Server, use SQL Server. If the choice is still open, PostgreSQL is also an excellent fit. The key requirement is:

- strong transactional behavior
- first-class indexing
- JSON column support
- mature backup/retention tooling
- easy joins to business aggregates and workflow link tables

Use:

- **relational tables** for core metadata, ownership, links, approvals, and indexes
- **JSON columns** for full `ChatMessage`/`AIContent` payloads and flexible artifact payloads
- **blob/object storage** for large attachments or generated files
- **optional Redis** only for cache and resumable stream transport, not for canonical session storage[11][15][16]

### High-level architecture

```text
Browser / Device A, B
  |
  v
AGUIDojoClient (BFF + UI cache)
  |- GET/POST /api/chat-sessions/*         -> Chat Sessions module
  |- POST /chat                            -> AG-UI agent route
  \- IndexedDB/localStorage                -> secondary cache only

AGUIDojoServer / Modular Monolith
  |- Chat Sessions module                  -> primary conversation system of record
  |- Agent execution facade                -> runs unified AG-UI agent
  |- Workflow integration facade           -> links durable workflow/run ids
  |- Outbox / domain events                -> cross-module integration
  \- Business modules                      -> workflow/entity associations

Persistence
  |- Relational DB                         -> sessions, nodes, approvals, artifacts, links
  |- Blob store                            -> attachments / large generated files
  \- Redis (optional)                      -> resumable stream cursor log / cache

Durable runtime (optional but important)
  |- Durable agents                        -> long-running agent execution
  \- Durable workflows                     -> orchestration, checkpoints, HITL pauses
```

## Recommended domain model

The most important design choice is to model chat as a **business aggregate**, not just a transport session.

### 1. `chat_sessions`

One row per chat session.

Suggested fields:

- `id`
- `tenant_id`
- `owner_user_id`
- `title`
- `status`
- `created_at`
- `last_activity_at`
- `archived_at`
- `active_leaf_message_id`
- `server_protocol_version`
- `agui_thread_id` or `conversation_id` (nullable, correlation only)
- `durable_agent_session_id` (nullable)
- `workflow_instance_id` (nullable)
- `row_version`

Why this matters:

- the session is the top-level aggregate for cross-device resume
- `active_leaf_message_id` preserves current branch selection
- runtime ids are stored as correlations, not treated as the canonical identity
- internal durable agent/workflow sessions can be created and resumed independently of the top-level product chat session[18]

### 2. `chat_session_links`

A generic association table that lets the chat system attach to business modules without hard-coding the domain too early.

Suggested fields:

- `session_id`
- `link_type` (`owner`, `subject`, `workflow`, `case`, `order`, `ticket`, etc.)
- `module_name`
- `entity_type`
- `entity_id`
- `relationship_role`
- `created_at`

This lets AGUIDojo evolve into a modular monolith where a chat session can be associated with:

- a user
- a business workflow
- a business aggregate in another module
- an external case or request id

If later a specific integration becomes central, a dedicated typed table can sit beside this generic one.

### 2a. `chat_session_participants` or `chat_session_cursors` (future-safe)

AGUIDojo is currently single-user oriented, but cross-device continuity becomes cleaner if read-state and cursor/stateful positions are modeled separately from the session aggregate.

Suggested fields:

- `session_id`
- `user_id`
- `device_id` (nullable)
- `last_read_message_id`
- `last_seen_run_event_id`
- `updated_at`

This can remain optional in v1, but it is the cleanest place to put per-user/device read state later instead of overloading `chat_sessions`.

### 3. `chat_message_nodes`

This should preserve the current AGUIDojo branching conversation semantics rather than flattening everything into a single ordered transcript.

Suggested fields:

- `id`
- `session_id`
- `parent_message_id` (nullable for root)
- `sibling_order`
- `message_role`
- `author_name`
- `created_at`
- `turn_id`
- `message_kind` (`system`, `user`, `assistant`, `tool_call`, `tool_result`, `approval_response`, etc.)
- `content_json` (full serialized message contents, not just text)
- `text_preview`
- `model_name` (nullable)
- `usage_json` (nullable)

Why this matters:

- AGUIDojo already supports branch creation and branch switching through the `ConversationTree`
- edit-and-regenerate must not destroy the original lineage
- full `AIContent` fidelity is required to reconstruct tool calls, structured outputs, and multimodal references[2][5]

### 4. `chat_turns` (recommended projection)

Not strictly required, but very useful.

Suggested fields:

- `id`
- `session_id`
- `started_at`
- `completed_at`
- `status`
- `request_correlation_id`
- `initiated_by_user_id`
- `request_leaf_message_id`
- `response_leaf_message_id`
- `agent_route`
- `error_summary`

This groups many message nodes into a single user-visible "turn" and makes analytics and workflow handoffs easier.

### 4a. `chat_run_events` (short-lived replay journal)

If AGUIDojo needs reliable mid-stream reconnect semantics, keep a short-lived append-only run-event journal for active runs rather than storing every token forever inside the canonical message model.

Suggested fields:

- `id`
- `session_id`
- `chat_run_id`
- `sequence_number`
- `event_type`
- `payload_json`
- `created_at`
- `expires_at`

Recommended retention rule:

- keep canonical messages, artifacts, approvals, and audit permanently according to product retention rules
- keep fine-grained replay events only long enough to support `Last-Event-ID` style reconnect and recent-run troubleshooting

This follows the same separation shown by the reliable-streaming sample: session identity and final durable state outlive the transient streaming event log.[11][19]

### 5. `chat_approvals`

This persists the governance/HITL surface as domain data, not just UI state.

Suggested fields:

- `id`
- `session_id`
- `message_node_id`
- `approval_kind`
- `function_name`
- `arguments_json`
- `message`
- `status`
- `requested_at`
- `resolved_at`
- `resolved_by_user_id`
- `resolution_payload_json`

### 6. `chat_audit_entries`

The current client already models audit history separately, so the server should persist it separately as well.

Suggested fields:

- `id`
- `session_id`
- `approval_id` (nullable)
- `event_type`
- `risk_level`
- `autonomy_level`
- `was_approved`
- `was_auto_decided`
- `performed_by`
- `created_at`
- `details_json`

### 7. `chat_artifact_snapshots`

Artifacts should not be rebuilt only from transient stream behavior.

Suggested fields:

- `id`
- `session_id`
- `source_message_node_id`
- `artifact_type`
- `version`
- `is_current`
- `payload_json`
- `blob_uri` (nullable)
- `created_at`

This table can persist:

- plan state
- diff preview payloads
- recipe state
- document state
- data grid state
- future chart/form metadata

### 8. `chat_session_projection` or projection columns on `chat_sessions`

Maintain a fast current-state projection for list views and hydration:

- latest title
- current status
- unread count per user
- pending approval count
- last assistant summary/snippet
- latest artifact type

This should be derived from the canonical journal/tables, not treated as the only store.

## Preserve full history; derive summaries separately

One major lesson from the current AGUIDojo bug is that **canonical history must not be confused with context optimization**.

Today the client's `StatefulMessageCount` optimization drops earlier messages even though the AG-UI route expects the full history each turn. That is exactly the kind of layering mistake the server-side persistence design should avoid.

Recommended rule:

- keep the **full canonical session history** in the database
- keep branch structure intact
- create **derived summaries** or **context-window projections** separately when needed for model token limits
- never destructively replace the canonical history with summaries

In other words:

- **source of truth** = full journal/graph
- **runtime optimization** = summarized or trimmed working context

That separation mirrors the existing `ContextWindowChatClient` idea on the server side and avoids making persistence decisions based on prompt-length constraints. The current server already applies a context-window trim for prompt construction, which is exactly why canonical persistence must remain separate from runtime prompt assembly.[19]

## Workflow and modular-monolith integration

This is the most important architectural boundary for the future system.

### Recommended rule

Treat the chat session as a **business-facing record** that may participate in workflows, but do not make the workflow runtime itself the canonical chat store.

### Why

The repo's Durable Task and workflow layers are built around:

- agent session execution
- orchestration state
- checkpoints
- request/response pauses for HITL
- rehydration and resumption[6][7][8][9][10]

The workflow research adds an important identity boundary:

- durable agent state has its own `AgentSessionId(name,key)`
- workflow state has its own `WorkflowSession.SessionId` plus checkpoint chain
- AG-UI transport has `threadId` and `runId`

Those identities overlap conceptually, but they should not be collapsed into one storage identity. The application-level `ChatSession` should remain the stable product-facing aggregate, while agent sessions, workflow sessions, and transport ids remain linked execution references.[18]

That makes them ideal for:

- long-running agentic business workflows
- resumable approval steps
- execution recovery
- streaming durability

But the chat record still needs to be easily queryable by:

- business modules
- admin/reporting features
- authorization checks
- user profile/session screens
- analytics and compliance exports

### Best integration pattern

Use a **hybrid model**:

1. `chat_sessions` remains the product/business system of record.
2. When a session launches or joins a workflow, persist correlation ids on the session:
   - `workflow_instance_id`
   - `durable_agent_session_id`
   - `agui_thread_id`
3. Persist workflow-specific checkpoints and process state in the durable runtime.
4. Publish outbox/domain events from the chat module when important events occur:
   - session created
   - message appended
   - approval requested
   - approval resolved
   - artifact updated
   - workflow linked
   - session archived
5. Let business modules subscribe internally to those events instead of reading runtime internals directly.

This is the cleanest path for a modular monolith because it avoids coupling every module to AG-UI or Durable Task storage internals while still enabling agentic workflow capabilities.[14]

It also matches the sample/workflow split already present in the repo: workflow/business payloads such as ticket ids or research tasks travel as workflow data, while conversation/session identity is managed separately.[18]

For cross-device concurrency, I would default to **optimistic concurrency plus auto-branching** on stale writes, because the current AGUIDojo UI already understands branch semantics. A hard conflict dialog is still valid, but auto-branching is the least destructive default if two devices advance the same session concurrently.[19]

## Stream delivery should be separated from canonical session state

The reliable-streaming sample in this repo is a useful design signal:

- session identity is durable
- stream chunks are appended to Redis Streams
- clients reconnect using cursors
- stream data has its own TTL/lifecycle[11]

That pattern should inform AGUIDojo:

- **canonical chat history** belongs in the chat session store
- **stream transport durability** can use Redis or another append-only transport log
- do not overload the session record with transient per-chunk delivery state

This keeps cross-device replay simple:

- hydrate session from DB
- optionally resume an in-flight stream from Redis/event-log cursor
- if the stream log expired, the canonical turn output is still in the database

## Recommended API shape

The current server already has `/api/*` business endpoints and the unified `/chat` route, so the migration path should stay compatible with that split.[1][12][13]

Recommended new server APIs:

- `POST /api/chat-sessions`
- `GET /api/chat-sessions`
- `GET /api/chat-sessions/{id}`
- `GET /api/chat-sessions/{id}/messages`
- `GET /api/chat-sessions/{id}/artifacts`
- `POST /api/chat-sessions/{id}/branch/select`
- `POST /api/chat-sessions/{id}/approvals/{approvalId}/decision`
- `PATCH /api/chat-sessions/{id}`
- `POST /api/chat-sessions/{id}/archive`

The `/chat` route should then operate against a server-known session id:

1. client creates or opens a server chat session
2. server returns canonical metadata and current active branch info
3. client sends a turn through `/chat`
4. server appends user input, runs the agent, persists outputs/artifacts/approvals, updates projections
5. client rehydrates from server projections and may cache locally

### Important near-term constraint

Do **not** assume that introducing server persistence means the client can immediately send only deltas.

Because the current AG-UI path expects full history, the safe near-term approach is:

- fix the memory bug first
- keep sending full active-branch history per turn for the current `/chat` implementation
- let server persistence reconstruct/store that canonical history
- only later introduce a versioned server-stateful turn contract if AG-UI transport semantics are intentionally changed

## Migration plan

### Phase 0: fix the current memory bug first

Before changing persistence semantics, fix the within-session continuity issue by sending full active-branch history to the current AG-UI route.

Without that, the new server store will be fed already-truncated turn context.

### Phase 1: introduce a server Chat Sessions module

Add server-side session metadata APIs first:

- create/list/get/archive session
- store owner user id and timestamps
- move session-id creation to the server
- generate titles server-side from canonical session history instead of client-posted excerpts

The current `/api/title` endpoint is a natural precursor that can later read from stored session messages rather than receiving an ad hoc request payload.[12]

### Phase 2: persist the canonical conversation graph server-side

On every user turn:

- append the user message node
- persist the assistant/tool outputs as nodes
- update active leaf
- update last activity/title snippets

Keep browser persistence, but convert it into a cache.

### Phase 3: persist approvals, audit, and artifacts

Move these surfaces off of client-only state:

- pending approval requests
- approval decisions
- audit entries
- current artifact snapshots

This is the step that unlocks real cross-device continuity for the non-chat parts of the experience.

### Phase 4: add workflow and business links

Introduce:

- `chat_session_links`
- workflow correlation ids
- outbox/domain events

At this point the session becomes a first-class module in the broader modular monolith.

### Phase 5: keep browser storage only as an optimization

Retain IndexedDB/localStorage for:

- fast startup cache
- optimistic UI
- limited offline convenience

But all authoritative reads/writes come from the server.

Also define explicit rules for:

- how to reconcile cached sessions whose server record was archived or deleted
- whether pending approvals are resumed, expired, or cancelled on reconnect
- whether in-flight streams use resumable transport logs or are restarted from the canonical turn record

## Design choices I would make now

If implementation started next, I would make these choices immediately:

1. **Use the monolith's primary relational database as the primary store.**
   - Do not introduce a separate database technology just for chat unless the monolith already requires it.

2. **Preserve the conversation as a branching graph, not a flat transcript.**
   - Current AGUIDojo already depends on this behavior.

3. **Store full message payload JSON, not just text.**
   - The current browser DTO shape is not sufficient.

4. **Separate canonical history from runtime summaries.**
   - Summaries are derived artifacts.

5. **Treat workflow ids and durable agent ids as correlation ids, not primary identities.**

6. **Keep Redis optional and scoped to stream delivery/cache.**
   - Never as the only durable store.

7. **Add `tenant_id`, `owner_user_id`, and `row_version` from the beginning.**
   - Even if auth is not complete yet.

8. **Use the hosting/application layer as the persistence seam.**
   - This aligns with `AgentSessionStore` / `WithSessionStore` instead of overloading AG-UI transport.

9. **Emit outbox/domain events from the chat module.**
   - This is the cleanest way to integrate with other modules later.

10. **Enforce auth and tenant checks at the chat/session boundary as soon as identity lands.**
    - `/chat` currently runs without mandatory auth by default, so server-side primary persistence must be introduced together with ownership checks, not as a later afterthought.[19]

## Decision matrix

| Option | Fit for AGUIDojo future | Why |
| --- | --- | --- |
| Browser localStorage + IndexedDB as primary | Poor | Not cross-device, lossy, client-trust based, weak for business integration |
| Durable Task entity/checkpoint storage as primary | Weak to moderate | Good for execution durability, poor as business-queryable product record, TTL/runtime-centric |
| Document DB only | Moderate | Flexible schema, but weaker natural fit for modular-monolith joins and transactional links |
| Relational DB primary + JSON payloads + optional durable runtime integration | Best | Preserves business semantics, queryability, cross-module links, and execution/runtime separation |

## Final recommendation

The best solution is:

**Build a server-owned Chat Sessions module backed by the monolith's primary relational database, preserve the full branching conversation and rich AI payloads there, move approvals/artifacts/audit into the same authoritative server-side model, keep browser persistence only as a cache, and treat Durable Task/workflow persistence as a complementary execution-layer mechanism rather than the primary product database.**

That design is the best match for:

- AGUIDojo's current client/session model
- the need for cross-device resume
- future identity and ownership
- modular-monolith integration
- workflow correlation and agentic business processes
- long-term auditability and operational safety

## Confidence assessment

Confidence is **high** on the architectural direction and storage boundary:

- current AGUIDojo code clearly shows the browser cache is incomplete as a primary store
- current AGUIDojo session state clearly exceeds plain text chat
- local Durable Task code clearly shows a runtime-oriented durable model with TTL and checkpoint semantics
- local workflow samples clearly separate durable execution/recovery from broader product concerns

Confidence is **medium** on the exact table breakdown because that should still be aligned with the eventual monolith's existing database conventions and identity model.

## Footnotes

[1] `dotnet/samples/05-end-to-end/AGUIDojo/README.md`

[2] `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/SessionPersistenceService.cs`

[3] `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/wwwroot/js/sessionPersistence.js`

[4] `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Store/SessionManager/SessionHydrationEffect.cs`

[5] `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Store/SessionManager/SessionState.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Models/ConversationTree.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Models/SessionMetadata.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Models/ApprovalItem.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Models/AuditEntry.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/ApprovalHandler.cs`

[6] `dotnet/src/Microsoft.Agents.AI.DurableTask/README.md`

[7] `dotnet/src/Microsoft.Agents.AI.DurableTask/State/DurableAgentStateData.cs`, `dotnet/src/Microsoft.Agents.AI.DurableTask/State/DurableAgentStateRequest.cs`, `dotnet/src/Microsoft.Agents.AI.DurableTask/State/DurableAgentStateResponse.cs`, `dotnet/src/Microsoft.Agents.AI.DurableTask/State/DurableAgentStateMessage.cs`

[8] `dotnet/src/Microsoft.Agents.AI.DurableTask/AgentEntity.cs`, `dotnet/src/Microsoft.Agents.AI.DurableTask/DurableAIAgent.cs`, `dotnet/src/Microsoft.Agents.AI.DurableTask/AgentSessionId.cs`

[9] `dotnet/src/Microsoft.Agents.AI.Workflows/CheckpointInfo.cs`, `dotnet/src/Microsoft.Agents.AI.Workflows/Checkpointing/Checkpoint.cs`, `dotnet/src/Microsoft.Agents.AI.Workflows/Checkpointing/ICheckpointStore.cs`

[10] `dotnet/samples/03-workflows/Checkpoint/CheckpointAndResume/Program.cs`, `dotnet/samples/03-workflows/Checkpoint/CheckpointAndRehydrate/Program.cs`, `dotnet/samples/03-workflows/Checkpoint/CheckpointWithHumanInTheLoop/Program.cs`, `dotnet/samples/04-hosting/DurableWorkflows/ConsoleApps/06_WorkflowSharedState/Program.cs`, `dotnet/samples/04-hosting/DurableWorkflows/ConsoleApps/08_WorkflowHITL/Program.cs`

[11] `dotnet/samples/04-hosting/DurableAgents/ConsoleApps/07_ReliableStreaming/Program.cs`, `dotnet/samples/04-hosting/DurableAgents/ConsoleApps/07_ReliableStreaming/RedisStreamResponseHandler.cs`

[12] `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Api/TitleEndpoints.cs`

[13] `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Program.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Program.cs`

[14] Microsoft ISE, "Hitchhikers Guide to Workflow Engines": https://devblogs.microsoft.com/ise/guide-to-workflow-engines/

[15] Redis, "LangGraph & Redis: Build smarter AI agents with memory & persistence": https://redis.io/blog/langgraph-redis-build-smarter-ai-agents-with-memory-persistence/

[16] Restate, "Durable Sessions": https://docs.restate.dev/ai/patterns/sessions-and-chat

[17] `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Store/SessionManager/SessionEntry.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Store/SessionManager/SessionManagerState.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Components/Pages/Chat/Chat.razor`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient.Tests/Services/SessionPersistenceServiceTests.cs`

[18] `docs/decisions/0010-ag-ui-support.md`, `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs`, `dotnet/src/Microsoft.Agents.AI.AGUI/AGUIChatClient.cs`, `dotnet/src/Microsoft.Agents.AI.Hosting/AgentSessionStore.cs`, `dotnet/src/Microsoft.Agents.AI.Hosting/AIHostAgent.cs`, `dotnet/src/Microsoft.Agents.AI.Hosting/HostedAgentBuilderExtensions.cs`, `dotnet/src/Microsoft.Agents.AI.Workflows/WorkflowSession.cs`, `dotnet/src/Microsoft.Agents.AI.DurableTask/Workflows/DurableExecutorDispatcher.cs`, `workflow-samples/CustomerSupport.yaml`, `workflow-samples/DeepResearch.yaml`

[19] `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/ContextWindowChatClient.cs`, `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIServerSentEventsResult.cs`, `dotnet/src/Microsoft.Agents.AI.AGUI/AGUIHttpService.cs`, `dotnet/src/Microsoft.Agents.AI.AGUI/Shared/RunAgentInput.cs`, `dotnet/src/Microsoft.Agents.AI.AGUI/Shared/RunStartedEvent.cs`, `dotnet/src/Microsoft.Agents.AI.AGUI/Shared/RunFinishedEvent.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoServer/Program.cs`, `dotnet/src/Microsoft.Agents.AI.DurableTask/Workflows/DurableWorkflowRun.cs`, `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Models/Checkpoint.cs`
