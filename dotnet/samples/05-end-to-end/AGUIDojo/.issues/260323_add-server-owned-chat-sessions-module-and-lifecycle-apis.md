---
date: 2026-03-23
type: Task
severity: Critical
status: Proposed
related:
  - 260323_aguidojo-durable-chat-sessions-foundation.md
---

# Task: Add server-owned Chat Sessions module and lifecycle APIs

## Objective
Move primary session ownership into `AGUIDojoServer` by adding a SQL-first Chat Sessions module that issues the business session ID, owns lifecycle state, and exposes thin server-authoritative recovery and archival APIs.

## Tasks
- [ ] Define the initial relational Chat Sessions model for summary/detail use: business session ID, lifecycle state, created/updated timestamps, primary subject link, correlation links, and preferred-model metadata.
- [ ] Add thin lifecycle APIs for session list, session summary/detail, and archive operations while keeping heavy conversation, audit, and artifact payloads out of the root session DTO.
- [ ] Create the server-owned session implicitly on the first persisted `/chat` turn when a draft has no canonical server session yet.
- [ ] Shift session hydration and session-list reads toward the server-owned index instead of browser-local identity.
- [ ] Add a small sample-scoped model catalog endpoint so the client can later render model choices without hardcoding model facts in the browser.
- [ ] Validate the lifecycle flow with focused server/API checks and a small end-to-end smoke pass.

## Acceptance Criteria
- [ ] The first persisted prompt against a new draft yields a server-owned session ID.
- [ ] Refresh or a second browser can recover the session list without depending on prior browser metadata.
- [ ] Archive behavior is represented by server lifecycle state, not only client-side removal.
- [ ] Session summary/detail stays thin but exposes enough recovery/support metadata to inspect a session without loading the full conversation graph or artifact payloads.
- [ ] Session contracts leave room for preferred-model metadata without multiplying chat endpoints.

## References
- Parent roadmap: `260323_aguidojo-durable-chat-sessions-foundation.md`
- Planning redirect stub: `.docs/how-to/implementation-plan.md`
- Architecture baseline: `.docs/explanation/agui-dojo/system-design.md`
- Persistence rationale: `.docs/explanation/agui-dojo/server-side-persistence-for-chat-session.md`
