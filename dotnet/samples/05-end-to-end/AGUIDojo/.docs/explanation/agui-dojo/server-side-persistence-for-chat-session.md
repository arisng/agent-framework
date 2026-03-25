<!-- MY CUSTOMIZATION POINT: align persistence guidance with the restored full-history /chat invariant -->

# Server-side primary persistence for AGUIDojo chat sessions

## Executive summary

The best fit for AGUIDojo is still **not** to keep browser storage as the source of truth, and it is still **not** to make Durable Task entity state the product-level chat database.

However, after reviewing the current Microsoft Agent Framework (.NET) repo more closely, the main conclusion changes in one important way: **SQL should be the recommended default, not Cosmos/NoSQL.** MAF already gives AGUIDojo the right seams to build on — `AgentSessionStore` for serializable session restoration, `ChatHistoryProvider` for canonical message history, and the AG-UI hosting boundary around `/chat` — but those seams are **storage-agnostic**. They require durable persistence and ordered history, not a document database.[18][20][22]

That matters because AGUIDojo is intended to sit inside a modular monolith whose business modules already lean relational and whose nearby design docs already assume SQLite for sample scope with portability to SQL Server or PostgreSQL later.[23][24] In that environment, the default persistence choice should align with the rest of the application:

- **recommended default**: application-owned relational Chat Sessions storage, backed by the monolith's primary SQL database family, with `AgentSessionStore` and any `ChatHistoryProvider` implementation adapted to that relational store
- **optional variant**: Cosmos/NoSQL only when a deployment already standardizes on it and is comfortable treating it as an implementation detail for linear history or checkpoints rather than the architectural default[21]

The important limitation is still not a NoSQL requirement; it is an integration gap. AG-UI hosting does not wire these pieces together out of the box today, and the repo does **not** ship a durable `AgentSessionStore` implementation for AG-UI/ASP.NET Core. `MapAGUI` forwards `threadId` and `runId`, but ADR 0010 explicitly says applications currently own thread persistence; A2A is the place in this repo that actually auto-wraps agents with `AIHostAgent` and `AgentSessionStore`.[18][22]

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
- the server owns a durable chat-session catalog, canonical branching conversation graph, and workspace projection APIs
- the client still renders from Fluxor, but hydration now prefers server-owned conversation/workspace reads and only falls back to browser cache/import when the server surfaces are unavailable
- the browser cache remains useful for drafts and best-effort recovery, but it is no longer the authoritative session record[1][12][13][17]

The current persistence model is therefore split on purpose:

- the **server** stores session identity, list/detail lifecycle, canonical branching conversation history, approvals, audit events, and workspace snapshots
- the **browser** still caches metadata in `localStorage`, active session state in `localStorage`, and conversation-tree convenience data in IndexedDB
- hydration now reconstructs the main session experience from server reads first, using the browser cache only as fallback/import support[2][3][4]

This local persistence is also **lossy**:

- `SessionMetadataDto` persists only `Id`, `Title`, `EndpointPath`, `Status`, `CreatedAt`, and `LastActivityAt`
- `ConversationNodeDto` reduces `ChatMessage` to `role`, `authorName`, `text`, and `createdAt`
- rich `AIContent` payloads are not durably preserved in the browser format
- hydration resets transient statuses and clears unread/pending approval surfaces
- hydration returns early if metadata is missing, so IndexedDB-only session trees are not recovered even though the JS layer exposes `loadAllSessionIds()`
- plans, approvals, audit trail, diff previews, recipe state, document state, and data grid state are not reconstructed from the persisted tree model[2][4][5]

So the browser-local layer remains useful as a local UX cache/import source, but it is not a viable primary store for:

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

## What MAF already gives us out of the box

MAF is not blank on this problem. The repo already ships most of the primitives we would otherwise write by hand.

### 1. A session persistence seam

