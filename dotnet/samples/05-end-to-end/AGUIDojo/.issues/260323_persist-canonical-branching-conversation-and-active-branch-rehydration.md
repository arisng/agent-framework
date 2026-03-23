---
date: 2026-03-23
type: Task
severity: Critical
status: Proposed
related:
  - 260323_aguidojo-implementation-plan.md
---

# Task: Persist canonical branching conversation and active-branch rehydration

## Objective
Make the server-owned branching conversation graph the canonical durable record so AGUIDojo can restore the correct active branch, preserve alternate branches, and rehydrate current artifact-driving message state after reload.

## Tasks
- [ ] Design and persist the branching message-node model with explicit parent/child relationships, branch lineage, and active-leaf metadata.
- [ ] Preserve rich message fidelity for assistant/user text, tool call and result linkage, structured `AIContent`, approval-related context, and the payloads current AGUIDojo artifacts depend on.
- [ ] Rehydrate the client from server-owned branch state first and treat browser conversation trees as cache or best-effort import only.
- [ ] Persist `preferredModelId` on the session and `effectiveModelId` plus divergence-reason facts on turns or audit records once model switching is active.
- [ ] Define stale-parent write behavior so concurrent cross-device submissions auto-branch instead of silently overwriting prior leaves.

## Acceptance Criteria
- [ ] Edit-and-regenerate creates a durable alternate branch that survives refresh.
- [ ] Reloading a session restores the correct active branch and current leaf state.
- [ ] Persisted messages keep enough fidelity for current tool and artifact rehydration without collapsing to plain text only.
- [ ] Runtime correlation IDs remain linked references, not the primary session identity.

## References
- Parent roadmap: `260323_aguidojo-implementation-plan.md`
- See parent: [260323_aguidojo-implementation-plan.md](260323_aguidojo-implementation-plan.md)
- Architecture baseline: `.docs/explanation/agui-dojo/system-design.md`
- Persistence rationale: `.docs/explanation/agui-dojo/server-side-persistence-for-chat-session.md`
