# AGUIDojo implementation plan

## Purpose

This is the consolidated execution plan for evolving the AGUIDojo sample. It supersedes the older v1/v2/v3 implementation-plan sprawl and the retired long-form roadmap issue. It stays aligned with the current README, the consolidated system design, the unified `POST /chat` implementation, and the current model-picker / context-window / Copilot-overlap research set. It is an execution playbook, not a duplicate system design.

The current research baseline for model selection, model-aware compaction, persistence boundaries, MAF integration, and overlapping Copilot CLI product patterns is captured in `.docs/research/aguidojo-llm-picker-architecture-and-maf-alignment.md`, `.docs/research/server-side-persistence-for-chat-session.md`, `.docs/research/copilot-cli-session-context-and-instruction-patterns.md`, `.docs/research/copilot-cli-public-repo-grounding.md`, `.docs/research/copilot-cli-session-state-schema.md`, and `.docs/research/copilot-cli-session-topology.md`.

The Copilot CLI research is a product-pattern reference, not a storage contract. AGUIDojo should adopt the durable-session, requested-versus-effective model, checkpointed-compaction, and catalog-plus-workspace inspection-surface lessons without copying Copilot CLI's local filesystem layout or exact SQLite/file schema.

The plan should stay synchronized with the sample as each phase lands. README, system design, supporting research, and implementation must describe the same architecture and sequencing.

## Scope guardrails

- **AGUIDojo scope only.** This plan applies only to `dotnet/samples/05-end-to-end/AGUIDojo`.
- **Auth and identity stay simulated.** Use seeded or fake current-user/current-tenant context when needed; do not turn this into a real auth rollout plan.
- **SQL-first, relational-first persistence is the default.** Keep the model cloud-vendor agnostic and portable; SQLite may remain useful for local sample runs, but SQL Server or PostgreSQL are the natural modular-monolith targets.
- **AGUIDojo remains a module inside a modular monolith.** Chat sessions link to business-module subjects (start with Todo), while business data stays owned by those modules.
- **Browser storage becomes secondary over time.** `localStorage` and IndexedDB remain useful for cache, drafts, and best-effort import, not as the long-term system of record.
- **Single `/chat` route stays.** Model selection must travel on the existing route as request metadata; do not split AGUIDojo into one endpoint per model.
- **Server owns context budgeting.** The client may expose a model picker, but token counting, compaction, and downgrade safety stay server-side.
- **Use MAF seams where they exist.** Prefer AG-UI forwarded/request metadata, `ChatClientFactory`, and `CompactionProvider` / pipeline compaction over bespoke transport shapes.
- **Model metadata is application-owned.** MAF does not ship a model registry or context-window catalog; keep AGUIDojo's catalog small and sample-scoped.
- **Validation stays pragmatic.** Prefer focused server tests plus lightweight manual validation. A broader `AGUIDojoClient.Tests` strategy is deferred.
- **Replay and collaboration are optional.** Defer replay, multi-device collaboration, fine-grained event journaling, and similar features unless they directly unblock the foundation.
- **Runtime IDs are not business IDs.** AG-UI `threadId`, `runId`, `ConversationId`, Durable Task IDs, and workflow instance IDs remain correlation/runtime references.

## Current baseline and blocking gaps

### Current baseline

- AGUIDojo currently presents a unified `/chat` sample end to end.
- `AGUIDojoClient` is a Blazor Server BFF with session-keyed UI state, direct AG-UI streaming to `/chat`, and YARP for `/api/*` business endpoints.
- The client already renders chat, plans, approvals, recipe/shared state, document previews, charts, forms, data grids, notifications, and background-session switching.
- Conversation state already has a branching shape in the client, but that branching state is still client-owned.
- Browser persistence today uses `localStorage` for metadata/active-session hints and IndexedDB for conversation trees.
- `AGUIDojoServer` hosts `/chat` plus business APIs, but it does **not** yet own a Chat Sessions module or a primary chat-session store.
- The server currently chooses one model at startup (`OPENAI_MODEL` or Azure deployment) and all sessions share that provider-bound client.
- Context trimming currently relies on `ContextWindowChatClient`, a fixed 80 non-system-message wrapper rather than a model-aware token policy.

