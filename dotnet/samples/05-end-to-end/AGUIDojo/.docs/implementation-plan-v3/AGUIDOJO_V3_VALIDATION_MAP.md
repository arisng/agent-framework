# AGUIDojo v3 Validation Areas — Implementation & Coverage Map

**Updated:** 2026-03-19  
**Scope:** `/dotnet/samples/05-end-to-end/AGUIDojo`  
**Purpose:** Reflect the completed validation follow-up work and identify only the gaps that still remain real.

---

## Executive Summary

AGUIDojo is no longer in the “infrastructure exists but there are effectively no tests” state described by older validation notes.

### Verified outcomes from the completed session work
- A new **`AGUIDojoClient.Tests`** project now covers client-side logic and Blazor components.
- **Persistence, autonomy/governance, multimodal, and streaming** each have targeted validation coverage.
- **Confidence visualization is implemented** and should not be tracked as missing.
- **Undo grace period is implemented** and should not be tracked as missing.
- `ShouldAutoDecide` has been extracted into **`AutonomyPolicyService`**, so it is no longer private-only logic inside `AgentStreamingService`.

### Verified test state
- Client: **41 passed**
- Server: **16 passed**
- Total: **57 passed**

---

## Completed Validation Waves

| Wave | Commit | What it changed | Result |
| --- | --- | --- | --- |
| Client test scaffold | `6681d4b4` | Added `AGUIDojoClient.Tests` and client test infrastructure | Closed the “no client tests” gap |
| Persistence + autonomy coverage | `b364547d` | Added unit/component tests across persistence and governance | Closed several stale “untested/private/missing” claims |
| Multimodal coverage | `9be787d6` | Added server multimodal attachment validation tests | Closed the “0 multimodal tests” gap |
| SSE hardening | `71e0ea8d` | Hardened reconnect/queue behavior and added coverage | Streaming is tested, though not fully spec-complete |
| Undo grace period | `5d319c1f` | Added undo grace period UX/state flow and tests | Closed the “undo missing” gap |

---

## Test Infrastructure Summary

### Current test projects

| Project | Status | Notes |
| --- | --- | --- |
| `AGUIDojoClient.Tests` | ✅ Present | Client/service/model/component coverage with xUnit and bUnit |
| `AGUIDojoServer.Tests` | ✅ Present | Server validation coverage, including multimodal |

### Current test files

#### Client
- `Models/ConversationTreeTests.cs`
- `Services/SessionPersistenceServiceTests.cs`
- `Services/AgentStreamingServiceTests.cs`
- `Services/RiskAssessmentServiceTests.cs`
- `Services/AutonomyPolicyServiceTests.cs`
- `Components/Governance/AutonomySelectorTests.cs`
- `Components/Governance/ApprovalQueueTests.cs`
- `Components/Governance/UndoGracePeriodToastTests.cs`
- `Store/SessionManager/UndoGracePeriodReducerTests.cs`

#### Server
- `BasicTests.cs`
- `SharedStateAgentTests.cs`
- `MultimodalAttachmentAgentTests.cs`

### Verified totals
- **Client tests:** 41 passed
- **Server tests:** 16 passed

---

## 1. Session Persistence & Cross-Circuit Continuity

### v3 target
- L1 localStorage for lightweight metadata/preferences
- L2 IndexedDB for full conversation history
- DAG-based conversation state
- Hydration on reconnect
- Layout persistence for UX continuity

### Key implementation files

| File | Location | Purpose |
| --- | --- | --- |
| `SessionPersistenceService.cs` | `AGUIDojoClient/Services/` | Save/load metadata and conversations through JS interop |
| `sessionPersistence.js` | `AGUIDojoClient/wwwroot/js/` | localStorage + IndexedDB persistence |
| `SessionPersistenceEffect.cs` | `AGUIDojoClient/Store/SessionManager/` | Debounced persistence on state changes |
| `ConversationTree.cs` | `AGUIDojoClient/Models/` | DAG operations for branching conversation history |
| `SessionState.cs` | `AGUIDojoClient/Store/SessionManager/` | Stores active conversation tree and session state |
| `layoutPersistence.js` | `AGUIDojoClient/wwwroot/js/` | Layout/sidebar persistence |

### Tests now covering this area

