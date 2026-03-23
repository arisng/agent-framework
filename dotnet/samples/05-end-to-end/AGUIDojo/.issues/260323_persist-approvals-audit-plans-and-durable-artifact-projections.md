---
date: 2026-03-23
type: Task
severity: High
status: Proposed
related:
  - 260323_aguidojo-durable-chat-sessions-foundation.md
---

# Task: Persist approvals, audit, plans, and durable artifact projections

## Objective
Make server persistence useful for the full AGUIDojo experience by durably restoring approvals, audit facts, plan state, compaction checkpoints, and the session-scoped artifact projections that matter for resume and support.

## Tasks
- [ ] Persist approval requests, decisions, and key timestamps with session and turn lineage.
- [ ] Persist structured audit entries for lifecycle events, tool actions, model-routing divergences, compaction outcomes, and correlation facts.
- [ ] Define durable projections for plan state and checkpoints, recipe/shared state, document preview or diff context, data-grid projections, and material file/document references.
- [ ] Keep the root session detail contract thin by surfacing latest facts, counts, and pointers while exposing deeper inspection through dedicated projections or sub-resources.
- [ ] Add best-effort import from current browser-local data where it materially helps seed server-owned state.
- [ ] Validate that durable projections rehydrate meaningful session state instead of falling back to message text only.

## Acceptance Criteria
- [ ] Reloading a session can restore approval and audit context without relying on the original browser tab.
- [ ] Core artifact surfaces reopen with meaningful state instead of only a flattened transcript.
- [ ] Compaction checkpoints and model-switch diagnostics are inspectable without rewriting away canonical branch history.
- [ ] Session detail plus dedicated inspection reads can expose current plan, checkpoint, artifact, and file state without ad hoc database spelunking.

## References
- Parent roadmap: `260323_aguidojo-durable-chat-sessions-foundation.md`
- Planning redirect stub: `.docs/how-to/implementation-plan.md`
- Persistence rationale: `.docs/explanation/agui-dojo/server-side-persistence-for-chat-session.md`
- Architecture baseline: `.docs/explanation/agui-dojo/system-design.md`