`AgentSessionStore` defines the contract for saving and loading serialized `AgentSession` instances, `AIHostAgent` wraps an existing agent so it can restore and save those sessions, and `HostedAgentBuilderExtensions` exposes `WithInMemorySessionStore()` and `WithSessionStore(...)` as the official registration points.[18]

That is not a full product chat module, but it is a real framework-supported seam for server-side conversation persistence. The repo also includes `NoopAgentSessionStore` for stateless hosting, but the only bundled stateful store is in-memory, and its remarks list several durable production options — including Redis, SQL Server, and Cosmos DB — rather than prescribing NoSQL. For AGUIDojo, that is a signal to plug the seam into the same relational infrastructure family the rest of the modular monolith already expects.[18][23][24]

### 2. A first-class chat history seam

`ChatClientAgent` already separates **session identity** from **chat history storage**. When the underlying AI service does not manage history server-side, the agent uses `ChatHistoryProvider` to fetch, store, truncate, summarize, or archive messages, and the serialized `AgentSession` can contain either conversation history directly or a reference to externally stored history.[20]

This is important because `ChatHistoryProvider` defines behavior, not database shape. It needs ordered message persistence and retrieval plus optional truncation, summarization, or archival; those requirements map cleanly to relational tables with sequence numbers, timestamps, and JSON payload columns just as well as to a document store. AGUIDojo therefore does **not** need to invent its own abstraction, and it also does **not** need NoSQL just to satisfy the framework seam.[20]

### 3. A shipped optional Cosmos example

The `Microsoft.Agents.AI.CosmosNoSql` package already ships `CosmosChatHistoryProvider`, helper extensions like `WithCosmosDBChatHistoryProvider(...)`, and `CosmosCheckpointStore` for workflow checkpoints.[21]

That is useful because it proves MAF expects pluggable durable history and checkpoint providers. But it should be read as an optional implementation family, not as a signal that AGUIDojo must choose Cosmos DB or NoSQL. For AGUIDojo's intended modular-monolith architecture — where nearby docs already assume SQLite for sample scope and portability to SQL Server or PostgreSQL later — the stronger default is still relational application storage.[21][23][24]

Its trade-off also remains the same: it models **linear chat history and checkpoint storage**, not a relational business aggregate with first-class branching/session projections.[21]

### 4. The current AG-UI gap

`MapAGUI` currently just forwards inbound messages to `aiAgent.RunStreamingAsync(...)`; it does not look up an `AgentSession`, call `GetOrCreateSessionAsync`, or save it afterward. ADR 0010 is explicit that AG-UI applications manage thread persistence today, with server-managed conversation left as future work. By contrast, A2A already demonstrates the intended session-management pattern by wrapping the agent in `AIHostAgent`, loading the session by `contextId`, and saving it after each run.[18][22]

This is the key strategic conclusion: **MAF can reduce AGUIDojo's custom persistence work substantially, but only if AGUIDojo adds a small amount of server integration around `/chat`; it is not turnkey for current AG-UI hosting yet.**

If AGUIDojo were willing to change hosting models entirely, Durable Agents / Azure Functions is the only closer-to-turnkey server-side session option in this repo. But that is a larger architectural shift and still represents runtime durability more than a product-facing chat module.[6][22]

## Recommended target architecture

### Core decision

For AGUIDojo, the default implementation path should be an application-owned **relational Chat Sessions module**, not a Cosmos-first thin shim and not Durable Task storage as the product database. That matches the nearby AGUIDojo docs, which already assume a SQLite-backed sample that stays portable to SQL Server or PostgreSQL later.[23][24]

Use MAF's abstractions, but back them with the same SQL-oriented persistence family as the rest of the modular monolith:

- load and save `AgentSession` in a SQL-backed `AgentSessionStore`
- persist canonical message history through a SQL-backed `ChatHistoryProvider` implementation or directly through relational message tables that the provider reads and writes
- keep `MapAGUI("/chat", ...)` as the transport boundary only; application code around it is still responsible for session restore/save
- keep Todo/business data in the Todo module; the chat module stores links, history, approvals, artifacts, and audit, not duplicate business ownership[18][20][22][23][24]

