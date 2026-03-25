<!-- MY CUSTOMIZATION POINT: capture the server-owned chat session catalog behavior after child 2 -->

# Server-owned chat session catalog and hydration

## Executive summary

AGUIDojo now has a meaningful **server-owned session stack**, not only a thin catalog:

- the **server** owns session identity, list/detail/archive lifecycle, the canonical branching conversation graph, and the durable workspace projection
- the **client** still owns live rendering state and browser cache/draft support, but it rehydrates from the server-owned surfaces first

That makes second-browser and post-refresh recovery useful without depending on the original browser tab.

## What the server owns now

`AGUIDojoServer` already keeps a relational `ChatSession` row per AG-UI thread and exposes:

- `GET /api/chat-sessions`
- `GET /api/chat-sessions/{id}`
- `GET /api/chat-sessions/{id}/conversation`
- `GET /api/chat-sessions/{id}/workspace`
- `POST /api/chat-sessions/{id}/archive`

`ChatSessionMiddleware` runs in front of `POST /chat`, extracts the AG-UI `threadId`, ensures a server session row exists, and returns `X-Session-Id`.

The important refinements from this pass are:

- `ChatSessionMiddleware` still ensures a durable session row and lets `ChatSessionService` backfill a thin session title from the first user message whenever the row is still untitled
- `ChatConversationService` durably upserts the canonical root-to-leaf branch graph on the server
- `ChatSessionWorkspaceService` derives and stores approvals, audit facts, and artifact/workspace snapshots for the active branch
- hydration uses those server-owned reads first, while browser storage only fills cache/import gaps

That means a server-only browser can now recover a session list with meaningful labels instead of a stack of anonymous rows.

## Why title derivation belongs on the server

AGUIDojo already had client-side title generation, but client-only titles were not enough for the recovery scenarios child 2 cares about:

- a second browser or fresh device needs useful list labels before any client cache exists
- archive/list/detail lifecycle APIs should be useful without depending on prior local hydration
- server-owned session identity is more trustworthy when the catalog is understandable on its own

Because AG-UI requests already carry the full active branch on every turn, the server can safely derive the title from the earliest user message present in the request without inventing a separate title-generation round trip.

## Ownership boundary after this change

The system boundary is now:

- **server-authoritative catalog and detail**
  - session id
  - lifecycle status
  - created/last-activity timestamps
  - archive state
  - AG-UI thread correlation
  - thin list/detail title
  - canonical branching conversation graph
  - approvals, audit, plan, document, data-grid, and file-reference projections
- **client/runtime-authoritative rendering**
  - transient in-flight streaming state
  - session-scoped toast/notification state
  - browser cache/import convenience

That split keeps the browser helpful for UX while making the server the durable inspection and recovery boundary.

## Practical implication for later roadmap items

Later work can build on this server-owned base instead of replacing it:

- model routing can persist requested/preferred/effective model metadata on the same session aggregate
- ownership, subject, and workflow links can stay on the same session detail surface without changing the basic list/detail/archive contract
- browser cache can keep shrinking toward draft/offline convenience because the durable recovery/inspection path is already server-owned

In other words, the catalog spine and the richer detail/workspace side now exist together, and the browser layer is no longer carrying the durable recovery story by itself.
