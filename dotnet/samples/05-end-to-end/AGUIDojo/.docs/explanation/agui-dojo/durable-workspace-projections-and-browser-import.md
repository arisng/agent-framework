# Durable workspace projections and browser import

AGUIDojo now restores more than the conversation transcript. The server persists a durable workspace projection per chat session so reload, support inspection, and resume flows can reconstruct the artifacts that matter to the current branch.

## What is persisted

The server stores three durable surfaces alongside the canonical branching conversation graph:

- approval records, including request and resolution timestamps plus branch-lineage pointers
- audit events for approval outcomes, model routing, compaction checkpoints, tool facts, and browser imports
- a workspace snapshot containing the current plan, recipe state, document state, data-grid state, and file references

The thin session list/detail APIs stay summary-oriented. Rich inspection lives under the dedicated workspace resource at `GET /api/chat-sessions/{id}/workspace`.

In practice, the main inspection surfaces are now:

- `GET /api/chat-sessions` for the session catalog
- `GET /api/chat-sessions/{id}` for thin detail and correlation metadata
- `GET /api/chat-sessions/{id}/conversation` for the canonical branch graph
- `GET /api/chat-sessions/{id}/workspace` for approvals, audit timelines, compaction checkpoints, artifact state, and file references

## How projections are produced

`ChatSessionWorkspaceService` derives durable state from the active branch of the canonical conversation graph. It parses structured AG-UI payloads instead of flattening everything into transcript text:

- plan snapshots and plan patches rebuild the current plan
- recipe and document payloads rebuild shared artifact state
- `show_data_grid` results rebuild the current data-grid projection
- `request_approval` calls and approval results rebuild approval state
- model-routing and compaction facts remain inspectable through audit events

This keeps the conversation graph canonical while exposing support-friendly projections for the current branch.

## Browser import is additive, not authoritative

Browser storage is still useful for draft-like client state, especially during the transition away from browser-owned persistence. AGUIDojo therefore accepts best-effort import at `POST /api/chat-sessions/{id}/workspace/import`.

Import is intentionally merge-oriented:

- browser-only fields can seed or refresh durable state
- existing derived server state is preserved when the import omits a field
- imported approvals and approval-resolution audit entries reconcile onto the same durable approval record

This matters because browser import should not erase derived artifacts such as the current plan or data grid when the browser only sends recipe/document state.

The runtime-validation pass for the ownership/workflow child item also hardened this import path against optimistic-concurrency races. `ChatSessionWorkspaceService` now retries from a fresh EF view when the browser import collides with concurrent server-side derived-state refreshes, so import stays best-effort instead of surfacing transient snapshot-update failures in the chat UI.

## Client rehydration behavior

During hydration, the client now loads both the canonical conversation graph and the workspace projection. The workspace projection reopens the meaningful UI surfaces directly:

- plan view
- recipe editor
- document preview and diff context
- data grid
- audit trail
- pending approval state when a durable pending approval still exists

The result is that re-entry does not depend on the original browser tab reconstructing everything from local storage.

## Runtime validation evidence

The required three-turn real-user scenario was replayed against the live sample after the persistence fixes:

1. `Which tools are you equipped?`
2. `Let's compose a plan of multi integrated step that can showcase all these tools`
3. `Let's implement the plan`

In the final validated run, the assistant thinking UI remained error-free, the inline `show_form` artifact rendered, and the execution plan visibly advanced to `2 / 7 completed` in the UI while background workspace-import calls returned `204` instead of concurrency failures.
