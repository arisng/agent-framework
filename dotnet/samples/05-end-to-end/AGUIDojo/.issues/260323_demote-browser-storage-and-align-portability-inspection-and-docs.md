---
date: 2026-03-23
type: Task
severity: High
status: Resolved
related:
  - 260323_aguidojo-implementation-plan.md
---

# Task: Demote browser storage and align portability, inspection, and docs

## Objective
Finish the transition to a server-owned session foundation by making browser storage secondary, clarifying the portability and inspection model, and keeping the operational and documentation story aligned with the implementation.

## Tasks
- [x] Demote `localStorage` and IndexedDB to cache, draft support, offline convenience, and best-effort import or recovery only.
- [x] Document the SQL-first relational portability story, including SQLite as the local option and SQL Server/PostgreSQL as the natural modular-monolith targets.
- [x] Document the model catalog and context-window policy at the same level as the persistence portability story.
- [x] Document the server inspection surfaces used for local debugging: summary/detail, audit timelines, checkpoints, and artifact or file read models.
- [x] Update README, system design, the implementation plan, and any changed research notes so they continue to describe the same architecture and phase sequencing.
- [x] Reconfirm that replay, collaboration, and similar optional features remain deferred until the durable-session foundation is complete.

## Acceptance Criteria
- [x] The sample can be understood and used without depending on pre-existing browser-local state.
- [x] Docs consistently describe unified `/chat`, server-owned sessions, browser-cache demotion, and server-based inspection surfaces.
- [x] Portability and inspection guidance exists without copying Copilot CLI's filesystem layout.
- [x] Browser-local import remains a best-effort convenience, not a lossless contract.

## References
- Parent roadmap: `260323_aguidojo-implementation-plan.md`
- See parent: [260323_aguidojo-implementation-plan.md](260323_aguidojo-implementation-plan.md)
- Sample overview: `README.md`
- Architecture baseline: `.docs/explanation/agui-dojo/system-design.md`
- Persistence rationale: `.docs/explanation/agui-dojo/server-side-persistence-for-chat-session.md`

## Completion notes

- Browser persistence is now documented and commented as a cache/import layer rather than a durable source of truth.
- The README and AGUIDojo wiki now point readers toward the server-owned inspection surfaces and the SQL-first portability story.