### Blocking gaps

1. **Within-session continuity bug on `/chat`.**
   - The current client path uses `StatefulMessageCount` and `session.Messages.Skip(session.StatefulMessageCount)` for later turns.
   - That delta-turn behavior drops earlier active-branch history after the first successful turn.
   - Result: multi-turn continuity breaks, and approval/retry/edit-and-regenerate re-entry paths are at risk of replaying incomplete context.

2. **Browser-local persistence is the only persistence today, and it is lossy.**
   - Session metadata and active session identity live in `localStorage`.
   - Conversation trees live in IndexedDB.
   - Current browser persistence does not rehydrate full approvals, audit history, plans, recipe state, document state, grids, or other rich session surfaces.
   - The current browser conversation serialization is intentionally lightweight, so it does not preserve full rich-message fidelity.

3. **There is no server-owned Chat Sessions module yet.**
   - The server does not issue and own the primary business session ID.
   - There is no canonical session lifecycle/API surface yet; the target list/get/archive endpoints and implicit server-side creation on the first persisted `/chat` turn do not exist yet.
   - Cross-device resume, durable hydration, archival, and server-authoritative linking to workflows or business entities are therefore not in place.

4. **There is no per-session model picker or model-aware context policy yet.**
   - No model catalog or `/api/models` contract exists yet.
   - The `/chat` transport does not yet carry a session-selected model.
   - `ChatClientAgentFactory` binds a single provider client at startup.
   - `ContextWindowChatClient` is a fixed message cap, not a model-aware compaction pipeline.

## Guiding principles

- **Fix continuity before durability.** Do not harden persistence around already-truncated turn submission.
- **Move ownership to the server incrementally.** Keep the sample runnable while shifting the source of truth from client/browser to server-owned relational storage; SQLite may remain a local convenience during the transition.
- **Treat durability as a support surface.** Server-owned session state should help explain recovery, approvals, model routing, and compaction without depending on browser-local artifacts.
- **Preserve the canonical branching model.** Do not flatten conversation history into a single transcript just to simplify storage.
- **Keep one `/chat` route.** Model selection is request/session metadata, not endpoint topology.
- **Send full branch, compact on the server.** The client should not become the model-aware tokenizer or compaction engine, and compaction should checkpoint invocation context rather than rewrite the business record.
- **Separate preference from execution.** The client can express a preferred model; the server decides which provider client actually serves the turn and records the outcome.
- **Record why execution diverged when it does.** If the effective model differs from the preferred model, persist the reason in turn/audit data instead of leaving fallback behavior implicit.
- **Use MAF seams before custom protocol.** Transport metadata, `ChatClientFactory`, and `CompactionProvider` should do most of the work; AGUIDojo only fills the gaps.
- **Persist enough fidelity for the current sample.** Text-only storage is not enough once approvals, tools, data content, and artifacts matter.
- **Keep business identity separate from runtime correlation.** Session identity must not collapse into AG-UI or workflow runtime IDs.
- **Keep chat and business ownership separate.** Start with Todo as the anchor: one Todo can have many related chat sessions, and agents should act on business data through module/application services on behalf of the current user.
- **Prefer thin, portable foundations.** Use the simplest SQL-backed relational model that can grow cleanly across SQLite, SQL Server, and PostgreSQL.
- **Keep docs aligned as code lands.** Each phase should leave README, system design, implementation-plan decisions, and any touched research notes in agreement.

## Phased plan

### Phase 0 -- Restore full-history continuity on `/chat`

**Objective**

Make the unified `/chat` path reliably stateful by always sending the full active-branch history on every turn and every relevant re-entry path.

**Key changes**

- Remove delta-turn submission for the unified `/chat` flow.
- Stop using `StatefulMessageCount` as the basis for outbound history slicing.
- Always assemble and send the full active-branch message history for:
  - normal follow-up turns
  - approval submit/reject paths
  - retry/restart flows
  - edit-and-regenerate flows
  - any other re-entry path that resumes the same session branch