In framework terms, the relevant MAF seams remain useful, but they do **not** force a NoSQL choice:

- `AgentSessionStore` only saves and loads serialized session state by conversation id
- `ChatHistoryProvider` only requires ordered retrieval and storage plus optional truncation, summarization, or archival
- ADR 0010 says AG-UI applications manage thread persistence today, which means the application chooses the backing store[18][20][22]

### Recommended storage stack

**SQL-first default**

- **SQLite** for sample scope, kept relational and portable to **SQL Server or PostgreSQL** for the modular-monolith target[23][24]
- `chat_sessions` and related relational tables as the authoritative system of record
- SQL-backed `AgentSessionStore` for serializable agent/session restoration
- SQL-backed `ChatHistoryProvider` or equivalent relational message persistence for canonical history
- JSON columns for rich `ChatMessage`/`AIContent` payloads and flexible artifact payloads
- blob/object storage only for large attachments or generated files
- **optional Redis** only for cache or resumable stream transport, never as the canonical session store[11][15][16]

**Optional NoSQL variant**

If a specific deployment already standardizes on Cosmos DB and mostly wants linear history/checkpoint storage, `CosmosChatHistoryProvider` and `CosmosCheckpointStore` are valid optional implementations.[21]

But for AGUIDojo's intended modular-monolith shape, they should be treated as provider choices, not as the design center. Business-facing session metadata, Todo links, approvals, audit, and cross-module queries still fit better in relational storage.[21][23][24]

### High-level architecture

```text
Browser / Device A, B
  |
  v
AGUIDojoClient (BFF + UI cache)
  |- POST /chat                            -> AG-UI route + session restore/save adapter
  |- GET/POST /api/chat-sessions/*         -> server-owned session API
  \- IndexedDB/localStorage                -> secondary cache only

AGUIDojoServer / Modular Monolith
  |- Chat Sessions module                  -> session aggregate, projections, auth boundary
  |- AIHostAgent-style session adapter     -> get/create/save AgentSession
  |- ChatHistoryProvider                   -> canonical history seam
  |- Todo module facade                    -> load/update Todo as current user
  \- Workflow integration facade           -> correlation ids only

Persistence
  |- Primary relational DB
  |    |- SQLite for sample scope
  |    |- SQL Server or PostgreSQL for monolith deployments
  |    |- chat_sessions / chat_message_nodes / approvals / audit / artifacts
  |    \- todos and other business tables
  |- Optional blob/object storage          -> attachments/generated files
  |- Optional workflow checkpoint store    -> runtime durability
  \- Browser storage                       -> local cache only

Optional provider swap
  \- Cosmos/NoSQL ChatHistoryProvider      -> only if a deployment already prefers it
```

This architecture keeps the product-facing chat record in the same relational universe as the business modules it supports, while still using MAF seams where they help.[18][20][23][24]

## Recommended domain model for the SQL-first path

The most important design choice is to model chat as a **business aggregate inside the modular monolith**, not just as a transport session. In AGUIDojo's expected direction, the guiding example is the Todo module: a user can have multiple related chats around the same Todo or business flow, and agents act on that Todo data on behalf of the current user.[23][24]

### 1. `chat_sessions`

One row per chat session.

Suggested fields:

- `id`
- `tenant_id`
- `owner_user_id`
- `subject_module_name` (for example `Todo`)
- `subject_entity_type` (for example `Todo`)
- `subject_entity_id` (for example the Todo id)
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
- the subject columns make the business context explicit without collapsing it into runtime ids
- one Todo/business flow can have **many** chat sessions because many rows can point at the same subject id
- `owner_user_id` makes it clear the agent is acting on Todo data on behalf of the current user
- runtime ids are stored as correlations, not treated as the canonical identity[18][23][24]

### 2. `chat_session_links`

If AGUIDojo later needs a session to reference more than one business record, add a separate link table rather than turning the primary session row into an unstructured blob.

Suggested fields:

