---
date: 2026-03-21
type: Feedbacks
status: Open
---

# AGUIDojo sample roadmap: continuity, persistence, and integration alignment

## Reframed problem statement

The current note is directionally useful, but it is too narrow if treated mainly as an "implement AG-UI features and tool wrappers" checklist. AGUIDojo now needs a sample-wide roadmap for the unified `/chat` architecture across `dotnet/samples/05-end-to-end/AGUIDojo`, because the next work is not just about adding more feature demos. It is about stabilizing conversation continuity, moving session ownership to the server, defining the persistence boundary, sequencing backend/domain realism after that foundation, and keeping sample documentation and operational guidance aligned with the architecture we actually want to demonstrate.

The first priority is the verified within-session memory bug on the unified `/chat` path. That must be fixed before AGUIDojo deepens feature coverage or invests in broader persistence, because a server-side store should not institutionalize already-truncated conversation history.

## Scope guardrails

- **Scope stays inside AGUIDojo** under `dotnet/samples/05-end-to-end/AGUIDojo`; this is not a repo-wide platform plan.
- **Auth/identity is simulated only** for this roadmap. If ownership or authorization needs to appear, model it as seeded or fake current-user and current-tenant context inside the sample. Do **not** plan real auth flows, token issuance, tenant onboarding, or production identity plumbing here.
- **SQLite is the sample persistence choice** to keep AGUIDojo lightweight to run and easy to reason about. The relational model should stay portable so the same concepts can later map to SQL Server or PostgreSQL.
- **Browser storage is secondary**. `localStorage` and IndexedDB remain useful as cache, draft storage, or best-effort import sources, but not as the primary business record.
- **AG-UI, DurableTask, and workflow IDs remain correlation/runtime-only**. `threadId`, `runId`, `ConversationId`, durable session IDs, and workflow instance IDs are useful links, but they must not become the primary business session identity.
- **Testing discussion stays light in this issue**. Capture only the minimum regression validation needed to make each phase safe. A dedicated `AGUIDojoClient.Tests` strategy is intentionally deferred to a later session.
- **Optional replay/collaboration features are not v1 blockers**. Multi-device cursors, mid-run reconnect/join, rich attachment pipelines, and fine-grained event replay may be tracked as later questions, not prerequisites for the first persistence milestone.

## Current architecture snapshot

AGUIDojo currently runs as a unified sample around a single `POST /chat` route:

- `AGUIDojoClient` is a Blazor Server BFF that streams AG-UI traffic directly to `AGUIDojoServer` `/chat`, keeps per-session UI state in Fluxor, and uses YARP for `/api/*` business endpoints.
- The client already surfaces rich session experiences: chat history, plans, approvals, recipe/shared state, document previews, charts, forms, data grids, notifications, and background-session switching.
- The live source of truth for session state is still the client/store layer. Browser persistence currently uses `localStorage` for lightweight metadata and active session selection, plus IndexedDB for conversation trees.
- `AGUIDojoServer` hosts the unified agent route plus business APIs and tool wrappers, but it does not yet expose a canonical Chat Sessions API or a server-owned primary chat-session store.
- Durable Task and workflow capabilities exist in the wider repo, but inside AGUIDojo they should be treated as runtime and execution concerns, not the product-facing chat-session system of record.

This means AGUIDojo already demonstrates many AG-UI capabilities, but the sample's foundations are still uneven: transport continuity, authoritative session ownership, durable persistence, and integration boundaries are not yet mature enough for the roadmap to stay focused only on feature demos.

## Critical chat continuity issue

This is the first implementation priority.

The unified `/chat` path currently behaves as though the backend can remain conversation-stateful after the first turn and accept only delta messages. In practice, the AG-UI flow used here expects the **full active-branch message history on every turn**. The current client pipeline tracks `ConversationId`, sets `StatefulMessageCount`, and then slices outbound messages with `Skip(session.StatefulMessageCount)` on later turns. Once a prior turn succeeds, that usually removes earlier user/assistant messages and leaves only the newest turn going back to the server.

Result: second and later turns within the same session can lose the context that the model needs to remember prior instructions, tool results, or decisions.

### Direction for the fix

