---
date: 2026-03-23
type: Task
severity: Medium
status: Resolved
related:
  - 260323_aguidojo-implementation-plan.md
---

# Task: Add simulated ownership and workflow/business links

## Objective
Make the durable session model more realistic by adding explicit simulated ownership context and durable links to workflows and business subjects without turning the sample into a real authentication rollout.

## Scope note
- The currently implemented slice is the honest routing seam only: server-owned chat sessions can expose simulated owner/tenant/subject/workflow context to downstream request-scoped services.
- This issue is **not** complete just because that seam exists. AGUIDojo still does not ship a real Todo module, production auth rollout, or business-state ownership inside chat persistence.
- Future work should route business actions through subject/application services that own their own records, using the chat-session seam only as caller and correlation context.

## Tasks
- [x] Add explicit simulated current-user and current-tenant context to server-owned session records and APIs.
- [x] Add durable subject links suitable for Todo-style module routing plus linked workflow-instance and Durable Task/runtime identifiers where the sample benefits from that realism.
- [x] Keep runtime and workflow IDs as linked references only, never as the primary chat business session identity.
- [x] Route chat-driven business actions through module or application services on behalf of the current user instead of duplicating business ownership inside chat persistence.
- [x] Expose lightweight query or projection support for owner, subject, and workflow links where the sample UX needs them.
- [x] Document the sample-only constraints so this work does not drift into real auth or production identity design.

## Acceptance Criteria
- [x] Sessions can be associated with a seeded or fake owner and tenant context.
- [x] Subject and workflow links survive reload and can be surfaced where the sample needs them.
- [x] Runtime and workflow IDs remain correlation links, not business IDs.
- [x] No real auth flow, token issuance, tenant onboarding, or production identity plumbing is introduced.

## References
- Parent roadmap: `260323_aguidojo-implementation-plan.md`
- See parent: [260323_aguidojo-implementation-plan.md](260323_aguidojo-implementation-plan.md)
- Architecture baseline: `.docs/explanation/agui-dojo/system-design.md`
- Persistence rationale: `.docs/explanation/agui-dojo/server-side-persistence-for-chat-session.md`
- Seam note: `.docs/explanation/agui-dojo/simulated-ownership-workflow-and-subject-links.md`

## Completion notes

- The implemented slice keeps ownership and subject/workflow context as thin routing/correlation facts on the server-owned chat session.
- Real-user validation now passes the required three-turn runtime scenario without the earlier persistence failures, so the ownership/workflow seam no longer blocks the end-to-end acceptance path.