- `session_id`
- `link_type` (`primary_subject`, `related_workflow`, `attachment_owner`, etc.)
- `module_name`
- `entity_type`
- `entity_id`
- `relationship_role`
- `created_at`

For the near-term AGUIDojo direction, the important case is still simple and relational: many chat sessions can be linked to the same Todo, and those links should be indexed for list pages, authorization checks, and business queries.[23][24]

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

A previously considered `StatefulMessageCount`/delta-turn optimization would have dropped earlier messages even though the AG-UI route expects the full history each turn. That is exactly the kind of layering mistake the server-side persistence design should avoid, and the current client now preserves the full active branch for every `/chat` turn.

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

If AGUIDojo follows the SQL-first path, this becomes the most important architectural boundary for the future system.

### Recommended rule

Treat the chat session as a **business-facing record owned by the AGUIDojo module and linked to a business subject**. For the clearest near-term example, one `Todo` can have many related chat sessions, while the `Todo` aggregate remains the source of truth for Todo data.

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

Those identities overlap conceptually, but they should not be collapsed into one storage identity. The application-level `ChatSession` should remain the stable product-facing aggregate, while Todo ids, agent sessions, workflow sessions, and transport ids remain linked but distinct references.[18]

That separation is especially important for AGUIDojo's intended modular monolith:

- business modules want SQL queries and transactions against familiar tables
- agents should read or mutate Todo data through Todo module services on behalf of the current user
- other modules should not need to understand AG-UI or Durable Task internals just to answer "show me all chats for Todo 123" or "what approvals were raised while this user worked that Todo?"[23][24]

### Best integration pattern

Use a **hybrid relational model**:

1. `todos` remains the system of record for Todo/business data.
2. `chat_sessions` remains the system of record for chat identity, history, approvals, artifacts, and audit.
3. Link sessions to the business flow using indexed relational subject fields or link rows so that **one Todo can have many chat sessions**.
4. Resolve the current user and the linked Todo before the agent runs tools; the agent acts through Todo application services on behalf of that user, not by bypassing business rules.
5. When a session launches or joins a workflow, persist correlation ids on the session:
   - `workflow_instance_id`
   - `durable_agent_session_id`
   - `agui_thread_id`
6. Persist workflow-specific checkpoints and process state in the durable runtime.
7. Publish outbox/domain events from the chat module when important events occur:
   - session created
   - message appended
   - approval requested
   - approval resolved
   - artifact updated
   - workflow linked
   - session archived
8. Let business modules subscribe internally to those events instead of reading runtime internals directly.

This is the cleanest path for a modular monolith because it keeps business data in the business module, keeps chat data in the chat module, and still makes cross-module SQL queries, authorization checks, and audit/reporting straightforward.[14][23][24]

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

Once AGUIDojo introduces the server-owned session layer, the server APIs should stay compatible with the current split between `/api/*` business endpoints and the unified `/chat` route.[1][12][13]

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

Because the current AG-UI path expects full history **and** `MapAGUI` does not yet restore `AgentSession` automatically, the safe near-term approach is:

- preserve the full-history continuity invariant first
- introduce server session load/save around `/chat`
- do **not** naively enable a server `ChatHistoryProvider` while the client still posts full active-branch history, or the same history will be duplicated
- keep sending full active-branch history per turn until the server contract explicitly reconciles or versions stateful turns
- only later introduce a versioned server-stateful turn contract if AG-UI transport semantics are intentionally changed[18][20][22]

## Migration plan

### Phase 0: preserve the current full-history invariant

Before changing persistence semantics, keep the within-session continuity invariant intact by sending full active-branch history to the current AG-UI route.

Without that, the new server store will be fed already-truncated turn context.

### Phase 1: introduce server session identity and SQL-backed session restoration

Add the minimum server-side session layer first:

- create/list/get/archive session metadata APIs
- store owner user id and timestamps
- move session-id creation to the server
- link a new session to the relevant business subject (start with Todo id / business flow id)
- wrap `/chat` handling with `AIHostAgent` / `AgentSessionStore` semantics, but back the store with relational persistence rather than browser state
- generate titles server-side from canonical session history instead of client-posted excerpts[18][20][22][23][24]

