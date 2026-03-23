<!-- MY CUSTOMIZATION POINT: capture the server-owned chat session catalog behavior after child 2 -->

# Server-owned chat session catalog and hydration

## Executive summary

AGUIDojo now has a meaningful **server-owned chat session catalog** even though the richer conversation tree and artifact workspace still live primarily in the client/runtime layer.

That distinction matters:

- the **server** now owns session identity, list/detail/archive lifecycle, and a resumable session index keyed by `AguiThreadId`
- the **client** still owns the active conversation tree, artifact projections, and local cache/hydration of richer session detail

This is the right intermediate boundary for AGUIDojo's roadmap because it makes second-browser and post-refresh recovery useful before the full durable branching/session-detail model lands.

## What the server owns now

`AGUIDojoServer` already keeps a relational `ChatSession` row per AG-UI thread and exposes:

- `GET /api/chat-sessions`
- `GET /api/chat-sessions/{id}`
- `POST /api/chat-sessions/{id}/archive`

`ChatSessionMiddleware` runs in front of `POST /chat`, extracts the AG-UI `threadId`, ensures a server session row exists, and returns `X-Session-Id`.

The important refinement from this pass is that the middleware also inspects the full AG-UI message list and lets `ChatSessionService` backfill a thin session title from the **first user message** whenever the row is still untitled.

That means a server-only browser can now recover a session list with meaningful labels instead of a stack of anonymous rows.

## Why title derivation belongs on the server

AGUIDojo already had client-side title generation, but client-only titles were not enough for the recovery scenarios child 2 cares about:

- a second browser or fresh device needs useful list labels before any client cache exists
- archive/list/detail lifecycle APIs should be useful without depending on prior local hydration
- server-owned session identity is more trustworthy when the catalog is understandable on its own

Because AG-UI requests already carry the full active branch on every turn, the server can safely derive the title from the earliest user message present in the request without inventing a separate title-generation round trip.

## Ownership boundary after this change

The system boundary is now:

- **server-authoritative catalog**
  - session id
  - lifecycle status
  - created/last-activity timestamps
  - archive state
  - AG-UI thread correlation
  - thin list/detail title
- **client/runtime-authoritative workspace**
  - branching conversation tree
  - approval state
  - artifacts and canvas projections
  - transient streaming state
  - browser cache/import convenience

That split keeps the child-2 implementation small and behavior-safe while still moving the product toward server-first recovery.

## Practical implication for later roadmap items

Future child items can build on this server catalog instead of replacing it:

- branching persistence can attach canonical message/node storage to an existing server session id
- model routing can persist requested/preferred model metadata on the same session aggregate
- durable approvals, artifacts, and workflow links can extend the richer session-detail surface without changing the basic list/detail/archive contract

In other words, child 2 establishes the session **catalog spine** first, then later phases can deepen the detail/workspace side behind it.
