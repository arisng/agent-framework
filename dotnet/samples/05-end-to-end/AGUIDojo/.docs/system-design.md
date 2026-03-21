# AGUIDojo system design

This document consolidates the legacy AGUIDojo design notes into one implementation-aligned view. It explains what the sample does today, the defects that matter now, and the server-owned session architecture the roadmap is targeting next.

## 1. Scope and status

**CURRENT**
- AGUIDojo is a unified `/chat` sample. `AGUIDojoClient` is a Blazor Server BFF, `AGUIDojoServer` is a Minimal API backend, and `AGUIDojo.AppHost` wires both together.
- `AGUIDojoClient` sends AG-UI traffic directly to `AGUIDojoServer /chat`. YARP proxies only `/api/*` business endpoints.
- Client session state lives in Fluxor and is hydrated from browser storage. The server does not yet own chat-session identity or persistence.
- Browser storage is a convenience cache, not the system of record.
- `/chat` is the canonical server shape. The older seven-endpoint AG-UI layout is legacy only.

**TARGET**
- Phase 0 fixes multi-turn continuity by sending full active-branch history on every `/chat` turn.
- Phase 1+ moves primary session ownership into `AGUIDojoServer` via a Chat Sessions module backed by SQLite for sample scope.
- Auth and ownership remain simulated only.
- `ConversationId`, `threadId`, `runId`, and workflow/Durable Task IDs remain correlation-only.

## 2. Current sample architecture