| Test file | What is covered |
| --- | --- |
| `ConversationTreeTests.cs` | Root creation, branching, active-leaf switching, truncate behavior |
| `SessionPersistenceServiceTests.cs` | Metadata round-trip, conversation round-trip, invalid JSON handling |

### Current status
- ✅ Core persistence implementation exists
- ✅ DAG behavior has direct unit coverage
- ✅ Service round-trip behavior has direct unit coverage
- ❌ Hydration/circuit reconnect behavior is still not explicitly tested
- ❌ Browser-level persistence behavior (`localStorage`/IndexedDB wiring) is still not directly tested
- ❌ Layout persistence remains lightly validated compared with conversation persistence

### Real remaining gaps
1. Hydration tests for reconnect/circuit restoration
2. JS/browser-level persistence tests
3. Layout persistence validation
4. End-to-end save/load flow beyond service-level mocks

---

## 2. Multimodal Attachments

### v3 target
- Attachment upload from chat input
- Attachment markers embedded into message text
- Server-side attachment resolution into multimodal content
- Validation for allowed types and size limits
- Image/file rendering in the UI

### Key implementation files

| File | Location | Purpose |
| --- | --- | --- |
| `MultimodalAttachmentAgent.cs` | `AGUIDojoServer/Multimodal/` | Resolves attachment markers to `DataContent` |
| `FileUploadEndpoints.cs` | `AGUIDojoServer/Api/` | Handles upload + file fetch endpoints |
| `MessageAttachmentMarkers.cs` | `AGUIDojoClient/Helpers/` | Marker generation/parsing helpers |
| `AttachmentInfo.cs` | `AGUIDojoClient/Models/` | Uploaded file metadata |
| `ChatInput.razor` | `AGUIDojoClient/Components/Pages/Chat/` | Pending attachments, previews, upload error/status UI |
| `Program.cs` | `AGUIDojoServer/` | Registers `IFileStorageService` |

### Tests now covering this area

| Test file | What is covered |
| --- | --- |
| `MultimodalAttachmentAgentTests.cs` | Marker stripping, image resolution, missing attachment handling, assistant-message passthrough, allowed upload types, unsupported types, oversized file rejection, placeholder file serving |

### Current status
- ✅ Server-side multimodal path is covered
- ✅ Upload validation coverage exists for supported/unsupported types and size limits
- ✅ File storage implementation is identifiable (`InMemoryFileStorageService` registration)
- ✅ Placeholder handling for missing attachments is covered
- ⚠️ Client attachment UI exists, but this validation set does not yet test it directly
- ⚠️ End-to-end vision behavior is still not deeply validated here

### Real remaining gaps
1. Client-side attachment UI/component tests (`ChatInput` previews, remove flow, upload error state)
2. Full end-to-end multimodal chat flow validation
3. Explicit validation of any per-message attachment-count cap
4. Rendering coverage for attached content in chat history

---

## 3. Streaming / SSE Infrastructure

### v3 target
- Robust streaming loop
- Queue/backpressure protection
- Reconnect resilience
- Observability and latency tracking
- Event-stream standards alignment where applicable

### Key implementation files

| File | Location | Purpose |
| --- | --- | --- |
| `AgentStreamingService.cs` | `AGUIDojoClient/Services/` | Main streaming loop and queue/retry control |
| `SessionStreamingContext.cs` | `AGUIDojoClient/Services/` | Per-session streaming state |
| `SseStreamSnapshot.cs` | `AGUIDojoClient/Models/` | Stream metrics snapshot |
| `SseStreamMetrics.cs` | `AGUIDojoClient/Services/` | Metrics emission |
| `ContextWindowChatClient.cs` | `AGUIDojoServer/` | Context trimming middleware |
| `ToolResultStreamingChatClient.cs` | `AGUIDojoServer/` | Streams tool output content |

### Tests now covering this area

| Test file | What is covered |
| --- | --- |
| `AgentStreamingServiceTests.cs` | Queue full rejection, queued-session promotion after capacity frees, duplicate request returns existing in-flight task |

### Current status
- ✅ Queue saturation and promotion behavior are covered
- ✅ Reconnect/queue hardening landed in the completed session work
- ✅ Retry-related metrics/types still exist in the implementation
- ❌ First-token latency targets are not benchmark-tested
- ❌ `Last-Event-ID` replay support is still not documented as complete
- ❌ Native `.NET 10` SSE migration remains an open standards/alignment item
- ❌ Context trimming and tool-result streaming still lack direct tests here

