<!-- MY CUSTOMIZATION POINT: document the implemented server-owned session lifecycle and canonical id propagation flow -->

# Server-owned chat session lifecycle and canonical ID flow

This note captures what AGUIDojo now implements for the child work item that introduces the first server-owned Chat Sessions module.

## What the server owns now

`AGUIDojoServer` now owns a thin relational `ChatSessions` catalog in SQLite for:

- canonical business session ID
- lifecycle status (`Active`, `Archived`)
- created / last-activity / archived timestamps
- thin list title derived from the first user turn
- simulated ownership context (`OwnerId`, `TenantId`)
- primary subject link fields (`SubjectModule`, `SubjectEntityType`, `SubjectEntityId`)
- workflow/runtime correlation links (`WorkflowInstanceId`, `RuntimeInstanceId`)
- AG-UI correlation (`AguiThreadId`)
- preferred-model metadata placeholder (`PreferredModelId`)
- a small server protocol marker (`ServerProtocolVersion`)

This is intentionally still a **catalog/index** layer, not the full durable conversation graph. Child work item 3 remains the place where canonical branching conversation persistence is introduced.

## First-turn creation flow

When the browser sends the first persisted `/chat` turn for a draft:

1. the client includes a durable AG-UI `threadId`
2. `ChatSessionMiddleware` buffers the request body
3. the middleware extracts:
   - `threadId`
   - the first user message (for a thin title)
   - any forwarded metadata already present for owner / tenant / subject / workflow / preferred model
4. `ChatSessionService` creates or updates the server-owned session row
5. the middleware stores a request-scoped `ChatSessionRoutingContext` in `HttpContext.Items` so downstream tools/services can resolve the active owner / tenant / subject / workflow seam from the canonical chat session
6. the server returns the canonical session ID in `X-Session-Id`
7. the AG-UI client surfaces that value as `server_session_id` on the first streamed update
8. `AgentStreamingService` stores that canonical ID in the local session metadata

The important boundary is that the browser can still keep a local UI/session key, but it no longer has to guess the durable server identity after the first persisted turn.

## Hydration and list recovery

Hydration now leans on the server-owned session index for recovery:

- browser metadata remains the local cache / draft state
- `GET /api/chat-sessions` is the authoritative active-session catalog
- the client merges local sessions with server sessions by:
  1. raw server session ID
  2. persisted correlated server session ID
  3. durable AG-UI thread ID

That keeps local rendering state intact while making the sidebar list and recovery path increasingly server-authoritative.

## Archive behavior

Archive is represented by server lifecycle state rather than only browser deletion:

- the client calls `POST /api/chat-sessions/{id}/archive`
- the server marks the row archived and timestamps `ArchivedAt`
- archived rows disappear from the active list API
- detail reads can still inspect the archived row

If a new persisted turn later arrives for the same AG-UI thread, the thin lifecycle row is promoted back to `Active`. That is a pragmatic sample behavior for a thread-correlated session catalog until richer explicit reopen rules exist.

## Model catalog seam

AGUIDojo also exposes a small sample-scoped model catalog endpoint:

- `GET /api/models`

That endpoint exists so later per-session model selection work can render supported model choices without hardcoding model facts only in browser code.

## What is still intentionally deferred

This child item does **not** make the server the authority for:

- full branching conversation history
- approvals, audit, plans, or artifact projections
- model routing or model-aware compaction
- a real Todo module, production auth flow, or tenant-management rollout

Those remain later child items. The server-owned Chat Sessions module is only the first durable identity and lifecycle layer.