- Treat `ConversationId` and AG-UI thread/run IDs as correlation only, not as permission to omit prior history.
- If prompt-size management is needed later, handle it with an explicit server-side context-window policy, not client-side skipping.
- Treat this full-history invariant as the prerequisite for any later model-picker or server-side compaction work.

**Acceptance / validation**

- A multi-turn conversation on `/chat` retains prior instructions and decisions across second and later turns.
- Approval, rejection, retry, and edit-and-regenerate paths continue from the same full branch context.
- Lightweight regression validation covers at least one ordinary multi-turn flow and one re-entry flow.
- The invariant is clear: the server sees the whole active branch and is free to compact it later under explicit policy.

**Notable risks / dependencies**

- Request payloads will grow; do not prematurely reintroduce client-side truncation.
- Any later token-budget policy must be explicit and server-owned.

### Phase 1 -- Introduce server-owned session identity and Chat Sessions APIs

**Objective**

Move primary session ownership to `AGUIDojoServer` so the sample has a server-issued business session ID and a canonical session lifecycle.

**Key changes**

- Add a Chat Sessions module inside `AGUIDojoServer`.
- Back it with a SQL-first relational store aligned with the modular monolith's primary database family; SQLite may remain the local/sample option, but SQL Server or PostgreSQL should be natural fits for the same model.
- Introduce minimal session lifecycle APIs centered on recovery and archival:
  - list sessions
  - get session summary/detail
  - archive session
- Keep session summary list-friendly: server ID, user-facing title/summary, lifecycle status, created/updated timestamps, primary subject link, and preferred-model metadata when available.
- Keep session detail thin but support-worthy: summary fields plus stable correlation links, latest material approval/model/compaction facts, and counts or pointers for plans, checkpoints, audit entries, and session-scoped artifacts/files.
- Do not inline full message trees, full audit streams, or heavy artifact/file payloads in the root session detail contract; fetch them through dedicated read models or sub-resources only when the sample needs them.
- Let the first persisted `/chat` turn create the server session implicitly when no server-owned session exists yet; blank drafts can remain client-local before first send.
- Have the server issue the primary business session ID on first persisted turn.
- Shift hydration and session listing toward the server-authoritative index.
- Keep browser storage as cache/draft/import support rather than the owner of identity.
- Keep the initial model thin: metadata, lifecycle status, timestamps, stable correlation links, and enough support/debug metadata to inspect or recover a session later without over-designing the API.
- Model the session as chat-module data linked to a business subject from the start (for example `Todo`) so one Todo/business flow can have many related chat sessions.
- Add a small model catalog / registry endpoint (sample-scoped, likely config-backed) so the client can render a future model picker without hardcoding model facts in the browser.
- Leave room in session summary/detail contracts for selected-model metadata as soon as server-owned session identity exists.

**Acceptance / validation**

- The first persisted prompt against a new draft yields a server-owned session ID.
- Refresh or a second browser can recover the server session list without depending on prior browser metadata.
- Session summary/detail is useful for recovery and basic support inspection, not only session-list UX.
- Session summary/detail exposes enough recovery/support metadata to inspect a session without loading the full conversation graph or artifact payloads.
- Archive behavior is represented by the server lifecycle, not only client-side removal.
- The server contracts are ready to surface selected-model metadata once the picker lands.
- Validation remains lightweight: focused server/API checks plus a small end-to-end smoke pass.

**Notable risks / dependencies**

- Do not over-engineer the module into a repo-wide platform component.
- The API contract must keep the current `/chat` sample simple to run locally.
- Do not turn Copilot CLI's `workspace.yaml`, `events.jsonl`, `plan.md`, `checkpoints/`, or `files/` layout into an AGUIDojo storage contract; carry forward the product patterns, not the exact local serialization.

### Phase 2 -- Persist the canonical branching conversation with rich message fidelity

**Objective**

Persist the server-owned conversation as the canonical branching graph, with enough message fidelity to support the current AGUIDojo experience and future extensions.

