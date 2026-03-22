# AGUIDojo docs

This folder now keeps only the current, consolidated documentation for
`dotnet/samples/05-end-to-end/AGUIDojo`.

## Read this first

- `system-design.md` - current architecture, boundaries, current gaps, and target session design
- `implementation-plan.md` - active execution plan and rollout sequence
- `research/aguidojo-llm-picker-architecture-and-maf-alignment.md` - model picker, routing, and compaction research
- `research/server-side-persistence-for-chat-session.md` - persistence boundary and storage-direction research
- `research/copilot-cli-session-context-and-instruction-patterns.md` - Copilot CLI overlap analysis for sessions, model UX, compaction, and instruction safety

## Documentation stance

Older v1/v2/v3 plan generations and supporting research notes have been superseded by the consolidated
docs above. The goal is to keep one current story for AGUIDojo instead of maintaining multiple
historical plan stacks in parallel.

The former long-form roadmap issue has been consolidated into `implementation-plan.md` and the
supporting research notes above.

When the sample changes materially, update these docs so they stay aligned with the README,
system design, implementation plan, and any research notes whose assumptions changed.