### Real remaining gaps
1. `Last-Event-ID`/replay semantics
2. First-token latency benchmark or assertion coverage
3. Direct tests for `ContextWindowChatClient`
4. Direct tests for `ToolResultStreamingChatClient`
5. Telemetry/OpenTelemetry assertion coverage

---

## 4. Autonomy Controls, Confidence, and Governance

### v3 target
- Three autonomy levels
- Risk-based auto-decision policy
- Approval UI and queue
- Confidence visualization
- Undo/revert safety affordance
- Audit/history visibility

### Key implementation files

| File | Location | Purpose |
| --- | --- | --- |
| `AutonomyLevel.cs` | `AGUIDojoClient/Models/` | Governance mode enum |
| `AutonomyPolicyService.cs` | `AGUIDojoClient/Services/` | Testable `ShouldAutoDecide` policy |
| `RiskAssessmentService.cs` | `AGUIDojoClient/Services/` | Risk classification and descriptions |
| `AutonomySelector.razor` | `AGUIDojoClient/Components/Governance/` | Mode selector UI |
| `ApprovalQueue.razor` | `AGUIDojoClient/Components/Governance/` | Approval queue UI |
| `UndoGracePeriodToast.razor` | `AGUIDojoClient/Components/Governance/` | Undo grace period notification |
| `SessionReducers` / session actions | `AGUIDojoClient/Store/SessionManager/` | Pending undo state flow |
| `ChatClientAgentFactory.cs` | `AGUIDojoServer/` | Confidence instruction in system prompt |
| `ChatMessageItem.razor` | `AGUIDojoClient/Components/Pages/Chat/` | Confidence pill rendering |
| `ConfidenceMarkers.cs` | `AGUIDojoClient/Helpers/` | Confidence extraction/fallback logic |

### Tests now covering this area

| Test file | What is covered |
| --- | --- |
| `AutonomyPolicyServiceTests.cs` | Full autonomy × risk matrix |
| `RiskAssessmentServiceTests.cs` | Risk classification and description text |
| `AutonomySelectorTests.cs` | Active selection rendering and dispatch behavior |
| `ApprovalQueueTests.cs` | Risk/argument rendering, approve, reject, duplicate click prevention |
| `UndoGracePeriodReducerTests.cs` | Start/clear pending undo state behavior |
| `UndoGracePeriodToastTests.cs` | Undo toast rendering and callback wiring |

### Current status
- ✅ `ShouldAutoDecide` is now testable via `AutonomyPolicyService`
- ✅ Risk assessment has targeted test coverage
- ✅ Autonomy selector and approval queue have component coverage
- ✅ Undo grace period is implemented and covered
- ✅ Confidence instruction + UI indicator exist
- ❌ Confidence-specific tests are not a major focus of the current suite
- ❌ Audit trail rendering/behavior still lacks direct validation
- ❌ Full streaming approval lifecycle coverage remains limited

### Real remaining gaps
1. Audit trail tests
2. Approval flow coverage through the live streaming path
3. More direct confidence parsing/rendering tests if desired
4. Deeper runtime/browser verification of governance UX interactions

---

## Stale Claims Removed by This Refresh

These claims were accurate in the earlier snapshot but are now stale:
- “0 tests”
- “Only 2 server test files”
- “Confidence visualization missing”
- “Undo grace period missing”
- “`ShouldAutoDecide` is still private”
- “`IFileStorageService` implementation location unclear”

---

## Recommended Next Validation Work

### Highest priority
1. Persistence hydration / reconnect tests
2. Client attachment UX tests
3. SSE standards/replay coverage
4. Audit trail and streaming approval lifecycle tests

### Useful but lower priority
5. Confidence parsing/rendering tests
6. Context-window/tool-result middleware tests
7. Latency/telemetry assertions

---

## Reference to v3 Plan

| Area | Plan section | Current read |
| --- | --- | --- |
| Session Persistence | §2 | Core implementation + targeted tests; reconnect/browser validation still open |
| Multimodal Attachments | §6 | Server-side path covered; client UX and full E2E remain open |
| Streaming/SSE | §12 | Hardened and partially tested; replay/native SSE/latency items remain open |
| Autonomy Controls | §11 | Governance, confidence, and undo are implemented; some end-to-end/audit coverage still open |
