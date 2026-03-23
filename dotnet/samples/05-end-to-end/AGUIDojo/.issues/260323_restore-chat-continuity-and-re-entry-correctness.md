---
date: 2026-03-23
type: Task
severity: Critical
status: Proposed
related:
  - 260323_aguidojo-durable-chat-sessions-foundation.md
---

# Task: Restore /chat continuity and re-entry correctness

## Objective
Re-establish `/chat` as a stateful conversation surface by always sending the full active-branch history for every follow-up turn and every same-session re-entry path before any durability or model-routing work depends on that invariant.

## Tasks
- [ ] Remove delta-turn submission from the unified `/chat` flow and stop using `StatefulMessageCount` as the basis for outbound history slicing.
- [ ] Ensure active-branch assembly always includes the full branch for normal follow-up turns, approval submit/reject flows, retry/restart flows, edit-and-regenerate flows, and any other same-session re-entry path.
- [ ] Treat `ConversationId`, AG-UI `threadId`, and AG-UI `runId` as correlation metadata only, never as permission to omit prior history.
- [ ] Add lightweight regression coverage for at least one ordinary multi-turn flow and one re-entry flow.
- [ ] Document the invariant that prompt-size management, when needed, is handled by explicit server-side policy rather than client-side history skipping.

## Acceptance Criteria
- [ ] Second and later `/chat` turns preserve prior instructions, tool context, and decisions within the same active branch.
- [ ] Approval, rejection, retry, and edit-and-regenerate paths continue from the same full branch context.
- [ ] No client-side delta-history submission remains on the unified `/chat` path.
- [ ] Any future context trimming happens only through an explicit server-owned policy.

## References
- Parent roadmap: `260323_aguidojo-durable-chat-sessions-foundation.md`
- Planning redirect stub: `.docs/how-to/implementation-plan.md`
- Architecture and defect rationale: `.docs/explanation/agui-dojo/system-design.md`
- Sample overview: `README.md`