**Key changes**

- Store the branching conversation graph on the server instead of only in browser IndexedDB.
- Persist explicit branch/leaf state so the active branch is recoverable after reload.
- Preserve richer message fidelity than the current lightweight browser DTOs, including what is needed for:
  - assistant/user text
  - tool/function call and result linkage
  - structured/data content used by current artifacts
  - approval-related message context
  - future multimodal/tool extensibility where practical
- Introduce per-turn model preference transport on the existing `/chat` route using request metadata / forwarded properties.
- Route provider-bound clients per request on the server (for example via `ChatClientFactory` and a model-keyed client cache) so different sessions can target different models without multiple endpoints.
- Persist `preferredModelId` on the session and `effectiveModelId` on assistant turns or turn-level audit facts as soon as model switching is active.
- If the effective model differs from the preferred model, persist a compact reason/audit fact (for example policy block, availability fallback, or explicit auto-routing) instead of leaving the switch implicit.
- Keep `ConversationId`, `threadId`, `runId`, Durable Task IDs, and workflow IDs as linked correlations, not primary keys.
- Rehydrate the client from server-owned branch state first; browser data becomes optional cache/import input.

**Acceptance / validation**

- Edit-and-regenerate creates a durable alternate branch that survives refresh.
- Reloading a session restores the active branch correctly.
- The persisted model keeps enough fidelity for current tool and artifact flows to rehydrate without collapsing to plain text only.
- Different sessions can request different models through the same `/chat` endpoint once the picker lands.
- Recovered session detail can distinguish the preferred model from the effective serving model when they differ materially.

**Notable risks / dependencies**

- Rich message storage can sprawl quickly; persist what the sample needs now, not every possible future payload.
- Browser-to-server import should be best-effort only; do not block on lossless migration from old local formats.
- `ChatOptions.ModelId` alone is not sufficient for provider-bound OpenAI/Azure clients; routing has to swap the underlying client, not only annotate the request.

### Phase 3 -- Persist approvals, audit, artifacts, and session projections

**Objective**

Make server persistence useful for the richer AGUIDojo experience, not just for conversation text.

**Key changes**

- Persist approval requests, decisions, and key decision metadata.
- Persist audit entries and important timestamps tied to approvals, tool actions, and session lifecycle.
- Persist session-scoped support records separately from the root session row: current plan state, compaction checkpoints, session-scoped artifact snapshots/projections, and durable references to session-scoped files or derived documents when they matter for rehydration or debugging.
- Replace or supersede `ContextWindowChatClient` with a server-side compaction pipeline once the sample is ready for model-aware context management.
- Use MAF compaction seams (`CompactionProvider` plus ordered strategies such as tool-result compaction, summarization, sliding window, and truncation) so switching from larger- to smaller-context models is handled server-side.
- Trigger auto-compaction from conservative model-tiered safe-input budgets and run it as queued/background server work where possible; do not mirror Copilot CLI's public ~95% trigger because AGUIDojo needs more reserve for tool outputs, approvals, and richer artifacts.
- Persist session diagnostics as structured audit/support records: model-switch facts, compaction checkpoints/summaries, linked correlation IDs, strategy/outcome metadata, and enough before/after token estimates to explain behavior during debugging.
- Keep the root session detail API thin by surfacing the latest facts, counts, and pointers, while deeper inspection reads audit timelines, checkpoints, plans, and file/artifact references from dedicated projections or sub-resources.
- Prefer queryable relational audit/checkpoint/artifact records over copying Copilot CLI's raw `events.jsonl` or per-session directory shape; add raw journaling later only if implementation proves it materially helpful.
- Persist session-scoped artifact/projection state for the surfaces that matter in the current sample, such as:
  - plans and plan checkpoints/snapshots
  - recipe/shared state
  - document state, preview/diff context, and session-scoped file/document references that materially affect resume or support flows
  - data grid projections
  - other current session artifacts that need durable rehydration
- Define which projections are canonical state versus durable snapshots derived from message history.
- Add best-effort import from current browser-local data where practical.

**Acceptance / validation**