The nearby AGUIDojo docs already assume SQLite for sample scope and portability to SQL Server/PostgreSQL later, so this phase should follow that same relational boundary rather than introducing a separate NoSQL-first subsystem.[23][24]

The current `/api/title` endpoint is a natural precursor that can later read from stored session messages rather than receiving an ad hoc request payload.[12]

### Phase 2: persist the canonical conversation in SQL

On every user turn:

- append the user message node/row
- persist the assistant/tool outputs as nodes or ordered message records
- update active leaf
- update last activity/title snippets

If AGUIDojo uses `ChatHistoryProvider`, implement it against those relational tables or projections. The framework seam does not require NoSQL; it only requires durable ordered history.[20]

Keep browser persistence, but convert it into a cache. If a deployment later wants Cosmos for mostly linear history, that can remain an optional provider swap, not the basis of the domain model.[21]

### Phase 3: persist approvals, audit, and artifacts

Move these surfaces off of client-only state:

- pending approval requests
- approval decisions
- audit entries
- current artifact snapshots

This is the step that unlocks real cross-device continuity for the non-chat parts of the experience.

### Phase 4: deepen Todo/workflow integration

Introduce:

- indexed Todo/business-flow links so one Todo can be associated with many chat sessions
- workflow correlation ids
- outbox/domain events
- current-user authorization checks at the chat/Todo boundary

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

1. **Default to relational storage in the same database family as the business modules.**
   - SQLite for sample scope; SQL Server or PostgreSQL for the modular-monolith target.[23][24]

2. **Use MAF's session/history seams, but back them with SQL.**
   - `AgentSessionStore` and `ChatHistoryProvider` are useful abstractions, not a reason to prefer NoSQL.[18][20]

3. **Make the business subject part of session identity from the beginning.**
   - Start with the Todo example: one Todo can have many related chat sessions.

4. **Treat agents as acting on business data on behalf of the current user.**
   - Resolve Todo access and mutations through Todo module services, not through raw chat persistence.

5. **Store full message payload JSON, not just text.**
   - The current browser DTO shape is not sufficient.

6. **Separate canonical history from runtime summaries.**
   - Summaries are derived artifacts.

7. **Treat workflow ids and durable agent ids as correlation ids, not primary identities.**

8. **Keep Cosmos/NoSQL optional only.**
   - Use it if a specific deployment already prefers it and linear history is enough; do not make it the architectural default.[21]

9. **Keep Redis optional and scoped to stream delivery/cache.**
   - Never as the only durable store.

10. **Add `tenant_id`, `owner_user_id`, and concurrency/version fields from the beginning.**

11. **Use the hosting/application layer as the persistence seam.**
    - This aligns with `AgentSessionStore` / `WithSessionStore` instead of overloading AG-UI transport.

12. **Enforce auth and tenant checks at the chat/session boundary as soon as identity lands.**
    - `/chat` currently runs without mandatory auth by default, so server-side primary persistence must be introduced together with ownership checks, not as a later afterthought.[19]

## Decision matrix

| Option | Fit for AGUIDojo future | Why |
| --- | --- | --- |
| Browser localStorage + IndexedDB as primary | Poor | Not cross-device, lossy, client-trust based, weak for business integration |
| Durable Task entity/checkpoint storage as primary | Weak to moderate | Good for execution durability, poor as business-queryable product record, TTL/runtime-centric |
| Switch to Durable Agents / Azure Functions as the primary hosting model | Moderate | Closer to turnkey server durability, but it is a hosting-model change and still runtime-centric |
| SQL-backed Chat Sessions module using `AgentSessionStore` / `ChatHistoryProvider` seams | Best default | Cloud-vendor-agnostic, consistent with nearby AGUIDojo docs, and a natural fit for Todo/business modules already using SQL |
| Cosmos/NoSQL-backed `ChatHistoryProvider` plus relational business projections | Situational / optional | Valid if a deployment already standardizes on Cosmos and mostly wants linear history, but weaker as the default for a SQL-backed modular monolith |

