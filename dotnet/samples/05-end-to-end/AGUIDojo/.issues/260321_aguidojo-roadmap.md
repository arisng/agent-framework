---
date: 2026-03-21
type: Feedbacks
status: Open
---

# AGUIDojo sample roadmap: continuity, model-aware chat, persistence, and integration alignment

## Reframed problem statement

The current note is directionally useful, but it is too narrow if treated mainly as an "implement AG-UI features and tool wrappers" checklist. AGUIDojo now needs a sample-wide roadmap for the unified `/chat` architecture across `dotnet/samples/05-end-to-end/AGUIDojo`, because the next work is not just about adding more feature demos. It is about stabilizing conversation continuity, making model selection and context-window governance part of the normal chat experience, moving session ownership to the server, defining the persistence boundary, sequencing backend/domain realism after that foundation, and keeping sample documentation and operational guidance aligned with the architecture we actually want to demonstrate.

The first priority remains the verified within-session memory bug on the unified `/chat` path. That must be fixed before AGUIDojo enables per-session model switching or invests in broader persistence, because a server-side store and model picker should not sit on top of already-truncated conversation history.

## Scope guardrails

- **Scope stays inside AGUIDojo** under `dotnet/samples/05-end-to-end/AGUIDojo`; this is not a repo-wide platform plan.
- **Auth/identity is simulated only** for this roadmap. If ownership or authorization needs to appear, model it as seeded or fake current-user and current-tenant context inside the sample. Do **not** plan real auth flows, token issuance, tenant onboarding, or production identity plumbing here.
- **SQLite is the sample persistence choice** to keep AGUIDojo lightweight to run and easy to reason about. The relational model should stay portable so the same concepts can later map to SQL Server or PostgreSQL.
- **Browser storage is secondary**. `localStorage` and IndexedDB remain useful as cache, draft storage, or best-effort import sources, but not as the primary business record.
- **Model picker is a standard AGUIDojo chat feature**, but the model catalog, routing rules, and budget math stay sample-local. Do **not** turn this issue into a repo-wide model-routing abstraction.
- **Context-window budgeting and compaction are server-owned concerns**. The client may display the selected model, budget hints, or warnings, but it must not trim canonical history, summarize turns, or enforce token budgets on its own.
- **Use MAF seams where they already exist**. Prefer existing `ChatClientFactory`, request-time model metadata/overrides, and the compaction pipeline as enabling seams. Fill only the AGUIDojo-specific gaps needed to ship the sample.
- **AG-UI, DurableTask, and workflow IDs remain correlation/runtime-only**. `threadId`, `runId`, `ConversationId`, durable session IDs, and workflow instance IDs are useful links, but they must not become the primary business session identity.
- **Testing discussion stays light in this issue**. Capture only the minimum regression validation needed to make each phase safe. A dedicated `AGUIDojoClient.Tests` strategy is intentionally deferred to a later session.
- **Optional replay/collaboration features are not v1 blockers**. Multi-device cursors, mid-run reconnect/join, rich attachment pipelines, and fine-grained event replay may be tracked as later questions, not prerequisites for the first persistence milestone.

## Current architecture snapshot

AGUIDojo currently runs as a unified sample around a single `POST /chat` route:

- `AGUIDojoClient` is a Blazor Server BFF that streams AG-UI traffic directly to `AGUIDojoServer` `/chat`, keeps per-session UI state in Fluxor, and uses YARP for `/api/*` business endpoints.
- The client already surfaces rich session experiences: chat history, plans, approvals, recipe/shared state, document previews, charts, forms, data grids, notifications, and background-session switching.
- The live source of truth for session state is still the client/store layer. Browser persistence currently uses `localStorage` for lightweight metadata and active session selection, plus IndexedDB for conversation trees.
- Chat model selection is still effectively startup-owned. The sample does not yet expose a server-authoritative selected model per session, a model catalog API, or model-aware request routing.
- Prompt-size control is still coarse and client-shaped rather than model-specific. The current sample behaves more like it has one generic history window than multiple model-specific context budgets.
- `AGUIDojoServer` hosts the unified agent route plus business APIs and tool wrappers, but it does not yet expose a canonical Chat Sessions API, a server-owned primary chat-session store, or a sample-local model registry.
- Durable Task and workflow capabilities exist in the wider repo, but inside AGUIDojo they should be treated as runtime and execution concerns, not the product-facing chat-session system of record.