```text
Browser
  |
  v
AGUIDojoClient (Blazor Server BFF)
  |- Fluxor session store + browser persistence
  |- direct AG-UI streaming to AGUIDojoServer /chat
  `- YARP proxy for /api/*
        |
        v
AGUIDojoServer (Minimal API)
  |- POST /chat              unified agent route
  |- /api/weather/*          shared business APIs
  |- /api/email
  |- /api/title
  |- /api/files              attachment upload/retrieval
  |- /api/auth/dev/token     dev helper only
  `- /health
```

**CURRENT**
- `AGUIDojoClient` is intentionally a BFF. It owns UI composition and forwards business API traffic through YARP, but `/chat` bypasses YARP so SSE streaming is not buffered.
- `AGUIDojoServer` maps `/api/*` business endpoints and one `app.MapAGUI("/chat", ...)` endpoint backed by a single unified agent.
- `AGUIDojo.AppHost` injects the server URL twice: once for the direct `/chat` client and once for the YARP backend destination.
- Multimodal support is thin but real: files are uploaded through `/api/files`, stored in-memory, and resolved into `DataContent` by `MultimodalAttachmentAgent` before model execution.

**CURRENT AG-UI feature coverage**
AGUIDojo folds the legacy feature demos into one route. Today that unified pipeline covers:
1. agentic chat
2. backend tool rendering
3. human-in-the-loop approval
4. agentic generative UI
5. tool-based UI rendering
6. shared state
7. predictive state updates

That is the main reason `/chat` exists: the sample is demonstrating one combined agent surface, not a menu of separate protocol endpoints. `MapAGUI` is the transport boundary; the execution model behind it is a MAF `AIAgent` pipeline.

## 3. Request and streaming flow

**CURRENT**
1. `Chat.razor` adds the user message to the active session and asks `AgentStreamingService` to process the response.
2. `AgentStreamingService` flattens the active branch from the session's `ConversationTree`, reuses `ChatOptions.ConversationId` when present, and calls `AGUIChatClient.GetStreamingResponseAsync(...)` against `SERVER_URL/chat`.
3. `AGUIDojoServer/Program.cs` maps `/chat` to `ChatClientAgentFactory.CreateUnifiedAgent(...)`.
4. The unified pipeline currently includes:
   - `ContextWindowChatClient` with a simple non-system message cap
   - `ToolResultStreamingChatClient`, which emits `FunctionResultContent` into the stream instead of hiding it inside tool invocation
   - `MultimodalAttachmentAgent`
   - `ServerFunctionApprovalAgent`
   - `AgenticUIAgent`
   - `PredictiveStateUpdatesAgent`
   - `SharedStateAgent`
   - OpenTelemetry instrumentation
5. The server streams text, tool calls, tool results, approval requests/responses, plan snapshots/deltas, recipe/shared-state snapshots, document preview snapshots, author metadata, and `ConversationId`.
6. The client turns those updates into session-state mutations and renders them in either the message list or the artifact canvas.

**Why this wrapper stack exists**
- Tool execution is server-side, but tool results still need to be visible in the AG-UI stream.
- Approval requests need protocol translation between server-side function approval content and the client-visible approval tool pattern.
- Shared state and predictive UI need `DataContent` projections, not just final text.
- The sample is modeling one end-to-end agent pipeline, not a thin chat relay.

## 4. Client session model and artifact surfaces

### Session model

**CURRENT**
- The root Fluxor store (`SessionManagerState`) holds many `SessionEntry` objects plus the active session ID and global autonomy level.
- Each `SessionEntry` splits into:
  - `SessionMetadata`: title, status, created/last-activity timestamps, unread count, pending-approval badge, and a legacy `EndpointPath` hint.
  - `SessionState`: branching `ConversationTree`, current active-branch messages, in-progress assistant message, `ConversationId`, `StatefulMessageCount`, approval state, artifact state, audit trail, and undo state.
- Session switching is first-class. The sidebar changes the active session, clears unread count for the foreground session, and lets other sessions continue streaming in the background.
- Status values such as `Created`, `Active`, `Streaming`, `Background`, `Completed`, `Error`, and `Archived` are UI/session lifecycle markers, not server-owned records.
- Concurrency is managed client-side today: up to three active streams, with a small queue for overflow.

### Artifact rendering model

**CURRENT**
- The chat transcript renders the active branch only, but the underlying conversation structure is a client-side tree so branch switching and edit/regenerate are possible.
- `ChatMessageItem` sorts content as function calls -> function results -> data content -> text -> errors, then splits it into:
  - **inline visual content** in the message list
  - **non-visual thought/debug content** in `AssistantThought`
  - **plain text** rendered as markdown
- `ToolComponentRegistry` decides whether a tool result is shown inline or moved to the canvas pane.

### Artifact surfaces

**CURRENT**
- **Plans**: compact progress in the canvas header plus a full `PlanSheet`
- **Diff preview**: canvas tab
- **Recipe/shared state**: canvas tab with `RecipeEditor`
- **Document preview**: canvas tab with markdown preview or Monaco diff
- **Data grid**: canvas tab
- **Audit trail**: canvas tab
- **Weather / charts / forms**: inline visual tool results in the message list
- **Approvals**: inline approval UI plus background-session notifications

### Notification behavior

**CURRENT**
- Background sessions can finish, fail, or pause for approval while another session is active.
- `AgentStreamingService` emits session-scoped notifications for completion, approval required, and streaming error.
- `NotificationToast` opens the related session when the user clicks the toast.
- The session list also surfaces unread counts and pending-approval badges.

### BlazorBlueprint v3/UI role

**CURRENT**
- BlazorBlueprint is the rendering shell, not the protocol. Its Tabs, Sheet, Badge, Button, Sidebar, and dialog primitives turn session projections into artifact surfaces.
- The important design point is not the specific control library; it is that AG-UI/tool events are projected into stable client-side session artifacts instead of being treated as raw SSE text.

## 5. Current persistence boundary and limitations

**CURRENT source of truth**
- The live session state is the Fluxor store plus the current server stream.
- Browser persistence is a convenience layer.

**CURRENT browser persistence**
- **localStorage**
  - session metadata list
  - active session ID
- **IndexedDB**
  - conversation tree per session

**CURRENT limitations**
- Persistence is lossy by design:
  - `ConversationNodeDto` stores role, author name, text, parent/child links, and timestamps, but not full-fidelity AI content.
  - Approval state is not durably restored.
  - Audit history is not durably restored.
  - Plan state, diff preview, recipe state, document state, data grids, and undo state are not durably restored.
  - Hydrated sessions reset transient statuses back to `Completed`, clear unread counts, and clear pending approvals.
- Session archive/delete is still client-side cleanup: the client marks metadata as archived and deletes the local conversation record. There is no server archive API yet.
- Browser storage is therefore **not** a system of record. It cannot provide authoritative history, cross-device resume, or durable governance/audit data.
- Attachment persistence is also temporary today: uploaded files live in an in-memory file store, so attachments disappear after a server restart.

**TARGET**
- Browser persistence becomes cache, draft, and best-effort import support only.
- Durable session ownership moves to the server.

## 6. Critical continuity bug and why it matters

**CURRENT defect**
`AgentStreamingService` currently sends delta-only messages after the first successful turn:

```csharp
chatClient.GetStreamingResponseAsync(
    session.Messages.Skip(session.StatefulMessageCount),
    context.ChatOptions,
    responseCancellation.Token)
```

After a turn completes, the client stores `StatefulMessageCount = session.Messages.Count` when `ConversationId` is set. On the next turn, earlier messages are skipped and only the new tail of the branch is sent.

**Why this is wrong**
- In AGUIDojo's current `/chat` contract, `ConversationId` is correlation metadata, not proof that the backend already owns complete conversational memory.
- The server-side agent wrappers still need full active-branch history to interpret:
  - follow-up questions
  - previous tool results
  - approval decisions
  - plan/recipe/document state already discussed in the branch
- The bug is not subtle: second and later turns can forget earlier instructions inside the same session.

**TARGET / Phase 0**
- Always send the full active-branch history on every `/chat` turn.
- Keep context-window trimming as an explicit server policy, not a client-side skip heuristic.
- Apply the same rule to edit/regenerate, retry, and approval re-entry paths.

## 7. Target session architecture

**TARGET**
The next real architecture step is not "more tools." It is a server-owned Chat Sessions module inside `AGUIDojoServer`.

### Phase shape

**Phase 0**
- Fix full-history turns on `/chat`.

**Phase 1**
- Add a Chat Sessions module with create/list/get/archive semantics.
- Move primary business session ID issuance from the client to the server.
- Use SQLite for sample scope, but keep the schema relational and portable to SQL Server or PostgreSQL later.

**Phase 2**
- Persist the canonical branching conversation graph on the server.
- Store the active leaf/branch explicitly.
- Preserve richer message payloads than the current browser DTOs.

**Phase 3**
- Persist approvals, audit entries, and session artifact projections/snapshots.

**Phase 4**
- Add simulated current-user/current-tenant ownership context on server records.
- Link sessions to workflows or business entities where that makes the sample more realistic.
- Keep runtime IDs as links and correlations, not as the primary session key.

**Phase 5**
- Fully demote browser storage to cache/import behavior.
- Keep the SQLite-first sample model portable to SQL Server or PostgreSQL later.
- Keep README and internal design notes aligned with the architecture that is actually implemented.

### Practical target data ownership

| Concern | CURRENT | TARGET |
| --- | --- | --- |
| Session identity | client-generated, effectively browser-owned | server-issued business session ID |
| Conversation graph | client-only `ConversationTree` | server-owned branching graph in SQLite |
| Message fidelity | good live state, lossy browser persistence | durable message payloads with tool/state context |
| Approval history | transient UI/session state | durable approval records |
| Audit trail | transient UI/session state | durable audit records |
| Artifact state | client projection only | server snapshots/projections with client cache |
| Browser storage | convenience persistence | cache/import only |

### Practical sample-scope store

A pragmatic SQLite-backed module can stay small and still demonstrate the right boundary:
- `ChatSession`
- `ConversationNode` or equivalent parent-linked message record
- `ApprovalDecision`
- `AuditEvent`
- `ArtifactProjection` / `ArtifactSnapshot`
- `RuntimeLink`

The important point is not the exact table names. The important point is that the server, not the browser, becomes the owner of the durable business session.

## 8. Integration boundaries

**CURRENT**
- `/api/*` endpoints are shared business endpoints. The client reaches them through YARP.
- `/chat` is the direct AG-UI stream path.
- The server has auth plumbing and a dev token endpoint, but `/chat` is not protected by default.
- The wider repo contains Durable Task/workflow capabilities, but AGUIDojo does not currently use them as the chat session system of record.

**TARGET**
- Ownership/auth remains simulated only. If the sample needs user or tenant identity, use seeded/fake current-user and current-tenant context on server records.
- Durable Task/workflow integration should be additive: link sessions to workflow/entity records when useful, but do not let orchestration runtime IDs become the primary session key.
- `ConversationId`, `threadId`, `runId`, Durable Task IDs, and workflow IDs stay correlation-only.

### Identifier boundaries

| Identifier | Meaning now | Meaning later |
| --- | --- | --- |
| `sessionId` | client session key used by Fluxor and browser persistence | primary business session ID issued by server |
| `ConversationId` | AG-UI conversation correlation stored in client session state | runtime correlation only |
| `threadId` / `runId` | AG-UI runtime identifiers when surfaced by the transport | runtime correlation only |
| Durable Task / workflow IDs | downstream orchestration/runtime IDs | linked records only, never the business session key |

## 9. Non-goals and deferred items

**Not current**
- a server-owned Chat Sessions API
- SQLite-backed primary session storage
- durable attachment/blob storage
- cross-device session authority
- real auth or real tenant isolation

**Still deferred after the next session milestones**
- replay/collaboration features such as mid-run join, participant cursors, or fine-grained event journaling
- making browser storage lossless
- treating orchestration runtime IDs as product-facing session identity
- reviving the old multi-endpoint AG-UI server shape

## 10. Key code touchpoints

- `README.md` - live sample summary and current topology
- `.issues/260321_aguidojo-roadmap.md` - canonical roadmap and phase ordering
- `AGUIDojo.AppHost/AppHost.cs` - Aspire wiring between client and server
- `AGUIDojoClient/Program.cs` - Blazor Server BFF setup, direct `/chat` client, YARP proxy setup
- `AGUIDojoServer/Program.cs` - `/api/*` endpoints and unified `app.MapAGUI("/chat", ...)`
- `AGUIDojoServer/ChatClientAgentFactory.cs` - unified tool registry and wrapper pipeline
- `AGUIDojoServer/ToolResultStreamingChatClient.cs` - tool-result streaming bridge
- `AGUIDojoServer/Multimodal/MultimodalAttachmentAgent.cs` - attachment resolution into multimodal model input
- `AGUIDojoClient/Services/AgentStreamingService.cs` - direct AG-UI streaming, approvals, notifications, and the current history-skipping bug
- `AGUIDojoClient/Store/SessionManager/SessionState.cs` - per-session state model
- `AGUIDojoClient/Store/SessionManager/SessionHydrationEffect.cs` - browser hydration behavior
- `AGUIDojoClient/Store/SessionManager/SessionPersistenceEffect.cs` and `AGUIDojoClient/Services/SessionPersistenceService.cs` - localStorage/IndexedDB persistence boundary
- `AGUIDojoClient/wwwroot/js/sessionPersistence.js` - tiered browser persistence implementation
- `AGUIDojoClient/Components/Layout/CanvasPane.razor` - canvas artifact surface
- `AGUIDojoClient/Services/ToolComponentRegistry.cs` - tool-result to UI-component routing
- `AGUIDojoClient/Components/NotificationToast.razor` - background session notification UX
