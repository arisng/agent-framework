---
date: 2026-03-23
type: Task
severity: Critical
status: resolved
related:
  - 260323_aguidojo-implementation-plan.md
---

# Task: Restore /chat continuity and re-entry correctness

## Objective
Re-establish `/chat` as a stateful conversation surface by always sending the full active-branch history for every follow-up turn and every same-session re-entry path before any durability or model-routing work depends on that invariant.

## Tasks
- [x] Remove delta-turn submission from the unified `/chat` flow and stop using `StatefulMessageCount` as the basis for outbound history slicing.
- [x] Ensure active-branch assembly always includes the full branch for normal follow-up turns, approval submit/reject flows, retry/restart flows, edit-and-regenerate flows, and any other same-session re-entry path.
- [x] Treat `ConversationId`, AG-UI `threadId`, and AG-UI `runId` as correlation metadata only, never as permission to omit prior history.
- [x] Add lightweight regression coverage for at least one ordinary multi-turn flow and one re-entry flow.
- [x] Document the invariant that prompt-size management, when needed, is handled by explicit server-side policy rather than client-side history skipping.

## Acceptance Criteria
- [x] Second and later `/chat` turns preserve prior instructions, tool context, and decisions within the same active branch.
- [x] Approval, rejection, retry, and edit-and-regenerate paths continue from the same full branch context.
- [x] No client-side delta-history submission remains on the unified `/chat` path.
- [x] Any future context trimming happens only through an explicit server-owned policy.

## Resolution
- Completed in `da19721f` (`fix(aguidojo): guard chat continuity invariant`).

## References
- Parent roadmap: `260323_aguidojo-implementation-plan.md`
- See parent: [260323_aguidojo-implementation-plan.md](260323_aguidojo-implementation-plan.md)
- Architecture and defect rationale: `.docs/explanation/agui-dojo/system-design.md`
- Sample overview: `README.md`