- Stop delta-turn submission on the unified `/chat` path.
- Always send the full active-branch message history to `AGUIChatClient` on every turn.
- Treat `ConversationId` and AG-UI `threadId` as correlation metadata only, not as permission to omit prior messages.
- Audit approval, retry, edit-and-regenerate, and other re-entry paths so they preserve full history as well.
- If prompt-size control is needed later, handle it through explicit server-side context-window policy rather than client-side history skipping.

## Session persistence and integration gap

AGUIDojo's current browser-local persistence is useful as a convenience layer, but it is not enough for the roadmap the sample is now pointing toward.

Today the persisted model is intentionally lightweight and incomplete:

- session metadata is stored in `localStorage`
- active session identity is stored in `localStorage`
- conversation trees are stored in IndexedDB
- hydrated state does not recreate the full fidelity of approvals, audit history, plans, recipe state, document state, data grids, or other rich AI/session surfaces
- session identity is still effectively client-owned rather than server-issued

That is acceptable for a local cache, but not for:

- cross-device session resume
- server-authoritative session listing, hydration, and archive
- linking sessions to workflows or business entities
- preserving approvals, audit history, and artifact state as durable server-side data
- evolving AGUIDojo toward a more realistic application boundary

### Persistence direction for AGUIDojo

AGUIDojo should add a **server-owned Chat Sessions module** inside `AGUIDojoServer` and use it as the primary business owner of:

- session metadata and lifecycle
- canonical branching conversation state
- approvals and audit entries
- artifact snapshots and projections
- workflow and business links
- simulated ownership context

For sample scope, the system of record should be **SQLite**. The schema should remain relational and portable so it can later map cleanly to SQL Server or PostgreSQL without rethinking the domain model.

Key boundary decisions:

- the **server** issues and owns the primary business session ID
- browser storage becomes **cache and import only**
- the canonical conversation stays a **branching graph**, not a flattened transcript
- rich message payloads and artifact/governance state become first-class server data over time
- AG-UI thread and run IDs, DurableTask identifiers, and workflow IDs stay as **linked runtime references**, not the primary business key

## Sample-wide maturity / gap matrix

| Area                                     | Current shape                                                                                             | Gap / risk                                                                                                 | Roadmap stance                                                                            |
| ---------------------------------------- | --------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Conversational continuity                | Unified `/chat` route is in place, but later turns can be sliced by `StatefulMessageCount`.               | The sample loses within-session memory and misuses the transport contract.                                 | Fix first by always sending full active-branch history.                                   |
| Session ownership                        | Session lifecycle is effectively client-owned.                                                            | No server-authoritative create/list/get/archive model or server-issued primary session ID.                 | Add a server-owned Chat Sessions module before deepening realism work.                    |
| Persistence model                        | Browser persistence uses `localStorage` + IndexedDB and is lossy.                                         | No cross-device continuity, incomplete artifact/governance durability, weak auditability.                  | Use SQLite as AGUIDojo's system of record and keep browser storage secondary.             |
| Runtime correlation                      | `ConversationId`, AG-UI run and thread IDs, and future workflow IDs are available.                        | Easy to conflate runtime IDs with business identity.                                                       | Store them as correlation and runtime references only.                                    |
| Artifact and governance state            | Plans, approvals, recipe state, document previews, charts, grids, and notifications exist in the UI flow. | They are not yet durably modeled as first-class server session data.                                       | Persist these after the base session store is in place.                                   |
| Tool maturity and AG-UI feature coverage | Existing wrappers already demonstrate AG-UI concepts through tools and artifact rendering.                | A narrow "implement more tools" mindset can bypass foundational continuity and persistence work.           | Treat tool maturity as one workstream within the broader roadmap, not the roadmap itself. |
| Backend and domain realism               | Business endpoints and tool wrappers exist, but ownership and integration boundaries are still informal.  | The sample can become unrealistic if domain linking grows without durable session foundations.             | Sequence workflow and entity linking after continuity and persistence are established.    |
| Documentation and operations             | README reflects the unified sample, while some older internal notes lag.                                  | Execution can drift if docs, terminology, and operational guidance do not match the intended architecture. | Keep docs and operational guidance aligned as each phase lands.                           |

## Phased roadmap

### Phase 0 -- Restore conversational continuity

- Remove client-side delta-turn submission from the unified `/chat` flow.
- Send full active-branch history on every request.
- Audit edit/regenerate, approval, retry, and cancel/restart paths for the same guarantee.
- Add only the minimum regression validation needed to prove that within-session memory now survives multi-turn conversations.

