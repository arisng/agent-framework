---
date: 2026-03-23
type: Task
severity: High
status: Proposed
related:
  - 260323_aguidojo-implementation-plan.md
---

# Task: Add per-session model selection, server routing, and model-aware compaction

## Objective
Support per-session model preference on the single `/chat` route while keeping the server responsible for effective-model routing, safe context budgeting, and durable compaction diagnostics.

## Tasks
- [ ] Add a small application-owned model catalog/registry with stable model IDs, display metadata, and context-window data, and expose a thin server contract for the client picker.
- [ ] Carry the preferred model through AG-UI forwarded/request metadata on the existing `/chat` route instead of adding per-model endpoints.
- [ ] Route requests server-side with `ChatOptions.ModelId` plus a model-keyed `ChatClientFactory` or equivalent cache so different sessions can use different effective clients.
- [ ] Persist `preferredModelId`, `effectiveModelId`, and the reason whenever effective routing diverges from the preferred selection.
- [ ] Replace the fixed message-cap approach with server-side model-aware compaction using MAF compaction seams and safe input-budget thresholds that preserve enough reserve for tool output and approvals.
- [ ] Persist compaction checkpoints and model-routing facts as inspectable support/debug records.

## Acceptance Criteria
- [ ] Different sessions can request different models through the same `/chat` endpoint.
- [ ] Effective-model fallback or auto-routing is visible in persisted audit or turn data.
- [ ] Switching to a smaller-context model compacts or fails explicitly under server policy instead of relying on client-side history slicing.
- [ ] Compaction decisions leave durable checkpoint and audit breadcrumbs that can explain what happened during support/debug flows.

## References
- Parent roadmap: `260323_aguidojo-implementation-plan.md`
- See parent: [260323_aguidojo-implementation-plan.md](260323_aguidojo-implementation-plan.md)
- Model-picker architecture: `.docs/explanation/agui-dojo/aguidojo-llm-picker-architecture-and-maf-alignment.md`
- Architecture baseline: `.docs/explanation/agui-dojo/system-design.md`
