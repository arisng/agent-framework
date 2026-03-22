# AGUIDojo docs

<!-- MY CUSTOMIZATION POINT: keep the active AGUIDojo research index aligned with current session research notes -->

This folder now keeps only the current, consolidated documentation for
`dotnet/samples/05-end-to-end/AGUIDojo`.

## Read this first

- `system-design.md` - current architecture, boundaries, current gaps, and target session design
- `implementation-plan.md` - active execution plan and rollout sequence
- `research/aguidojo-llm-picker-architecture-and-maf-alignment.md` - model picker, routing, and compaction research
- `research/server-side-persistence-for-chat-session.md` - persistence boundary and storage-direction research
- `research/copilot-cli-session-context-and-instruction-patterns.md` - public-docs overlap analysis for sessions, model UX, compaction, and instruction safety
- `research/copilot-cli-public-repo-grounding.md` - public-repo grounding pass for durable sessions, model routing, compaction, instruction visibility, and trust boundaries
- `research/copilot-cli-session-state-schema.md` - observed Copilot CLI session artifacts and schema reference for inspectable session surfaces
- `research/copilot-cli-session-topology.md` - catalog/index plus per-session workspace topology note grounded in public docs and observed artifacts

The Copilot CLI schema/topology notes are part of the active set because they ground inspectable session
surfaces and the catalog-versus-workspace split. They are reference material for AGUIDojo's server-owned
session design, not a directive to reproduce Copilot CLI's literal `~/.copilot/` filesystem layout.

## Historical/background context

- `research/aguidojo-harnessing-plan.md` - earlier sample-wide hardening/roadmap framing kept for
  background context only; the active execution source is `implementation-plan.md` plus the research set
  above

## Documentation stance

Older v1/v2/v3 plan generations and supporting research notes have been superseded by the consolidated
docs above. The goal is to keep one current story for AGUIDojo instead of maintaining multiple
historical plan stacks in parallel.

The former long-form roadmap issue has been consolidated into `implementation-plan.md` and the
supporting research notes above.

When the sample changes materially, update these docs so they stay aligned with the README,
system design, implementation plan, and any research notes whose assumptions changed.
