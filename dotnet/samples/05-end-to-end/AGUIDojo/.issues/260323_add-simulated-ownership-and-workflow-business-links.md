---
date: 2026-03-23
type: Task
severity: Medium
status: Proposed
related:
  - 260323_aguidojo-durable-chat-sessions-foundation.md
---

# Task: Add simulated ownership and workflow/business links

## Objective
Make the durable session model more realistic by adding explicit simulated ownership context and durable links to workflows and business subjects without turning the sample into a real authentication rollout.

## Tasks
- [ ] Add explicit simulated current-user and current-tenant context to server-owned session records and APIs.
- [ ] Add durable subject links starting with Todo plus linked workflow-instance and Durable Task/runtime identifiers where the sample benefits from that realism.
- [ ] Keep runtime and workflow IDs as linked references only, never as the primary chat business session identity.
- [ ] Route chat-driven business actions through module or application services on behalf of the current user instead of duplicating business ownership inside chat persistence.
- [ ] Expose lightweight query or projection support for owner, subject, and workflow links where the sample UX needs them.
- [ ] Document the sample-only constraints so this work does not drift into real auth or production identity design.

## Acceptance Criteria
- [ ] Sessions can be associated with a seeded or fake owner and tenant context.
- [ ] Todo and workflow links survive reload and can be surfaced where the sample needs them.
- [ ] Runtime and workflow IDs remain correlation links, not business IDs.
- [ ] No real auth flow, token issuance, tenant onboarding, or production identity plumbing is introduced.

## References
- Parent roadmap: `260323_aguidojo-durable-chat-sessions-foundation.md`
- Planning redirect stub: `.docs/how-to/implementation-plan.md`
- Architecture baseline: `.docs/explanation/agui-dojo/system-design.md`
- Persistence rationale: `.docs/explanation/agui-dojo/server-side-persistence-for-chat-session.md`
