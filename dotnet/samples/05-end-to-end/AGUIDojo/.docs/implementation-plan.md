# AGUIDojo implementation plan

## Purpose

This is the consolidated execution plan for evolving the AGUIDojo sample. It supersedes the older v1/v2/v3 implementation-plan sprawl and is aligned to the current roadmap (`.issues/260321_aguidojo-roadmap.md`), the current README, and the unified `POST /chat` implementation. It is an execution playbook, not a duplicate system design.

The plan should stay synchronized with the sample as each phase lands. README, roadmap notes, and implementation must describe the same architecture and sequencing.

## Scope guardrails

- **AGUIDojo scope only.** This plan applies only to `dotnet/samples/05-end-to-end/AGUIDojo`.
- **Auth and identity stay simulated.** Use seeded or fake current-user/current-tenant context when needed; do not turn this into a real auth rollout plan.
- **SQLite is the sample persistence choice.** Keep the model relational and portable to SQL Server or PostgreSQL later.
- **Browser storage becomes secondary over time.** `localStorage` and IndexedDB remain useful for cache, drafts, and best-effort import, not as the long-term system of record.
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
   - There is no canonical create/list/get/archive session API surface.
   - Cross-device resume, durable hydration, archival, and server-authoritative linking to workflows or business entities are therefore not in place.

## Guiding principles

- **Fix continuity before durability.** Do not harden persistence around already-truncated turn submission.
- **Move ownership to the server incrementally.** Keep the sample runnable while shifting the source of truth from client/browser to server/SQLite.
- **Preserve the canonical branching model.** Do not flatten conversation history into a single transcript just to simplify storage.
- **Persist enough fidelity for the current sample.** Text-only storage is not enough once approvals, tools, data content, and artifacts matter.
- **Keep business identity separate from runtime correlation.** Session identity must not collapse into AG-UI or workflow runtime IDs.
- **Prefer thin, portable foundations.** Use the simplest SQLite-backed relational model that can grow cleanly into SQL Server/PostgreSQL later.
- **Keep docs aligned as code lands.** Each phase should leave README, roadmap notes, and implementation in agreement.

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

**Acceptance / validation**

- A multi-turn conversation on `/chat` retains prior instructions and decisions across second and later turns.
- Approval, rejection, retry, and edit-and-regenerate paths continue from the same full branch context.
- Lightweight regression validation covers at least one ordinary multi-turn flow and one re-entry flow.

**Notable risks / dependencies**

- Request payloads will grow; do not prematurely reintroduce client-side truncation.
- Any later token-budget policy must be explicit and server-owned.

### Phase 1 -- Introduce server-owned session identity and Chat Sessions APIs

**Objective**

Move primary session ownership to `AGUIDojoServer` so the sample has a server-issued business session ID and a canonical session lifecycle.

**Key changes**

- Add a Chat Sessions module inside `AGUIDojoServer`.
- Back it with SQLite as the sample system of record.
- Introduce minimal session lifecycle APIs, such as:
  - create session
  - list sessions
  - get session summary/detail
  - archive session
- Have the server issue the primary business session ID.
- Shift hydration and session listing toward the server-authoritative index.
- Keep browser storage as cache/draft/import support rather than the owner of identity.
- Keep the initial model thin: metadata, lifecycle status, timestamps, and enough server linkage to support later phases.

**Acceptance / validation**

- New sessions receive a server-owned session ID.
- Refresh or a second browser can recover the server session list without depending on prior browser metadata.
- Archive behavior is represented by the server lifecycle, not only client-side removal.
- Validation remains lightweight: focused server/API checks plus a small end-to-end smoke pass.

**Notable risks / dependencies**

- Do not over-engineer the module into a repo-wide platform component.
- The API contract must keep the current `/chat` sample simple to run locally.

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
- Keep `ConversationId`, `threadId`, `runId`, Durable Task IDs, and workflow IDs as linked correlations, not primary keys.
- Rehydrate the client from server-owned branch state first; browser data becomes optional cache/import input.

**Acceptance / validation**

- Edit-and-regenerate creates a durable alternate branch that survives refresh.
- Reloading a session restores the active branch correctly.
- The persisted model keeps enough fidelity for current tool and artifact flows to rehydrate without collapsing to plain text only.

**Notable risks / dependencies**

- Rich message storage can sprawl quickly; persist what the sample needs now, not every possible future payload.
- Browser-to-server import should be best-effort only; do not block on lossless migration from old local formats.