This means AGUIDojo already demonstrates many AG-UI capabilities, but the sample's foundations are still uneven: transport continuity, authoritative session ownership, model selection, context-window policy, durable persistence, and integration boundaries are not yet mature enough for the roadmap to stay focused only on feature demos.

## Critical chat continuity issue

This is the first implementation priority.

The unified `/chat` path currently behaves as though the backend can remain conversation-stateful after the first turn and accept only delta messages. In practice, the AG-UI flow used here expects the **full active-branch message history on every turn**. The current client pipeline tracks `ConversationId`, sets `StatefulMessageCount`, and then slices outbound messages with `Skip(session.StatefulMessageCount)` on later turns. Once a prior turn succeeds, that usually removes earlier user/assistant messages and leaves only the newest turn going back to the server.

Result: second and later turns within the same session can lose the context that the model needs to remember prior instructions, tool results, or decisions.

### Direction for the fix

- Stop delta-turn submission on the unified `/chat` path.
- Always send the full active-branch message history to `AGUIChatClient` on every turn.
- Treat `ConversationId` and AG-UI `threadId` as correlation metadata only, not as permission to omit prior messages.
- Audit approval, retry, edit-and-regenerate, and other re-entry paths so they preserve full history as well.
- Keep this phase deliberately narrow: do **not** hide the bug with client-side trimming, summarization, or early model-switch logic.
- If prompt-size control is needed later, handle it through explicit server-side context-window policy keyed by the selected model rather than client-side history skipping.

## Model selection and context-window gap

AGUIDojo currently treats model choice as an infrastructure decision fixed at startup. The consolidated research says that is the wrong mental model for this sample. A model picker is a normal chat feature because users need to choose cost/quality/speed trade-offs during real conversations, and the selected model directly changes context-window size, output reserve, and how aggressively history must be compacted.

For AGUIDojo scope, that does **not** require a platform-wide model marketplace. It requires a sample-local model catalog plus server-owned rules for how each model is budgeted and invoked.

### Direction for AGUIDojo

- Add a sample-local model catalog exposed by `AGUIDojoServer`; each entry should define model ID, display name, deployment/provider key, context-window tokens, reserved output budget, capability flags, and a default compaction tier.
- Make the selected model part of chat-session metadata and server-owned session state, not just ephemeral client UI state.
- Expose the model picker in the standard chat surface once Phase 0 is stable; do **not** position it as an advanced-only control.
- Budget prompt input per model on the server using a clear formula such as: safe input budget = context window - reserved output budget - system/tool reserve - safety margin.
- Use model-specific tiers rather than one fixed history limit; larger-window models can preserve more raw history, while smaller-window models require earlier compaction.
- Let the client render informative badges or warnings, but keep the final budget decision authoritative on the server.

## MAF seams and AGUIDojo-owned gaps

MAF already provides useful enabling seams, but not the whole sample feature.

- **Model routing seam**: request-time model metadata and `ChatClientFactory` are the main seams AGUIDojo should use for per-session model selection instead of a single startup-fixed chat client.
- **Compaction seam**: the compaction pipeline gives AGUIDojo a server-side place to apply summarization/reduction policies before each model invocation.
- **Token-aware reduction seam**: MAF already points AGUIDojo toward token-aware compaction instead of a fixed message-count cap, which is the right fit for multi-model chat.
- **AGUIDojo-owned gaps remain explicit**: the sample still needs a model catalog/registry, per-session selected-model persistence, dynamic thresholds/policy changes when the user switches models, and any session-level audit record for compaction decisions.
- **Roadmap implication**: use the existing seams first, but keep the missing pieces sample-local. This issue should not become a hidden framework backlog unless AGUIDojo later proves a reusable need.

## Session persistence and integration gap

AGUIDojo's current browser-local persistence is useful as a convenience layer, but it is not enough for the roadmap the sample is now pointing toward.

Today the persisted model is intentionally lightweight and incomplete:

- session metadata is stored in `localStorage`
- active session identity is stored in `localStorage`
- conversation trees are stored in IndexedDB
- hydrated state does not recreate the full fidelity of approvals, audit history, plans, recipe state, document state, data grids, or other rich AI/session surfaces
- session identity is still effectively client-owned rather than server-issued
- selected model and context-window policy are not durably owned by the server

That is acceptable for a local cache, but not for:

- cross-device session resume
- server-authoritative session listing, hydration, and archive
- model-aware chat continuity across turns and devices
- linking sessions to workflows or business entities
- preserving approvals, audit history, artifact state, and compaction decisions as durable server-side data
- evolving AGUIDojo toward a more realistic application boundary

### Persistence direction for AGUIDojo

AGUIDojo should add a **server-owned Chat Sessions module** inside `AGUIDojoServer` and use it as the primary business owner of:

- session metadata and lifecycle
- selected model and default budget/compaction policy selection
- canonical branching conversation state
- approvals and audit entries
- artifact snapshots and projections
- workflow and business links
- simulated ownership context

For sample scope, the system of record should be **SQLite**. The schema should remain relational and portable so it can later map cleanly to SQL Server or PostgreSQL without rethinking the domain model.

Key boundary decisions:

- the **server** issues and owns the primary business session ID and the authoritative selected model state
- browser storage becomes **cache and import only**
- the canonical conversation stays a **branching graph**, not a flattened transcript
- context-window budgeting and compaction policy are evaluated on the **server** using model metadata and token estimation
- rich message payloads and artifact/governance state become first-class server data over time
- AG-UI thread and run IDs, DurableTask identifiers, and workflow IDs stay as **linked runtime references**, not the primary business key

## Sample-wide maturity / gap matrix

| Area | Current shape | Gap / risk | Roadmap stance |
| --- | --- | --- | --- |
| Conversational continuity | Unified `/chat` route is in place, but later turns can be sliced by `StatefulMessageCount`. | The sample loses within-session memory and misuses the transport contract. | Fix first by always sending full active-branch history. |
| Model selection / routing | Chat behaves as though one startup-selected model is enough for the sample. | No standard model picker, per-session model state, or server-owned model routing. | Treat model picker as standard chat capability in Phase 1 once continuity is stable. |
| Context-window budgeting and compaction | History limits are coarse and not model-specific. | Budgets can under- or over-preserve context, and client heuristics can break continuity. | Move to server-owned, model-specific token budgets and compaction tiers in Phase 2. |
| Session ownership | Session lifecycle is effectively client-owned. | No server-authoritative create/list/get/archive model or server-issued primary session ID. | Add a server-owned Chat Sessions module before deepening realism work. |
| Persistence model | Browser persistence uses `localStorage` + IndexedDB and is lossy. | No cross-device continuity, incomplete artifact/governance durability, weak auditability, and no durable selected-model state. | Use SQLite as AGUIDojo's system of record and keep browser storage secondary. |
| Runtime correlation | `ConversationId`, AG-UI run and thread IDs, and future workflow IDs are available. | Easy to conflate runtime IDs with business identity. | Store them as correlation and runtime references only. |
| MAF leverage and gaps | MAF already offers routing and compaction seams AGUIDojo can consume. | AGUIDojo does not yet wire them end to end and still needs sample-local registry/threshold/persistence decisions. | Use the seams, but keep the missing pieces explicit and sample-scoped. |
| Artifact and governance state | Plans, approvals, recipe state, document previews, charts, grids, and notifications exist in the UI flow. | They are not yet durably modeled as first-class server session data, and compaction decisions are not yet part of the audit story. | Persist these after the base session and context policy foundations are in place. |
| Backend and domain realism | Business endpoints and tool wrappers exist, but ownership and integration boundaries are still informal. | The sample can become unrealistic if domain linking grows without durable session and model foundations. | Sequence workflow and entity linking after continuity, session ownership, and model-aware persistence are established. |
| Documentation and operations | README reflects the unified sample, while some older internal notes lag. | Execution can drift if docs, terminology, model configuration, and operational guidance do not match the intended architecture. | Keep docs and operational guidance aligned as each phase lands. |