## Final recommendation

The best strategic move is **to make AGUIDojo's primary persistence relational and application-owned from the start.**

Use a SQL-backed Chat Sessions module as the system of record — SQLite for sample scope, kept portable to SQL Server or PostgreSQL for the modular-monolith target — and plug MAF into that boundary:

**Integrate `AIHostAgent` / `AgentSessionStore` into AGUIDojo's `/chat` handling, back canonical message history with relational storage behind `ChatHistoryProvider` or equivalent SQL tables, keep `MapAGUI` as the transport boundary only, and link each session to the current user and the relevant business subject (starting with the Todo module, where one Todo can have many related chat sessions).**[18][20][22][23][24]

Cosmos/NoSQL remains a valid optional provider choice if a specific deployment already prefers it, but it is not required and should not drive the default architecture.[21]

That sequence is the best match for:

- cloud-vendor-agnostic deployment
- consistency with AGUIDojo docs that already lean SQLite/relational
- easy joins and authorization boundaries with SQL-backed business modules
- using MAF seams without overloading Durable Task or AG-UI transport
- leaving room for optional Cosmos/NoSQL or workflow-specific stores later if a deployment truly benefits

If AGUIDojo is willing to change hosting models instead of extending the current ASP.NET Core `MapAGUI` path, Durable Agents is still worth evaluating separately. For the **current** AGUIDojo architecture, though, SQL-first persistence plus a thin AG-UI integration layer is the lower-friction path.[6][22][23][24]

## Confidence assessment

Confidence is **high** on the framework boundary and on the SQL-first recommendation:

- current AGUIDojo code clearly shows the browser cache is incomplete as a primary store
- current MAF code clearly provides `AgentSessionStore`, `AIHostAgent`, and `ChatHistoryProvider`, and those seams do not prescribe NoSQL
- current AG-UI hosting code clearly does **not** wire session load/save automatically
- ADR 0010 clearly leaves thread persistence to the application today
- nearby AGUIDojo docs clearly point toward SQLite now and SQL Server/PostgreSQL portability later
- local Durable Task code clearly shows a runtime-oriented durable model with TTL and checkpoint semantics

Confidence is **medium** on the exact long-term message shape:

- that depends on whether AGUIDojo truly needs first-class branching history and richer artifact projections than linear message history provides
- the current client state and Todo/business-flow direction suggest it probably does, but that should still be proven as implementation lands

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

[20] `dotnet/src/Microsoft.Agents.AI.Abstractions/AgentSession.cs`, `dotnet/src/Microsoft.Agents.AI.Abstractions/ChatHistoryProvider.cs`, `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgent.cs`, `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgentSession.cs`, `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgentOptions.cs`

[21] `dotnet/src/Microsoft.Agents.AI.CosmosNoSql/Microsoft.Agents.AI.CosmosNoSql.csproj`, `dotnet/src/Microsoft.Agents.AI.CosmosNoSql/CosmosChatHistoryProvider.cs`, `dotnet/src/Microsoft.Agents.AI.CosmosNoSql/CosmosDBChatExtensions.cs`, `dotnet/src/Microsoft.Agents.AI.CosmosNoSql/CosmosCheckpointStore.cs`, `dotnet/src/Microsoft.Agents.AI.CosmosNoSql/CosmosDBWorkflowExtensions.cs`

[22] `dotnet/src/Microsoft.Agents.AI.Hosting.A2A/AIAgentExtensions.cs`, `dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs`, `docs/decisions/0010-ag-ui-support.md`

[23] `dotnet/samples/05-end-to-end/AGUIDojo/.docs/explanation/agui-dojo/system-design.md`

[24] `dotnet/samples/05-end-to-end/AGUIDojo/.issues/260323_aguidojo-implementation-plan.md`