### Phase 3 -- Persist approvals, audit, artifacts, and session projections

**Objective**

Make server persistence useful for the richer AGUIDojo experience, not just for conversation text.

**Key changes**

- Persist approval requests, decisions, and key decision metadata.
- Persist audit entries and important timestamps tied to approvals, tool actions, and session lifecycle.
- Persist session-scoped artifact/projection state for the surfaces that matter in the current sample, such as:
  - plans
  - recipe/shared state
  - document state and preview/diff context
  - data grid projections
  - other current session artifacts that need durable rehydration
- Define which projections are canonical state versus durable snapshots derived from message history.
- Add best-effort import from current browser-local data where practical.

**Acceptance / validation**

- Reloading a session can restore approval and audit context without relying on the original browser tab.
- Core artifact surfaces reopen with meaningful state instead of only message text.
- Validation remains pragmatic: targeted server checks and focused manual walkthroughs.

**Notable risks / dependencies**

- Artifact scope can balloon; prioritize current sample surfaces over generic replay architecture.
- Avoid turning this phase into a full event-sourcing or perfect-replay effort.

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
- Document the SQLite-backed persistence model and how it maps cleanly to SQL Server/PostgreSQL later.
- Clarify minimal operational expectations for local data lifecycle, reset, and inspection.
- Update README, roadmap notes, and implementation-facing docs as each phase lands so the sample story stays current.
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
  - `ConversationId`, AG-UI thread/run IDs, Durable Task IDs, and workflow IDs are stored only as correlations or links.

- **Persistence boundary**
  - SQLite is the system of record for AGUIDojo sample scope.
  - Keep schema choices portable: avoid coupling the domain model to SQLite-only behavior where possible.

- **Browser transition strategy**
  - Continue reading existing browser-local data only as a convenience or best-effort import source during rollout.
  - Do not promise lossless migration from the current local format.

- **Message fidelity**
  - The current lightweight browser tree format is a useful cache, not a sufficient canonical model.
  - Server persistence should keep enough structure to support approvals, tools, artifacts, and branching without flattening the experience.

- **Validation strategy**
  - Favor focused server tests and pragmatic manual checks over broad new test infrastructure.
  - Keep `AGUIDojoClient.Tests` planning deferred unless one of these phases materially changes that need.

- **Documentation discipline**
  - This consolidated plan is the active playbook.
  - Older v1/v2/v3 implementation-plan documents should be treated as historical context, not the current execution source.
  - README, roadmap, and implementation notes should be updated when a phase materially changes the sample story.

## Deferred work

- Real authentication, identity federation, token issuance, tenant onboarding, or production authorization design.
- Full replay engine, participant cursors, mid-run multi-device collaboration, and similar collaboration-first features.
- Comprehensive attachment pipeline work unless needed to support the foundation.
- Large-scale `AGUIDojoClient.Tests` strategy planning.
- Repo-wide platformization of AGUIDojo persistence/session concepts.
- Any feature work that depends on durability but does not help establish the foundation first.

## Open questions / decisions to revisit

1. What is the thinnest Chat Sessions API and SQLite schema that keeps the sample simple while staying portable later?
2. Should session creation be explicit before first prompt, or should the first `/chat` turn create the server session implicitly?
3. What exact rich-message shape is sufficient for the current sample without over-designing for future multimodal cases?
4. Which artifact surfaces must be durable in the first rich-persistence milestone, and which can remain derived or deferred?
5. How much of today's browser-local data is worth importing once server persistence exists?
6. If the same server-owned session changes from multiple browsers later, should conflicts auto-branch, reject stale writes, or use another lightweight rule?
7. What minimum README/roadmap/ops updates should be required at the end of every phase to prevent drift?

## Definition of done for the consolidated plan

The consolidated plan is complete when AGUIDojo can be evolved phase by phase with a single shared understanding that:

- `/chat` always preserves within-session continuity by sending full active-branch history.
- The server owns chat session identity and lifecycle through a Chat Sessions module.
- SQLite is the sample system of record, with a portable relational model.
- Canonical branching conversation state lives on the server with enough fidelity for the current sample.
- Approvals, audit, and key artifact/projection state are durably recoverable.
- Ownership and integration links are modeled as simulated sample concerns, not real auth plumbing.
- Browser storage is secondary.
- Optional replay/collaboration features remain deferred unless they become foundational.
- README, roadmap, and implementation remain aligned, so AGUIDojo has one current story instead of another round of v1/v2/v3 plan sprawl.