- Reloading a session can restore approval and audit context without relying on the original browser tab.
- Core artifact surfaces reopen with meaningful state instead of only message text.
- Switching to a smaller model does not depend on client-side history slicing; the server compacts or fails explicitly within policy.
- Compaction produces inspectable checkpoint/audit artifacts without rewriting away the canonical branch history.
- If a model fallback or switch occurs, the persisted session can explain preferred versus effective model at the level needed for support/debug.
- Session detail plus dedicated inspection reads can expose current plan state, recent checkpoints, and session-scoped files/artifacts without relying on browser-local state or ad hoc database spelunking.
- Validation remains pragmatic: targeted server checks and focused manual walkthroughs.

**Notable risks / dependencies**

- Artifact scope can balloon; prioritize current sample surfaces over generic replay architecture.
- Avoid turning this phase into a full event-sourcing or perfect-replay effort.
- Avoid double compaction by leaving both `ContextWindowChatClient` and a new compaction pipeline active longer than necessary.
- Do not cargo-cult Copilot CLI's exact ~95% trigger; AGUIDojo's richer session shape needs more headroom.
- Avoid cargo-culting Copilot CLI's exact local session folders, lock files, or append-only log format; AGUIDojo should pick the thinnest server-owned representation that still supports inspection and recovery.

### Phase 4 -- Add simulated ownership and workflow/entity links

**Objective**

Make the persisted sample more realistic by introducing simulated ownership and durable links to workflows or business entities.

**Key changes**

- Add simulated current-user and current-tenant context to server-owned session records and APIs.
- Keep the simulation explicit: seeded dev user, fake tenant, or other sample-only context.
- Add durable links from chat sessions to:
  - workflow instances
  - Durable Task/runtime identifiers
  - relevant business entities where the sample benefits from that realism
- Start with a simple primary-subject pattern (for example `Todo`) so one Todo/business flow can have many related chat sessions.
- Route chat-driven business actions through module/application services on behalf of the current user instead of treating chat persistence as the business source of truth.
- Expose lightweight query or projection support where helpful for the sample UX.
- Preserve the rule that runtime/workflow IDs are linked references, not the primary chat business identity.

**Acceptance / validation**

- Sessions can be associated with simulated owner/tenant context.
- Workflow/entity links survive reload and can be surfaced in the sample where useful.
- No real auth flow, token issuance, tenant onboarding, or production identity plumbing is introduced.

**Notable risks / dependencies**

- This phase can easily drift into real auth design; do not let it.
- Ownership semantics must stay simple enough for local sample execution.

### Phase 5 -- Demote browser storage, document portability, and align operations/docs

**Objective**

Finish the transition to a server-owned sample foundation and keep the operational/documentation story aligned with the implementation.

**Key changes**

- Demote browser storage to cache, draft support, offline convenience, and best-effort import/recovery only.
- Document the SQL-first, relational, cloud-vendor-agnostic persistence model, including SQLite as a local convenience and SQL Server/PostgreSQL as the natural modular-monolith targets.
- Document the model catalog and context-window policy at the same level as the persistence portability story.
- Clarify minimal operational expectations for local data lifecycle, reset, session inspection, and support/debug artifact capture.
- Document the minimal inspection surfaces exposed by the server (summary/detail plus audit/checkpoint/artifact/file views or equivalent read models) and how they support local debugging.
- Document how to inspect persisted audit/model-routing/compaction facts locally so debugging does not depend on browser storage or ad hoc database spelunking.
- Clarify that AGUIDojo's inspection story is server/query-surface based rather than a copy of Copilot CLI's `~/.copilot/session-state` filesystem layout.
- Update README, system design, implementation-facing docs, and any changed research notes as each phase lands so the sample story stays current.
- Reconfirm which optional replay/collaboration capabilities remain deferred.

**Acceptance / validation**

- The sample can be understood and used without depending on pre-existing browser-local state.
- Docs consistently describe unified `/chat`, server-owned sessions, browser cache demotion, and the current persistence story.
- Portability guidance exists at the level needed to avoid repainting the model later.

