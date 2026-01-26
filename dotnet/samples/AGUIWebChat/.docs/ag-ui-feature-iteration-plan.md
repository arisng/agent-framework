# AG-UI Feature Iteration Plan (AGUIWebChat)

## Overview
This plan defines a feature-by-feature implementation path for the seven AG-UI protocol features in AGUIWebChat. Each section includes UX goals, a pragmatic use case, implementation steps, manual UI verification, and completion criteria. Use this plan to iterate one feature at a time.

## Feature Status Snapshot

| Feature                  | Current Coverage               | Iteration Intent                                 |
| ------------------------ | ------------------------------ | ------------------------------------------------ |
| Agentic Chat             | Implemented                    | Maintain and harden UX consistency               |
| Backend Tool Rendering   | Implemented                    | Improve clarity and observability of tool events |
| Human in the Loop        | Not implemented                | Add approval flows for sensitive tools           |
| Agentic Generative UI    | Partially implemented          | Expand state-driven UI experiences               |
| Tool-based Generative UI | Not implemented                | Render structured UI components from tool calls  |
| Shared State             | Implemented (server-to-client) | Add client-driven state mutations                |
| Predictive State Updates | Not implemented                | Add optimistic, streamed state previews          |

---

## 1) Agentic Chat

**UX/UI Goals**
- Preserve smooth, low-latency streaming with visible conversation continuity.
- Make assistant/tool messages visually distinct and easy to scan.

**Pragmatic Use Case**
- User asks for a plan; assistant streams the response and updates the conversation thread without stalling.

**Implementation Steps**
1. Ensure streaming tokens append to the latest assistant message without flicker.
2. Keep conversation identity stable across refresh/session continuation.
3. Add minimal UI affordances to distinguish assistant vs. tool output.

**Manual UI Test Checklist**
- [ ] Start a new conversation and verify the assistant response streams smoothly.
- [ ] Refresh the page; verify the conversation history and `ConversationId` continuity.
- [ ] Trigger a tool call and verify tool output is visually distinct.

**Completion Criteria**
- Streaming is continuous and messages render in order without duplication.
- Conversation continuity survives a full page refresh.

---

## 2) Backend Tool Rendering

**UX/UI Goals**
- Tool executions are clearly attributable and easy to inspect.
- Tool results are presented as structured outputs, not raw noise.

**Pragmatic Use Case**
- A planning tool updates the plan; users can see which tool ran and the resulting data.

**Implementation Steps**
1. Annotate tool events with tool name and timestamp in the UI.
2. Render tool result payloads in a compact, readable layout (collapsible JSON if needed).
3. Include “tool start” and “tool complete” indicators for long-running tools.

**Manual UI Test Checklist**
- [ ] Run a planning tool and verify the tool name is displayed with the result.
- [ ] Verify tool output can be collapsed/expanded for readability.
- [ ] Verify the UI indicates when a tool is running vs. completed.

**Completion Criteria**
- Tool results are consistently labeled and discoverable.
- Long outputs are readable without overwhelming the chat.

---

## 3) Human in the Loop

**UX/UI Goals**
- Users can approve or deny sensitive actions with clear context.
- Approval prompts are unambiguous and include a “why this is needed” explanation.

**Pragmatic Use Case**
- A tool that modifies an external system prompts the user for approval.

**Implementation Steps**
1. Introduce a tool approval requirement and surface a modal/banner prompt.
2. Provide a summary of the pending tool action (inputs, impact).
3. Capture approval/denial and log the decision in the chat transcript.

**Manual UI Test Checklist**
- [ ] Trigger a tool marked for approval and verify an approval prompt appears.
- [ ] Deny the tool action and confirm no execution happens.
- [ ] Approve the tool action and confirm execution proceeds and is logged.

**Completion Criteria**
- Approval prompts are consistent, actionable, and recorded.
- No sensitive tool executes without explicit user approval.

---

## 4) Agentic Generative UI

**UX/UI Goals**
- State updates drive meaningful UI changes beyond plain JSON.
- Users can see evolving state in a dedicated UI region.

**Pragmatic Use Case**
- Planning steps update a visual plan panel as the agent progresses.

**Implementation Steps**
1. Identify a state model that benefits from visual rendering (e.g., plan steps).
2. Render state updates in a dedicated panel that updates as deltas arrive.
3. Provide clear timestamps or ordering for state transitions.

**Manual UI Test Checklist**
- [ ] Trigger a plan update tool and verify state changes reflect in a UI panel.
- [ ] Confirm JSON Patch deltas update the UI without full re-rendering glitches.
- [ ] Verify state transitions are ordered and user-readable.

**Completion Criteria**
- State updates are visual and easy to interpret without raw JSON.
- Delta updates apply correctly and consistently.

---

## 5) Tool-based Generative UI

**UX/UI Goals**
- Tool calls map to purpose-built UI components (forms, cards, summaries).
- The UI adapts to tool output types rather than showing generic text.

**Pragmatic Use Case**
- A “create plan” tool renders a plan card UI with steps and status.

**Implementation Steps**
1. Define a tool-to-component mapping contract for common tool outputs.
2. Implement at least one custom component for a known tool output schema.
3. Add graceful fallback rendering for unknown tool outputs.

**Manual UI Test Checklist**
- [ ] Trigger a known tool and verify a custom component renders.
- [ ] Trigger an unknown/unsupported tool and verify fallback rendering appears.
- [ ] Validate the custom UI component updates with repeated tool calls.

**Completion Criteria**
- At least one tool output renders as a custom UI component.
- Unsupported tool output falls back safely without errors.

---

## 6) Shared State

**UX/UI Goals**
- State is synchronized both ways: server-to-client and client-to-server.
- User edits persist and are reflected in subsequent state deltas.

**Pragmatic Use Case**
- User reorders plan steps; the server acknowledges and streams updated state.

**Implementation Steps**
1. Add a client-side state mutation flow (e.g., edit or reorder steps).
2. Send state mutation requests to the server and receive updated deltas.
3. Resolve conflicts deterministically (last-write or server-authoritative).

**Manual UI Test Checklist**
- [ ] Modify state client-side and verify the UI updates immediately.
- [ ] Confirm the server acknowledges the change and sends updated state.
- [ ] Refresh the page and verify the edited state persists.

**Completion Criteria**
- Client-originated mutations are supported and persisted.
- Server and client state remain in sync after edits.

---

## 7) Predictive State Updates

**UX/UI Goals**
- Users see optimistic previews of changes while tools execute.
- The UI resolves optimistic previews to confirmed state without confusion.

**Pragmatic Use Case**
- While a tool runs, the UI shows a “pending” plan step before final output arrives.

**Implementation Steps**
1. Stream tool arguments as optimistic state patches before tool completion.
2. Mark optimistic updates visually (e.g., “pending” badge).
3. Reconcile optimistic updates with final tool results and remove pending indicators.

**Manual UI Test Checklist**
- [ ] Trigger a tool and verify an optimistic state update appears immediately.
- [ ] Confirm the pending state resolves to the final state once the tool finishes.
- [ ] Validate no duplicate or conflicting state remains after reconciliation.

**Completion Criteria**
- Optimistic updates are visible and clearly labeled.
- Final tool output reconciles with no orphaned pending state.

---

## References
- AG-UI integration with Agent Framework: https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/
- AG-UI protocol introduction: https://docs.ag-ui.com/introduction