### Phase 1 -- Introduce server-owned chat-session identity and APIs

- Add a Chat Sessions module in `AGUIDojoServer`.
- Support create, list, get, and archive semantics for chat sessions.
- Move primary session-id issuance from client-side generation to the server.
- Hydrate sessions from the server-authoritative index instead of browser-only metadata.
- Use SQLite for the sample implementation, with relational abstractions that stay portable to SQL Server and PostgreSQL later.
- Keep browser storage as cache, draft, and import support rather than authoritative identity.

### Phase 2 -- Persist the canonical branching conversation

- Persist the branching conversation graph on the server rather than only a browser-local tree.
- Preserve enough message fidelity to keep future tool, function, and multimodal extensions viable.
- Track the active leaf and current branch explicitly.
- Store AG-UI, DurableTask, and workflow identifiers only as linked runtime correlations.

### Phase 3 -- Persist approvals, audit, artifacts, and current session projections

- Persist approval requests and outcomes.
- Persist audit trail entries and key operational timestamps.
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
- Decide which optional reconnect, replay, and collaboration features remain deferred.
- Update README, issue notes, and operational guidance so the sample story stays aligned with the implemented architecture.
- Continue to defer a dedicated `AGUIDojoClient.Tests` strategy to separate planning unless scope materially changes.

## Acceptance criteria

This rewrite should be considered actionable only if future execution can proceed with the following shared understanding:

- [ ] The roadmap is explicitly sample-wide for `dotnet/samples/05-end-to-end/AGUIDojo`, not a narrow AG-UI feature checklist.
- [ ] The within-session memory and continuity bug on `/chat` is the **first** implementation priority.
- [ ] The roadmap calls for a **server-owned Chat Sessions module** as the primary business session owner.
- [ ] **SQLite** is the persistence choice for AGUIDojo sample scope, while the model stays relational and portable to SQL Server and PostgreSQL later.
- [ ] Browser storage is treated as cache and import support only, not the primary source of truth.
- [ ] AG-UI thread and run IDs, DurableTask identifiers, and workflow IDs are treated as correlation and runtime-only identifiers, not the primary business session ID.
- [ ] Any ownership or authorization behavior is framed as **simulated only** inside AGUIDojo scope.
- [ ] The roadmap includes continuity, server session APIs, canonical message persistence, approvals, audit, artifacts, workflow and entity linking, documentation and operational alignment, and backend/domain realism sequencing.
- [ ] Validation expectations stay lightweight here, and a dedicated `AGUIDojoClient.Tests` strategy remains deferred.

## Open questions

1. What is the thinnest Chat Sessions abstraction that keeps SQLite simple in the sample while remaining portable to SQL Server or PostgreSQL later?
2. How should simulated current-user and current-tenant context appear in server APIs and persisted chat-session records without turning into real auth plumbing?
3. How much of today's lossy browser-local session data should be imported into the server store once the Chat Sessions module exists?
4. For concurrent cross-device changes later, should stale writes auto-branch by default or produce an explicit conflict?
5. Which artifact surfaces must be durable in the first server-backed milestone versus later follow-up work?
6. Do we need any optional replay or resume features in early AGUIDojo milestones, or should participant cursors, mid-run reconnect, richer attachment handling, and fine-grained event journaling remain explicitly deferred?
7. What lightweight documentation and operational updates should be required at the end of each phase so the sample story never drifts from the implementation?

## Suggested execution notes

- Start with the memory bug before introducing new persistence behavior.
- Do not let tool-by-tool implementation work outrun the session foundation.
- Preserve focus on the unified `/chat` sample shape already described in the current AGUIDojo README.
- Preserve existing capability-wrapper organization unless a later phase proves a clearer domain or maturity split is worth it.
- Treat this issue as the execution roadmap for AGUIDojo sample evolution, not as a request to implement real production auth or repository-wide platform infrastructure.

## Reference points in the sample

- `README.md`
- `AGUIDojoClient/Program.cs`
- `AGUIDojoClient/Components/Pages/Chat/Chat.razor`
- `AGUIDojoClient/Services/AgentStreamingService.cs`
- `AGUIDojoClient/Services/SessionPersistenceService.cs`
- `AGUIDojoClient/Store/SessionManager/SessionHydrationEffect.cs`
- `AGUIDojoServer/Program.cs`