**Notable risks / dependencies**

- Documentation drift can undo the value of the implementation work.
- Portability work should stay conceptual/model-oriented, not become premature multi-database engineering.

## Cross-cutting implementation notes

- **Session identity model**
  - Primary business session ID is server-issued.
  - Blank drafts may stay client-local until the first persisted `/chat` turn creates the canonical server session.
  - `ConversationId`, AG-UI thread/run IDs, Durable Task IDs, and workflow IDs are stored only as correlations or links.
  - Session summary/detail should expose enough lifecycle and correlation metadata to act as a recovery/support artifact, not only a list-row DTO.

- **Session summary/detail and inspection surfaces**
  - Summary view should stay list-friendly: server ID, title/summary, lifecycle status, created/updated timestamps, primary subject link, preferred-model metadata when set, and a small last-activity hint.
  - Detail view should add stable correlation links plus the latest recovery/support facts such as recent approval state, last effective-model divergence, latest compaction/checkpoint info, active plan presence, and counts or pointers for checkpoints, audit entries, and session-scoped artifacts/files.
  - Heavy conversation graphs, full audit histories, checkpoint bodies, and artifact/file payloads should stay out of the root session DTO and come from dedicated read models or sub-resources when needed.
  - If instruction visibility is added later, surface active source labels and trust state as metadata rather than full instruction bodies.

- **Copilot CLI grounding as reference, not replica**
  - Use the Copilot CLI research to justify durable session identity, requested-versus-effective model separation, checkpointed compaction, session-scoped plans/checkpoints/files as inspectable concepts, and support/debug projections.
  - Do not replicate `workspace.yaml`, `events.jsonl`, `plan.md`, `checkpoints/index.md`, `files/`, lock files, or per-session local databases as AGUIDojo contracts.
  - Prefer relational audit/checkpoint/artifact/file-reference records and server-owned inspection endpoints/query surfaces; add raw journals or filesystem exports only if a later phase proves they help.

- **Model catalog and selection**
  - Selected model is per-session in UX terms, but server-authoritative in architecture terms.
  - Persist the session's `preferredModelId`, and record `effectiveModelId` on assistant turns or audit facts when that becomes useful.
  - When `effectiveModelId` diverges from `preferredModelId`, capture the reason in turn/audit data instead of hiding fallback behavior.
  - Keep the catalog small and sample-scoped (hardcoded or configuration-backed is fine for AGUIDojo).

- **Persistence boundary**
  - Primary persistence is a SQL-first relational store aligned with the modular monolith.
  - SQLite may remain the local/sample option, but avoid SQLite-only behavior so the model maps cleanly to SQL Server or PostgreSQL.

- **Business-module integration**
  - AGUIDojo is a chat module inside a modular monolith, not an isolated persistence island.
  - Store subject-module/entity links on sessions (start with Todo) so one business record can have many related chat sessions.
  - Keep Todo/business data in its owning module and access it through module/application services on behalf of the current user.

- **Transport and routing**
  - Preferred transport seam is AG-UI forwarded/request metadata on `/chat`; if the current `AGUIChatClient` cannot pass it directly, add a thin AGUIDojo extension rather than new endpoints.
  - `ChatOptions.ModelId` is useful metadata but not sufficient on its own for OpenAI/Azure model switching; pair it with a model-keyed `IChatClient` cache via `ChatClientFactory`.

- **Context-window policy**
  - Replace client-side history slicing and fixed message caps with full-history submission plus server-side compaction.
  - `ContextWindowChatClient` is transitional; the target is `CompactionProvider` / `PipelineCompactionStrategy` with model-aware thresholds, checkpointed/background compaction, and summarization/truncation backstops.
  - Compact against a safe input budget after subtracting output and system/tool reserve, with earlier thresholds (roughly 75-85% by model tier) rather than a last-minute hard-limit trigger such as Copilot CLI's public ~95% behavior.
  - Keep compaction artifacts durable: checkpoint summaries, linked audit facts, and before/after estimates should explain what was compressed and why.

