<!-- MY CUSTOMIZATION POINT: document the server-owned branching graph added for child3 -->

# Server-owned branching conversation graph and active-branch rehydration

## Summary

AGUIDojo now keeps the canonical branching conversation graph on the server instead of treating the browser's IndexedDB tree as the durable source of truth.

This closes the child-3 split-brain gap:

- the server persists the branch graph after `/chat` runs
- fresh browsers rehydrate from `GET /api/chat-sessions/{id}/conversation`
- branch selection can be pushed back to the server through the active-leaf API
- browser storage remains a cache and offline convenience layer, not the canonical record

## What changed

### Durable graph model

`AGUIDojoServer` now stores conversation nodes in `ChatConversationNodes` and tracks:

- `ChatSessions.RootMessageId`
- `ChatSessions.ActiveLeafMessageId`

Each node keeps:

- parent linkage
- sibling order
- runtime message id as metadata only
- role / author / text
- serialized `ContentJson`
- serialized `AdditionalPropertiesJson`
- creation timestamp

That shape lets the server reconstruct the active branch without flattening the conversation into a single transcript.

### Persistence path

The canonical graph is updated from the real `/chat` execution path, not from a client-side mirror write.

`ConversationPersistenceAgent` wraps the unified agent in `ChatClientAgentFactory` and, after streaming completes, persists:

1. the ordered input branch sent on the request
2. the assistant response messages produced by the run

`ChatSessionMiddleware` exposes the correlated server session id through `HttpContext.Items`, so the persistence layer can update the correct session without turning AG-UI thread ids into primary keys.

### Rehydration path

`SessionHydrationEffect` now prefers the server conversation API over browser IndexedDB:

1. load browser metadata / active session as before
2. load the server session catalog
3. fetch `/api/chat-sessions/{id}/conversation`
4. rebuild the `ConversationTree` from the server graph
5. fall back to IndexedDB only when the server graph is unavailable or invalid

This means cross-browser resume and fresh-browser re-entry now restore:

- the full visible branch
- alternate branches
- the current active leaf

### Active-branch selection

Persisting the graph after `/chat` is not enough by itself, because users can switch from `2/2` back to `1/2` without sending a new message.

To cover that UX, the client now calls:

- `PUT /api/chat-sessions/{id}/active-leaf`
- `DELETE /api/chat-sessions/{id}/conversation` when a conversation is cleared

The active-leaf call is only attempted when the current tree is clearly server-backed (the hydrated node ids are server-issued GUIDs). That keeps the pre-hydration local tree from trying to write local-only node ids back into the canonical store.

## Why this fixes the original bug

Before child 3:

- the server owned only the thin session catalog
- browser A owned the actual branch tree
- browser B could open the same session title but still see an empty pane
- branch choice was not durably owned by the server

After child 3:

- the server owns the canonical branch graph
- fresh browsers restore from that graph first
- active branch selection survives reload because the selected leaf is persisted server-side
- runtime correlation ids (`threadId`, `server_session_id`, runtime message ids) stay linked metadata instead of replacing the canonical session identity

## Fidelity notes

The durable node model now stores serialized `AIContent` plus serialized additional properties.

The client hydration path restores:

- `MessageId`
- additional properties
- serialized AI contents when they can be deserialized back into supported content types (`TextContent`, `DataContent`, `FunctionCallContent`, `FunctionResultContent`)

This is intentionally richer than the older browser-only `ConversationNodeDto`, which flattened messages to text plus minimal metadata.

## Runtime validation used for child 3

The live LLM endpoint in this environment was quota-limited during validation, so runtime proof focused on the server-owned rehydration path directly:

1. restart the Aspire-hosted sample so the new server persistence code owns the live SQLite database
2. seed a branched canonical session into `aguidojo-sessions.db`
3. open AGUIDojo in a fresh isolated browser
4. verify the UI restores the seeded conversation on branch `2/2`
5. switch to branch `1/2`
6. reload the page and verify it still restores `1/2`
7. open a second isolated browser and verify it also restores `1/2`

That runtime path proved the bug that motivated child 3 is closed even without relying on a successful upstream LLM response.