## Phased roadmap

### Phase 0 -- Restore conversational continuity

- Remove client-side delta-turn submission from the unified `/chat` flow.
- Send full active-branch history on every request.
- Audit edit/regenerate, approval, retry, and cancel/restart paths for the same guarantee.
- Keep this phase narrow: no client-owned trimming, summarization, or model-switch behavior that would obscure the transport contract.
- Add only the minimum regression validation needed to prove that within-session memory now survives multi-turn conversations.

### Phase 1 -- Introduce server-owned chat-session identity, model selection, and APIs

- Add a Chat Sessions module in `AGUIDojoServer`.
- Support create, list, get, and archive semantics for chat sessions.
- Move primary session-id issuance from client-side generation to the server.
- Include the selected model in server-owned session metadata from day one, even if the first milestone only supports a small curated catalog.
- Add a sample-local model catalog/registry that defines model ID, display name, deployment/provider key, context window, reserved output budget, capability flags, and default compaction tier.
- Expose server APIs for session hydration and available models so the standard chat UI can render and persist model selection.
- Wire the client model picker as part of normal chat controls after Phase 0 lands, but keep the server authoritative over allowed models and default selection.
- Route requests through `ChatClientFactory` and request-time model metadata/overrides rather than a single startup-fixed chat client.
- Keep browser storage as cache, draft, and import support rather than authoritative identity.

### Phase 2 -- Persist the canonical branching conversation and server-owned context policy

- Persist the branching conversation graph on the server rather than only a browser-local tree.
- Preserve enough message fidelity to keep future tool, function, and multimodal extensions viable.
- Track the active leaf, current branch, and selected model explicitly.
- Replace fixed message-count heuristics with model-specific token budgets evaluated on the server.
- Use a clear budgeting formula such as: safe input budget = context window - reserved output budget - system/tool reserve - safety margin.
- Adopt server-owned compaction policies by model tier using the MAF compaction pipeline seam; larger-window models should preserve more raw history, while smaller-window models can trigger more aggressive reduction.
- Re-evaluate and compact immediately when a session switches to a smaller-context model so a valid next turn never depends on client trimming.
- Store AG-UI, DurableTask, and workflow identifiers only as linked runtime correlations.

### Phase 3 -- Persist approvals, audit, artifacts, and compaction events

- Persist approval requests and outcomes.
- Persist audit trail entries and key operational timestamps.
- Persist compaction decisions as first-class audit data where useful: triggering reason, selected policy, before/after token estimates, and any summary lineage or references worth keeping.
- Persist session-scoped artifacts such as plan state, recipe/shared state, document state, and data-grid or document projections where they are part of the sample experience.
- Define best-effort import from current browser-local data where practical, without requiring lossless migration from the existing client cache format.

### Phase 4 -- Add simulated ownership context and integration links

- Model simulated current-user and current-tenant ownership on server records and APIs.
- Add session links to workflows and business entities where that helps the sample become more realistic.
- Expose lightweight integration points or events if helpful for AGUIDojo's modular-monolith direction.
- Keep all auth and identity behavior explicitly simulated. Do **not** turn this phase into real authentication or tenant plumbing.

### Phase 5 -- Operationalize portability, cache behavior, and roadmap alignment

- Fully demote browser persistence to cache, offline convenience, and import support.
- Document how the SQLite-backed sample model maps to SQL Server or PostgreSQL later.
- Document how new models are added, how default model selection works, and how context-window/compaction budgets are derived in AGUIDojo scope.
- Decide which optional reconnect, replay, and collaboration features remain deferred.
- Update README, issue notes, and operational guidance so the sample story stays aligned with the implemented architecture.
- Continue to defer a dedicated `AGUIDojoClient.Tests` strategy to separate planning unless scope materially changes.

## Acceptance criteria

This rewrite should be considered actionable only if future execution can proceed with the following shared understanding:

- [ ] The roadmap is explicitly sample-wide for `dotnet/samples/05-end-to-end/AGUIDojo`, not a narrow AG-UI feature checklist.
- [ ] The within-session memory and continuity bug on `/chat` is the **first** implementation priority.
- [ ] The roadmap treats model picker as a **standard chat capability** backed by server-owned session state, not as an advanced afterthought.
- [ ] The roadmap calls for a **server-owned Chat Sessions module** as the primary business session owner.
- [ ] **SQLite** is the persistence choice for AGUIDojo sample scope, while the model stays relational and portable to SQL Server and PostgreSQL later.
- [ ] Browser storage is treated as cache and import support only, not the primary source of truth.
- [ ] Context-window budgeting is **model-specific and server-owned**, with explicit reserve/safety-margin thinking rather than fixed client history limits.
- [ ] Compaction and summarization policy are **server-owned**, with MAF seams used where they already fit the sample.
- [ ] The roadmap explicitly distinguishes MAF enabling seams (`ChatClientFactory`, request-time model metadata/overrides, compaction pipeline) from AGUIDojo-owned gaps (model catalog, per-session selected-model persistence, policy thresholds, compaction audit decisions).
- [ ] AG-UI thread and run IDs, DurableTask identifiers, and workflow IDs are treated as correlation and runtime-only identifiers, not the primary business session ID.
- [ ] Any ownership or authorization behavior is framed as **simulated only** inside AGUIDojo scope.
- [ ] The roadmap includes continuity, model selection/routing, canonical message persistence, approvals, audit, compaction events, artifacts, workflow/entity linking, documentation and operational alignment, and backend/domain realism sequencing.
- [ ] Validation expectations stay lightweight here, and a dedicated `AGUIDojoClient.Tests` strategy remains deferred.

## Open questions

1. What is the thinnest model catalog/registry shape that keeps AGUIDojo simple while still carrying context-window, output reserve, capability flags, and deployment routing?
2. When model switches happen mid-session, should AGUIDojo compact immediately, branch automatically, or block the switch until the next turn if the new model budget would be exceeded?
3. How much compaction detail needs to be durably recorded for sample auditability without turning AGUIDojo into a full observability platform?
4. What is the thinnest Chat Sessions abstraction that keeps SQLite simple in the sample while remaining portable to SQL Server or PostgreSQL later?
5. How should simulated current-user and current-tenant context appear in server APIs and persisted chat-session records without turning into real auth plumbing?
6. How much of today's lossy browser-local session data should be imported into the server store once the Chat Sessions module exists?
7. For concurrent cross-device changes later, should stale writes auto-branch by default or produce an explicit conflict?
8. Which artifact surfaces must be durable in the first server-backed milestone versus later follow-up work?
9. Do we need any optional replay or resume features in early AGUIDojo milestones, or should participant cursors, mid-run reconnect, richer attachment handling, and fine-grained event journaling remain explicitly deferred?
10. What lightweight documentation and operational updates should be required at the end of each phase so the sample story never drifts from the implementation?

## Suggested execution notes

- Start with the memory bug before introducing new persistence behavior or model-switch UX.
- Once Phase 0 is stable, treat model picker as baseline chat UX backed by server session state rather than as an advanced-only settings panel.
- Keep context-window budgeting, compaction, and summary generation on the server; the client may only display state and warnings.
- Use existing MAF seams first (`ChatClientFactory`, request-time model metadata/overrides, compaction pipeline) before inventing AGUIDojo-specific wrappers around them.
- Do not let tool-by-tool implementation work outrun the session, model, and persistence foundations.
- Preserve focus on the unified `/chat` sample shape already described in the current AGUIDojo README.
- Preserve existing capability-wrapper organization unless a later phase proves a clearer domain or maturity split is worth it.
- Treat this issue as the execution roadmap for AGUIDojo sample evolution, not as a request to implement real production auth or repository-wide platform infrastructure.

## Reference points in the sample

- `README.md`
- `.docs/research/aguidojo-llm-picker-architecture-and-maf-alignment.md`
- `AGUIDojoClient/Program.cs`
- `AGUIDojoClient/Components/Pages/Chat/Chat.razor`
- `AGUIDojoClient/Services/AgentStreamingService.cs`
- `AGUIDojoClient/Services/SessionPersistenceService.cs`
- `AGUIDojoClient/Store/SessionManager/SessionHydrationEffect.cs`
- `AGUIDojoServer/Program.cs`