- **Browser transition strategy**
  - Continue reading existing browser-local data only as a convenience or best-effort import source during rollout.
  - Import only the session metadata and message history worth preserving when the server has no canonical copy yet; do not promise lossless migration from the current local format.

- **Message fidelity**
  - The current lightweight browser tree format is a useful cache, not a sufficient canonical model.
  - Server persistence should keep enough structure to support approvals, tools, artifacts, and branching without flattening the experience.

- **Instruction layering and prompt safety**
  - If AGUIDojo later adds project, module, session, or workspace instruction files, define a deterministic merge order, surface the active instruction sources in the UI or diagnostics, and label workspace/external sources explicitly.
  - Trust-gate workspace/project instruction-like content before it can influence executed behavior; fetched URLs, uploaded files, and user-authored instruction content remain untrusted context and must not silently override server policy or system instructions.

- **Validation strategy**
  - Favor focused server tests and pragmatic manual checks over broad new test infrastructure.
  - Keep `AGUIDojoClient.Tests` planning deferred unless one of these phases materially changes that need.

- **Documentation discipline**
  - This consolidated plan is the active playbook.
  - Older v1/v2/v3 implementation-plan documents and the retired roadmap issue should be treated as historical context, not the current execution source.
  - README, system design, implementation notes, and any changed research docs should be updated when a phase materially changes the sample story.

## Deferred work

- Real authentication, identity federation, token issuance, tenant onboarding, or production authorization design.
- Full replay engine, participant cursors, mid-run multi-device collaboration, and similar collaboration-first features.
- Comprehensive attachment pipeline work unless needed to support the foundation.
- Large-scale `AGUIDojoClient.Tests` strategy planning.
- Repo-wide platformization of AGUIDojo persistence/session concepts.
- Any feature work that depends on durability but does not help establish the foundation first.

## Resolved design decisions from the March 2026 analysis pass

These questions are now resolved enough to guide implementation. If later code work disproves one of these choices, update this section rather than reviving a second long-form roadmap document.

1. **Thinnest Chat Sessions API and schema**
   - Start with a SQL-first relational model centered on `chat_sessions`, canonical branching message-node storage, subject/entity links, audit events, and durable artifact/projection records.
   - Keep the initial server API thin: list sessions, get summary/detail, archive session, and let `/chat` create the canonical session on first persisted turn.
   - Keep summary/detail layered: summary/list fields stay small, while detail adds correlation links plus the latest support/debug facts and counts or pointers for plans, checkpoints, audit entries, and session-scoped artifacts/files.
   - Full audit/checkpoint/file bodies should remain separate read models or sub-resources rather than bloating the root session DTO.
   - Store `preferredModelId` on the session summary/detail model, and `effectiveModelId` on assistant turns or audit events rather than bloating the session root with provider-specific detail.
   - Treat the Copilot CLI session-state schema reference and session-topology note as grounding for inspectable session surfaces and the catalog-plus-workspace split, not as a storage blueprint; AGUIDojo should not replicate `workspace.yaml`, `events.jsonl`, `plan.md`, or per-session folders as its contract.

2. **Session creation behavior**
   - The default UX should keep blank drafts local and create the server-owned session implicitly on the first persisted `/chat` turn.
   - A separate explicit create endpoint is deferred unless a later workflow truly needs server-owned empty drafts before the first message.

3. **Rich-message and model/compaction metadata scope**
   - Persist the current sample's real needs: role/text/author identity, structured `AIContent`, tool call/result linkage, approval context, preferred/effective model IDs plus divergence reason facts, compaction summary/checkpoint references, and before/after token estimates where they materially aid debugging.
   - Avoid provider-specific transient payloads, chunk-level stream trivia, and speculative multimodal fields that the sample does not yet use.

4. **First durable artifact and audit scope**
   - The first rich-persistence milestone should durably preserve approvals and decisions, audit entries, plan state, checkpoint summaries, recipe/shared state, document preview/diff context, session-scoped file/document references, data-grid projections, material model-switch / compaction events, and the core diagnostics/support facts needed to inspect a session later.
   - Chart/form demo outputs and other purely re-renderable surfaces can remain derived or deferred unless later implementation proves they hold unique state worth saving.

