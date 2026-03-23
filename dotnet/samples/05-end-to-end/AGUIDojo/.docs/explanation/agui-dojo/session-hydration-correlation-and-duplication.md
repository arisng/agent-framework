# AGUIDojo session hydration correlation and duplication

## Defect pattern

AGUIDojo originally treated the browser-local session ID and the server-owned chat-session ID as if they were the same identity space.

That works only while a session stays fully local or fully server-owned. It breaks once the client:

1. creates a local session row in browser storage,
2. sends a first `/chat` request,
3. lets the server create its own chat-session row, and
4. reloads before the client has any durable correlation metadata.

On the next hydration pass, the client loaded:

- the browser session keyed by the local client session ID, and
- the server session keyed by the server session ID.

Because the merge logic only compared raw session IDs, AGUIDojo could render both rows in the sidebar even though they represented the same conversation.

## Root cause

The real continuity key for AG-UI `/chat` is the AG-UI `threadId`, not the server chat-session row ID.

Before the fix:

- the client generated a local session ID for UI state,
- the server generated a different chat-session ID for persistence,
- the server already stored `AguiThreadId` in `ChatSession`,
- but the client only kept the AG-UI thread ID in `SessionStreamingContext`,
- and that thread ID was ephemeral only.

After refresh, the browser lost the AG-UI thread correlation, so hydration had no stable cross-boundary key for deciding that a local session and a server session were the same conversation.

## Durable correlation model introduced

The sample now uses a two-layer correlation model:

1. **Client session ID**
   - remains the local UI/storage key.
   - preserves browser conversation trees and active-session selection.

2. **Durable AG-UI thread ID**
   - is generated on the client when a session is created.
   - is persisted in browser metadata.
   - is restored during hydration.
   - is reapplied to the streaming context before `/chat` requests.
   - survives a failed first send before the first SSE update arrives.

3. **Correlated server session ID**
   - is stored separately from the client session ID.
   - is discovered from the server session list by matching AG-UI thread ID.
   - is persisted once known.
   - is used for server-side archive synchronization.

## Hydration and merge rules

During hydration, AGUIDojo now:

1. restores local sessions from browser storage,
2. restores or backfills durable AG-UI thread IDs,
3. loads server session summaries including `aguiThreadId`,
4. merges sessions by correlation in this order:
   - raw server session ID,
   - persisted correlated server session ID,
   - durable AG-UI thread ID,
5. keeps the local session entry when a correlated match is found, and
6. adds server-only sessions only when no correlation exists.

This prevents duplicate sidebar rows while preserving the browser-owned conversation tree and local active session identity.

## Archive synchronization behavior

Archiving a session now prefers the correlated server session ID. When that ID has not been persisted yet, the client can still resolve it from the server session list by AG-UI thread ID before issuing the archive request.

That keeps the browser session ID and the server session ID decoupled while still allowing reliable server cleanup.
