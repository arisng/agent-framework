---
date: 2026-03-23
type: Task
severity: Critical
status: resolved
related:
  - 260323_aguidojo-implementation-plan.md
---

# Task: Add server-owned Chat Sessions module and lifecycle APIs

## Objective
Move primary session ownership into `AGUIDojoServer` by adding a SQL-first Chat Sessions module that issues the business session ID, owns lifecycle state, and exposes thin server-authoritative recovery and archival APIs.

## Tasks
- [x] Define the initial relational Chat Sessions model for summary/detail use: business session ID, lifecycle state, created/updated timestamps, primary subject link, correlation links, and preferred-model metadata.
- [x] Add thin lifecycle APIs for session list, session summary/detail, and archive operations while keeping heavy conversation, audit, and artifact payloads out of the root session DTO.
- [x] Create the server-owned session implicitly on the first persisted `/chat` turn when a draft has no canonical server session yet.
- [x] Shift session hydration and session-list reads toward the server-owned index instead of browser-local identity.
- [x] Add a small sample-scoped model catalog endpoint so the client can later render model choices without hardcoding model facts in the browser.
- [x] Validate the lifecycle flow with focused server/API checks and a small end-to-end smoke pass.

## Acceptance Criteria
- [x] The first persisted prompt against a new draft yields a server-owned session ID.
- [x] Refresh or a second browser can recover the session list without depending on prior browser metadata.
- [x] Archive behavior is represented by server lifecycle state, not only client-side removal.
- [x] Session summary/detail stays thin but exposes enough recovery/support metadata to inspect a session without loading the full conversation graph or artifact payloads.
- [x] Session contracts leave room for preferred-model metadata without multiplying chat endpoints.

## Resolution
- Completed in `cf92a61f` (`fix(aguidojo): complete chat session lifecycle flow`).
- Validated in `fe8d7b22` (`test(aguidojo-tests): cover lifecycle hydration regressions`).
- Documented in `7e654aef` (`docs(aguidojo): document server-owned session flow`).

## References
- Parent roadmap: `260323_aguidojo-implementation-plan.md`
- See parent: [260323_aguidojo-implementation-plan.md](260323_aguidojo-implementation-plan.md)
- Architecture baseline: `.docs/explanation/agui-dojo/system-design.md`
- Persistence rationale: `.docs/explanation/agui-dojo/server-side-persistence-for-chat-session.md`