5. **Browser-local import scope**
   - Import only the metadata and message history worth preserving when the server does not already own the session, plus selected-model metadata if the client has it by then.
   - Do not import transient streaming state, unread counts, pending-approval badges, `StatefulMessageCount`, or promise lossless artifact migration from the existing local cache format.

6. **Thinnest client transport change for model preference**
   - Add a small AGUIDojo-specific wrapper or extension around `AGUIChatClient` / its factory so the client can populate forwarded request metadata such as `modelId`.
   - Do not fork the AG-UI protocol and do not add per-model chat endpoints.

7. **Compaction safety margin**
   - Compute a safe input budget per model: `context window - reserved output - system/tool reserve`.
   - Implement compaction as checkpointed/background work against the invocation context rather than destructive rewriting of the canonical conversation.
   - Compact earlier than the model hard limit, using a practical starting policy of about 85% for large-window models, 80% for medium-window models, and 75% for smaller-window models instead of copying Copilot CLI's public ~95% trigger.

8. **Concurrent cross-device writes**
   - Use optimistic concurrency as the default rule.
   - If a submitted parent leaf is stale, auto-branch conversation writes instead of silently overwriting prior turns.
   - Reject stale metadata/projection writes with refetch-and-retry rather than hidden merges.

9. **Minimum documentation and ops updates per phase**
   - When a phase materially changes the sample story, update the root AGUIDojo README, `.docs/system-design.md`, this implementation plan, and any research or operational note whose assumptions changed.
   - No separate long-form roadmap document should be kept in parallel with the implementation plan.

10. **Future instruction-like feature guardrails**
     - Defer workspace/project/session instruction layering until the durable session foundation exists.
     - When instruction-like features are added, ship them with deterministic source ordering, visible active sources/labels, trust confirmation for workspace-derived content, and hard server-policy precedence over user/workspace instructions.

11. **Operational inspection surfaces**
    - Keep the root session API thin: session detail surfaces the latest support/debug facts plus counts or pointers for plans, checkpoints, audit entries, and session-scoped artifacts/files.
    - Provide deeper inspection through dedicated query/projection surfaces rather than stuffing full event streams or artifact payloads into the session row.
    - Local debugging should depend on those server-owned reads and normal database inspection, not on browser-local state or a Copilot-style filesystem tree.

## Definition of done for the consolidated plan

The consolidated plan is complete when AGUIDojo can be evolved phase by phase with a single shared understanding that:

- `/chat` always preserves within-session continuity by sending full active-branch history.
- The server owns chat session identity and lifecycle through a Chat Sessions module.
- The Chat Sessions module fits the modular-monolith boundary: sessions link to business subjects (starting with Todo), one business flow can have many chats, and agents work through module/application services on behalf of the current user.
- Per-session model preference can be expressed on the single `/chat` route without multiplying endpoints.
- The server routes model requests and owns context-window policy through compaction, not client-side history slicing.
- A SQL-first relational store underpins the Chat Sessions module; SQLite may support local runs, while SQL Server or PostgreSQL are the natural modular-monolith targets.
- Canonical branching conversation state lives on the server with enough fidelity for the current sample.
- Approvals, audit, and key artifact/projection state are durably recoverable.
- Durable session state also serves recovery/support needs: audit, model-routing facts, compaction checkpoints, and correlation links are recoverable without browser-local state.
- Thin session summary/detail plus dedicated inspection reads expose current plan/checkpoint/artifact/file state without depending on browser-local storage or a copied Copilot-style session filesystem.
- Selected/effective model and material model-switch / compaction facts are recoverable at the level the sample needs.
- Ownership and integration links are modeled as simulated sample concerns, not real auth plumbing.
- Browser storage is secondary.
- Optional replay/collaboration features remain deferred unless they become foundational.
- README, system design, implementation plan, and supporting research remain aligned, so AGUIDojo has one current story instead of another round of v1/v2/v3 plan sprawl.
