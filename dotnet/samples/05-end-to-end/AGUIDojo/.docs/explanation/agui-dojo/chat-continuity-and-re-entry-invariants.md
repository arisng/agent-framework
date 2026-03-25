# AGUIDojo chat continuity and re-entry invariants

AGUIDojo's unified `/chat` surface is stateful because the client always resends the **full active branch** of the conversation tree on every follow-up turn. The transport-level identifiers that come back from AG-UI (`ConversationId`, `threadId`, and `runId`) are useful for correlation and diagnostics, but they do not authorize the client to omit prior history.

## Core invariant

For every `/chat` request, the client must send the entire active branch from root to the current leaf:

- ordinary follow-up turns
- same-session continuation after a prior response
- approval submit/reject flows
- retry/restart and checkpoint re-entry flows
- edit-and-regenerate branches

That full-history rule also applies to tool execution boundaries:

- an assistant message that carries `tool_calls` must stay paired with the matching `tool` response messages for those `tool_call_id` values
- a later follow-up request must not replay a server-only tool chain into an unrelated client-tool invocation
- once an assistant tool-call turn has been abandoned by a later non-tool boundary, later orphan tool results for that abandoned call must be dropped instead of replayed

If prompt-size management is needed, the server owns that policy. In the current sample that means `ContextWindowChatClient` trims invocation context on the server side. Any future compaction or summarization must remain an explicit server-owned behavior rather than a silent client-side skip heuristic.

## Current implementation touchpoints

- `AGUIDojoClient/Store/SessionManager/SessionState.cs` exposes `Messages` by flattening `ConversationTree.GetActiveBranchMessages()`.
- `AGUIDojoClient/Models/ConversationTree.cs` walks from the active leaf back to the root and reverses that path into root-to-leaf order.
- `AGUIDojoClient/Services/AgentStreamingService.cs` passes `session.Messages` directly to `GetStreamingResponseAsync(...)`.
- `AGUIDojoClient/Store/SessionManager/SessionReducers.cs` preserves the invariant across branch-changing actions:
  - `EditAndRegenerateAction` swaps the active branch to the edited replacement branch
  - `TrimMessagesAction` truncates the active branch for checkpoint restore/restart flows
  - `SetConversationIdAction` updates correlation metadata only

## Correlation-only identifiers

The identifiers below are not session-memory authority:

- `ConversationId` from AG-UI responses
- AG-UI `threadId` (persisted in AGUIDojo as `AguiThreadId`)
- AG-UI `runId`
- downstream workflow or Durable Task runtime IDs

They are still valuable because they let AGUIDojo correlate client-side sessions with server rows, diagnostics, traces, and future durable session records. But continuity still comes from the canonical active branch that the client resends each turn.

## Re-entry behavior

### Approval submit / reject

Approval requests arrive as function-call content in the assistant stream. When the user approves or rejects, the client records that decision as a tool message on the same active branch. A subsequent `/chat` invocation must therefore include:

1. the prior branch context
2. the assistant approval request message
3. the approval decision tool message
4. any new user follow-up

The same structural rule applies to non-approval tool usage: the replayed branch must still contain valid assistant/tool adjacency for each surviving tool call, not just the visible text summary.

### Edit and regenerate

Edit-and-regenerate creates a new branch rooted at the edited message's parent. The next `/chat` request uses the replacement branch, not the superseded sibling branch, but it still sends the full replacement branch from root to the new active leaf.

### Retry / restart / checkpoint revert

Checkpoint restore trims the active branch to the checkpoint's retained message count and clears correlation metadata. The next `/chat` turn sends that retained branch in full. Resetting `ConversationId` or regenerating `threadId` does not change the full-history rule.

## Regression coverage

`AGUIDojoClient.Tests/Services/AgentStreamingServiceTests.cs` now covers:

- an ordinary follow-up turn with an existing `ConversationId`
- edit-and-regenerate branch replacement
- checkpoint re-entry after trimming the active branch
- approval approval-submit re-entry within the same stream
- same-session continuation after an approval rejection
- completed streaming turns strip unmatched tool calls before they enter replayable client history
- completed streaming turns preserve matched call/result pairs so live-tab follow-up requests still resend structurally valid tool history

The AGUI layer now also carries targeted regression coverage for the `260325` tool-call replay bug:

- incomplete assistant tool-call turns are dropped from outbound AG-UI history
- standalone tool messages with meaningful payloads still round-trip correctly
- orphan tool results tied to abandoned assistant turns are skipped
- server-only tool replay markers are not leaked into follow-up client-tool requests

Those tests guard the contract that `/chat` receives full active-branch history, that tool-call chains remain structurally valid, and that correlation IDs remain correlation only.

The follow-up hardening matters because AGUIDojo has two replay boundaries:

- durable server history restored from `/api/chat-sessions/...`
- same-tab live history captured from the just-finished streaming assistant message

Both boundaries now normalize tool-call structure before that history can be replayed on the next `/chat` turn.
