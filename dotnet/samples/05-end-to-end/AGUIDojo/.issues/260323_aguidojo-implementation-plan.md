---
date: 2026-03-23
type: Feature Plan
severity: Critical
status: Accepted
related:
  - 260323_restore-chat-continuity-and-re-entry-correctness.md
  - 260323_add-server-owned-chat-sessions-module-and-lifecycle-apis.md
  - 260323_persist-canonical-branching-conversation-and-active-branch-rehydration.md
  - 260323_add-per-session-model-selection-server-routing-and-model-aware-compaction.md
  - 260323_persist-approvals-audit-plans-and-durable-artifact-projections.md
  - 260323_add-simulated-ownership-and-workflow-business-links.md
  - 260323_demote-browser-storage-and-align-portability-inspection-and-docs.md
---

# AGUIDojo implementation plan

## Goal
Establish the canonical high-level roadmap and implementation plan for AGUIDojo durable chat sessions and related features, consolidating foundational goals, requirements, and the ordered child work items.

## Requirements
- [x] Restore `/chat` full-history continuity and correct all re-entry flows before durability work builds on top.
- [x] Add a server-owned Chat Sessions module with implicit first-turn creation plus thin list/detail/archive lifecycle APIs.
- [x] Persist the canonical branching conversation graph and active-branch state with rich enough message fidelity for current AGUIDojo surfaces.
- [x] Support per-session preferred model selection on the single `/chat` route, server-side effective-model routing, and model-aware compaction/checkpoint policy.
- [x] Persist approvals, audit, plan state, compaction checkpoints, and durable artifact/projection records needed for resume and support.
- [ ] Add simulated owner/tenant context plus durable workflow and business-subject links, starting with Todo.
- [ ] Demote browser storage to cache, drafts, offline convenience, and best-effort import while keeping portability, inspection, and docs aligned.

## Proposed Implementation
- AGUIDojo scope only: this roadmap applies to `dotnet/samples/05-end-to-end/AGUIDojo`.
- Keep auth and identity simulated. Do not expand the sample into a real auth, tenant onboarding, or production identity rollout.
- Keep a single `POST /chat` route. Model choice travels as request/session metadata, not endpoint topology.
- Make the server authoritative for session identity, lifecycle, model routing, context budgeting, compaction, and durable recovery/support facts.
- Use a SQL-first, relational-first Chat Sessions module that stays portable across SQLite for local runs and SQL Server/PostgreSQL for modular-monolith deployments.
- Preserve canonical branching conversation state instead of flattening sessions into a single transcript.
- Treat `ConversationId`, AG-UI `threadId`/`runId`, Durable Task IDs, and workflow IDs as correlation only, not as business session identity.
- Keep session summary/detail thin and list-friendly, then expose full audit, checkpoint, artifact, and conversation inspection through dedicated projections or sub-resources.
- Use MAF seams where they already exist—AG-UI forwarded properties, `ChatClientFactory`, `AgentSessionStore`/`ChatHistoryProvider` integration points, and `CompactionProvider`/pipeline strategies—rather than inventing bespoke transport or storage shapes.
- Record both `preferredModelId` and `effectiveModelId`, plus divergence reasons, whenever routing chooses a different effective model.
- Treat browser storage as cache, draft support, offline convenience, and best-effort import only once server-owned sessions exist.
- Use Copilot CLI research as topology and inspection inspiration only; do not copy `~/.copilot/session-state` or a per-session filesystem layout as AGUIDojo's storage contract.

## Ordered Child Work Items
- [Restore /chat continuity and re-entry correctness](./260323_restore-chat-continuity-and-re-entry-correctness.md)
- [Add server-owned Chat Sessions module and lifecycle APIs](./260323_add-server-owned-chat-sessions-module-and-lifecycle-apis.md)
- [Persist canonical branching conversation and active-branch rehydration](./260323_persist-canonical-branching-conversation-and-active-branch-rehydration.md)
- [Add per-session model selection, server routing, and model-aware compaction](./260323_add-per-session-model-selection-server-routing-and-model-aware-compaction.md)
- [Persist approvals, audit, plans, and durable artifact projections](./260323_persist-approvals-audit-plans-and-durable-artifact-projections.md)
- [Add simulated ownership and workflow/business links](./260323_add-simulated-ownership-and-workflow-business-links.md)
- [Demote browser storage and align portability, inspection, and docs](./260323_demote-browser-storage-and-align-portability-inspection-and-docs.md)

## Risks & Considerations
- Payload sizes grow when the client always sends the full active branch; do not reintroduce silent client-side truncation.
- The Chat Sessions module must stay sample-scoped and modular-monolith friendly rather than turning into premature repo-wide platformization.
- Rich message, audit, and artifact persistence can sprawl quickly; persist the current sample's real needs first.
- Model switching remains unsafe unless compaction stays server-owned and explainable through durable checkpoints and audit facts.
- Documentation drift will undercut the implementation if README, system design, and this roadmap diverge.

## References
- Architecture baseline: `.docs/explanation/agui-dojo/system-design.md`
- Persistence rationale: `.docs/explanation/agui-dojo/server-side-persistence-for-chat-session.md`
- Model picker and compaction rationale: `.docs/explanation/agui-dojo/aguidojo-llm-picker-architecture-and-maf-alignment.md`
- Product-pattern grounding only: `.docs/explanation/copilot/copilot-cli-session-context-and-instruction-patterns.md`
- Product-pattern grounding only: `.docs/explanation/copilot/copilot-cli-public-repo-grounding.md`
