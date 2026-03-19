# AGUIDojo v3 Validation Areas — Implementation & Test Mapping

**Date:** 2026-03-18  
**Scope:** Mapping implementation files and existing tests for AGUIDojo v3 validation areas  
**Source Docs:** 
- Implementation plan v3: `/AGUIDojo/.docs/implementation-plan-v3/unified-agentic-chat-implementation-plan-v3.md`
- Architecture guide: `/AGUIDojo/.docs/implementation-plan-v3/AGUIDOJO_ARCHITECTURE_DIAGRAM.md`

---

## Executive Summary

AGUIDojo has **partial v3 infrastructure** already in place:
- ✅ **Session Persistence**: Core infrastructure (L1 localStorage + L2 IndexedDB) is implemented
- ✅ **Multimodal Attachments**: Server-side attachment resolution exists (MultimodalAttachmentAgent)
- ✅ **SSE/Streaming**: Comprehensive SSE infrastructure with metrics and backpressure
- ✅ **Autonomy Controls**: Three-level autonomy model with risk-based auto-approval
- ⚠️ **Tests**: Only 2 basic server tests exist; **client-side persistence, streaming, and autonomy tests are MISSING**

---

## 1. Session Persistence & Cross-Circuit Continuity

### v3 Expected Behavior (from implementation-plan-v3.md §2)
- **Tier L0:** Fluxor in-memory state (active session) — existing
- **Tier L1:** localStorage for metadata + active session ID  
- **Tier L2:** IndexedDB for full conversation histories (keyed by sessionId)
- **Hydration:** On Blazor circuit reconnect, restore SessionManagerState from IndexedDB
- **Layout persistence:** Splitter position, sidebar collapsed state → localStorage

### ✅ Implementation Files

#### Client-Side Persistence

| File | Location | Lines | Purpose |
|------|----------|-------|---------|
| `SessionPersistenceService.cs` | `AGUIDojoClient/Services/` | 210 | C# service interfacing with JS interop; handles save/load of metadata (L1) and conversation trees (L2) via DTOs |
| `ISessionPersistenceService` | `AGUIDojoClient/Services/` | 22 | Interface defining `SaveMetadataAsync`, `LoadMetadataAsync`, `SaveConversationAsync`, `LoadConversationAsync`, `DeleteConversationAsync` |
| `sessionPersistence.js` | `AGUIDojoClient/wwwroot/js/` | 155 | JS module: localStorage (metadata, active ID) + IndexedDB (conversation trees); DB schema with version 1 |
| `layoutPersistence.js` | `AGUIDojoClient/wwwroot/js/` | — | JS module for UI layout persistence (splitter, sidebar state) |

#### Fluxor State & Effects

| File | Location | Purpose |
|------|----------|---------|
| `SessionPersistenceEffect.cs` | `AGUIDojoClient/Store/SessionManager/` | Fluxor effect listening to actions (`AddMessage`, `EditAndRegenerate`, `SwitchBranch`, `ClearMessages`, `TrimMessages`, `CreateSession`, `SetSessionTitle`, `SetActiveSession`, `ArchiveSession`); debounced IndexedDB writes (500ms) |
| `SessionState.cs` | `AGUIDojoClient/Store/SessionManager/` | Holds `ConversationTree` (replaces old `Messages` list); metadata like `ConversationId`, `StatefulMessageCount` |
| `ConversationTree.cs` | `AGUIDojoClient/Models/` | DAG-based message tree with `AddMessage()`, `BranchAt()`, `SwitchToLeaf()`, `GetActiveBranchMessages()`, `TruncateActiveBranch()`, `GetBranchInfo()` |

#### Data Models

| Model | Location | Purpose |
|-------|----------|---------|
| `ConversationNode` | `AGUIDojoClient/Models/ConversationTree.cs` | Single DAG node: `Id`, `ParentId`, `Message`, `ChildIds`, `CreatedAt` |
| `ConversationTree` | `AGUIDojoClient/Models/ConversationTree.cs` | Root structure: `Nodes` (immutable dict), `RootId`, `ActiveLeafId` |
| `ConversationNodeDto` | `SessionPersistenceService.cs` | Serialization DTO for JSON persistence |
| `ConversationTreeDto` | `SessionPersistenceService.cs` | Serialization DTO for conversation trees |
| `SessionMetadataDto` | `SessionPersistenceService.cs` | Lightweight metadata: `Id`, `Title`, `EndpointPath`, `Status`, `CreatedAt`, `LastActivityAt` |

### ❌ Test Coverage

| Area | Exists? | Location | Status |
|------|---------|----------|--------|
| Persistence save/load cycle | ❌ | — | **MISSING** — No xUnit tests for `SessionPersistenceService` |
| IndexedDB integration | ❌ | — | **MISSING** — No JS interop tests |
| Hydration on circuit reconnect | ❌ | — | **MISSING** — No Fluxor effect tests |
| Conversation tree DAG operations | ❌ | — | **MISSING** — No unit tests for `ConversationTree` branching logic |
| Layout persistence | ❌ | — | **MISSING** — No tests for `layoutPersistence.js` |

### 🔴 Obvious Gaps

1. **No integration tests** verifying L1 + L2 persistence flow end-to-end
2. **No mock IndexedDB tests** (would need `jest` or similar JS test framework)
3. **No hydration tests** — circuit reconnection scenario not covered
4. **No DAG operation tests** — branching, node traversal not validated
5. **No deletion/archival tests** — cleanup of old sessions from IndexedDB
6. **Layout persistence incomplete** — `layoutPersistence.js` exists but unclear if wired into components

---

## 2. Multimodal Attachments

### v3 Expected Behavior (from implementation-plan-v3.md §6)

**Client-Side:**
- File upload button + drag-and-drop in ChatInput
- Validate type/size; convert images to base64
- Display attachment preview chips below input
- Include multimodal content in message rendering

**Server-Side:**
- Resolve attachment markers from user message text
- Convert to `ImageContent` / `DataContent` in ChatMessage
- Max 10MB per file, 5 attachments per message
- New `analyze_image` tool for vision queries

### ✅ Implementation Files

#### Server-Side Attachment Resolution

| File | Location | Purpose |
|------|----------|---------|
| `MultimodalAttachmentAgent.cs` | `AGUIDojoServer/Multimodal/` | Wrapper agent that strips attachment markers (`<!-- file:ID:FILENAME:CONTENTTYPE -->`) from text and resolves binary data from `IFileStorageService`; converts to `DataContent` |
| `MessageAttachmentMarkers.cs` | `AGUIDojoClient/Helpers/` | Helper class for generating/parsing attachment markers in message text |

#### Client-Side Models

| Model | Location | Purpose |
|-------|----------|---------|
| `AttachmentInfo` | `AGUIDojoClient/Models/` | DTO for uploaded file: `Id`, `FileName`, `ContentType`, `Size` |

#### API Endpoints (Server)

| Endpoint | Location | Purpose |
|----------|----------|---------|
| `FileUploadEndpoints` | `AGUIDojoServer/Api/` | POST `/api/upload` — handles file receipt, storage, returns `AttachmentInfo` |

### ❌ Test Coverage

| Area | Exists? | Status |
|------|---------|--------|
| Attachment marker parsing | ❌ | **MISSING** — No tests for `MessageAttachmentMarkers` |
| MultimodalAttachmentAgent resolution | ❌ | **MISSING** — No unit/integration tests |
| File upload validation (type/size) | ❌ | **MISSING** |
| Base64 encoding client-side | ❌ | **MISSING** |
| Attachment preview UI | ❌ | **MISSING** — Component exists but not tested |
| Image/PDF rendering in messages | ❌ | **MISSING** |
| LLM vision capability integration | ❌ | **MISSING** |

### 🔴 Obvious Gaps

1. **No server-side tests** for `MultimodalAttachmentAgent` or `FileUploadEndpoints`
2. **No client-side attachment handling tests** — file upload, preview, serialization
3. **No vision model integration tests** — unclear if `analyze_image` tool is fully implemented
4. **No size/type validation tests** — 10MB / 5-attachment limits not verified
5. **No image rendering tests** — how inline images are displayed not covered
6. **No error handling tests** — upload failures, unsupported file types
7. **File storage implementation unclear** — `IFileStorageService` implementation location unknown

---

## 3. Streaming/SSE Infrastructure Upgrades

### v3 Expected Behavior (from implementation-plan-v3.md §12)

- **Native .NET 10 SSE:** Use `System.Net.ServerSentEvents` + `TypedResults.ServerSentEvents()`
- **Backpressure handling:** Detect slow clients, bounded buffers, throttle/drop for congestion
- **First-token latency:** Target <250ms; immediate flush (no batching)
- **Reconnection:** SSE standard `Last-Event-ID` header for replay
- **Observability:** Metrics for first-token latency, throughput, concurrent connections, reconnections

### ✅ Implementation Files

#### Client-Side Streaming

| File | Location | Purpose |
|------|----------|---------|
| `AgentStreamingService.cs` | `AGUIDojoClient/Services/` | Main coordinator for AG-UI SSE loop; manages session contexts, backpressure (3 concurrent, 5 queued), retry logic (3 attempts max) |
| `IAgentStreamingService` | `AGUIDojoClient/Services/` | Interface for streaming lifecycle |
| `SessionStreamingContext.cs` | `AGUIDojoClient/Services/` | Per-session mutable state: cancellation token, function call tracking, streaming message, approval task |
| `SseStreamSnapshot.cs` | `AGUIDojoClient/Models/` | Metrics snapshot: `FirstTokenLatencyMs`, `EventCount`, `DurationMs`, `RetryCount`, `ConnectionState` |
| `SseStreamMetrics.cs` | `AGUIDojoClient/Services/` | Collects and records SSE metrics to OpenTelemetry |

#### Server-Side Infrastructure

| File | Location | Purpose |
|------|----------|---------|
| `ToolResultStreamingChatClient.cs` | `AGUIDojoServer/` | Middleware that emits tool results as streaming content |
| `ContextWindowChatClient.cs` | `AGUIDojoServer/` | Middleware that trims old messages to stay within token budget |

### ✅ Features Implemented

- ✅ `MaxConcurrentStreams = 3` and `MaxQueuedStreams = 5` (backpressure)
- ✅ `MaxRetryAttempts = 3` (resilience)
- ✅ `SessionStreamingContext` per-session (isolation)
- ✅ `SseStreamSnapshot` metrics collection (observability)
- ✅ Cancellation token support (graceful shutdown)
- ✅ Function call deduplication (`SeenFunctionCallIds` tracking)

### ❌ Test Coverage

| Area | Exists? | Status |
|------|---------|--------|
| First-token latency measurement | ❌ | **MISSING** — `FirstTokenLatencyMs` collected but not tested |
| Backpressure (3 concurrent limit) | ❌ | **MISSING** |
| Queue overflow handling | ❌ | **MISSING** |
| Retry logic (3 attempts) | ❌ | **MISSING** |
| SSE reconnection with Last-Event-ID | ❌ | **MISSING** |
| Streaming message accumulation | ❌ | **MISSING** |
| Metrics recording to OpenTelemetry | ❌ | **MISSING** |
| Tool result streaming | ❌ | **MISSING** — `ToolResultStreamingChatClient` not tested |
| Context window trimming | ❌ | **MISSING** — `ContextWindowChatClient` not tested |
| Cancellation/timeout handling | ❌ | **MISSING** |

### ⚠️ Partial Implementation

- **Native .NET 10 SSE:** Not yet migrated from custom SSE handling (uses `IChatClient` streaming, not `TypedResults.ServerSentEvents()`)
- **Backpressure:** Implementation exists but untested
- **Reconnection:** No `Last-Event-ID` header logic visible in current code

### 🔴 Obvious Gaps

1. **No integration tests** for full SSE streaming loop
2. **No backpressure/concurrency tests** — queue behavior not validated
3. **No latency benchmarks** — <250ms first-token target not measured
4. **No retry/reconnection tests** — fault tolerance not covered
5. **No .NET 10 SSE migration** — still using older streaming pattern
6. **Observability untested** — OpenTelemetry metrics not validated

---

## 4. Autonomy Controls & Confidence Visualization

### v3 Expected Behavior (from implementation-plan-v3.md §11)

- **Three autonomy levels:**
  - `Suggest`: All tool calls require explicit approval (HITL)
  - `AutoReview`: Low-risk calls auto-approved; medium/high need approval
  - `FullAuto`: All calls auto-approved except Critical risk
- **Risk-based decisions:** Risk level (Low/Medium/High/Critical) determines auto-approval
- **Confidence visualization:** Agent self-rates confidence; UI shows indicator badge
- **Audit trail:** Activity tab with timeline of tool calls, approvals, branch points

### ✅ Implementation Files

#### Autonomy Level Model & State

| File | Location | Purpose |
|------|----------|---------|
| `AutonomyLevel.cs` | `AGUIDojoClient/Models/` | Enum: `Suggest`, `AutoReview`, `FullAuto` |
| `SessionManagerState` | `AGUIDojoClient/Store/SessionManager/` | Holds `AutonomyLevel` (property + reducer) |
| `SessionActions.SetAutonomyLevelAction` | `AGUIDojoClient/Store/SessionManager/` | Fluxor action to change autonomy |

#### Risk Assessment & Auto-Approval

| File | Location | Purpose |
|------|----------|---------|
| `RiskAssessmentService.cs` | `AGUIDojoClient/Services/` | Evaluates tool call risk level; provides `RiskLevel` enum |
| `IRiskAssessmentService` | `AGUIDojoClient/Services/` | Interface for risk determination |
| `ApprovalHandler.cs` | `AGUIDojoClient/Services/` | Coordinates approval workflow; applies autonomy rules |
| `ServerFunctionApprovalAgent.cs` | `AGUIDojoServer/HumanInTheLoop/` | Server-side agent managing approval requests |

#### Approval UI Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `AutonomySelector.razor` | `AGUIDojoClient/Components/Governance/` | Segmented button control for selecting autonomy level; three buttons (Suggest/Auto/Full) |
| `AutonomySelector.razor.css` | `AGUIDojoClient/Components/Governance/` | Styling for autonomy selector |
| `ApprovalDialog.razor` | `AGUIDojoClient/Components/Approvals/` | Modal dialog for human approval of tool calls |
| `ApprovalQueue.razor` | `AGUIDojoClient/Components/Governance/` | Queue display for pending approvals |

#### Audit Trail

| File | Location | Purpose |
|------|----------|---------|
| `AuditEntry` | `AGUIDojoClient/Models/` | Record of action: `Id`, `Timestamp`, `ActionType`, `ToolName`, `WasAutoDecided`, `AutonomyLevel`, `Risk` |
| `AuditTrailPanel.razor` | `AGUIDojoClient/Components/Observability/` | UI panel showing audit trail with expandable entries |
| `SessionState.AuditTrail` | `AGUIDojoClient/Store/SessionManager/` | Immutable list of audit entries per session |

#### Auto-Approval Logic

| Logic | Location | Implementation |
|-------|----------|-----------------|
| `ShouldAutoDecide()` | `AgentStreamingService.cs` | Matches autonomy level + risk level: `Suggest` → always false; `AutoReview` → only if `risk <= Low`; `FullAuto` → only if `risk < Critical` |

### ❌ Test Coverage

| Area | Exists? | Status |
|------|---------|--------|
| AutonomyLevel enum | ❌ | **MISSING** — Basic enum not tested |
| Risk assessment logic | ❌ | **MISSING** — `RiskAssessmentService` not tested |
| Auto-approval decision (`ShouldAutoDecide`) | ❌ | **MISSING** — Core logic not validated |
| Autonomy selector UI interaction | ❌ | **MISSING** — Component not tested |
| Approval workflow (approve/reject) | ❌ | **MISSING** |
| Audit trail recording | ❌ | **MISSING** |
| Confidence scoring/visualization | ❌ | **MISSING** — No confidence badge implementation visible |
| Server-side approval agent | ❌ | **MISSING** — `ServerFunctionApprovalAgent` not tested |

### ⚠️ Partial Implementation

- **Autonomy levels:** Defined and integrated into state
- **Risk assessment:** Service exists but risk scoring logic unclear
- **Auto-approval:** `ShouldAutoDecide()` implemented but untested
- **Approval UI:** Components exist (`AutonomySelector`, `ApprovalDialog`)
- **Audit trail:** Infrastructure in place (`AuditEntry`, `AuditTrailPanel`)
- **Confidence visualization:** NOT YET IMPLEMENTED (system prompt modification not visible)

### 🔴 Obvious Gaps

1. **No unit tests** for autonomy/risk/approval logic
2. **No approval workflow tests** — approve, reject, timeout scenarios
3. **Confidence scoring untested** — no system prompt instruction for self-rating
4. **No integration tests** — full autonomy + streaming lifecycle
5. **Risk scoring unclear** — how tools are categorized as Low/Med/High/Critical
6. **Confidence badge/indicator** — design/implementation not visible
7. **5-second grace period for undo** — not implemented in current code
8. **Activity tab visualization** — partial (audit trail exists but "Activity" tab UX unclear)

---

## Test Infrastructure Summary

### Existing Test Files

```
AGUIDojoServer.Tests/
├── BasicTests.cs                    (1 smoke test)
├── SharedStateAgentTests.cs         (2 shared state detection tests)
└── AGUIDojoServer.Tests.csproj
```

**Total:** 3 tests, all server-side, all basic  
**Missing:** 
- Client-side tests (Blazor components, services)
- Integration tests (end-to-end flows)
- Streaming/SSE tests
- Persistence tests
- Multimodal tests
- Autonomy/approval tests

### Test Framework

- ✅ xUnit (configured in `.csproj`)
- ❌ JavaScript test framework (jest/vitest) — not visible
- ❌ Blazor component tests (bUnit) — not visible

---

## v3 Implementation Plan References

### Key Sections from `unified-agentic-chat-implementation-plan-v3.md`

| Section | Page | Key Quote | Status |
|---------|------|-----------|--------|
| Session Persistence | §2 | "Tier L0–L3 persistence strategy; L1 localStorage + L2 IndexedDB for v3" | Implemented L1+L2 |
| Multimodal Attachments | §6 | "Client drops → Base64 → attach to message → LLM processes" | Partially implemented |
| Streaming/SSE | §12 | ".NET 10 native SSE; <250ms first-token latency; Last-Event-ID reconnection" | Infrastructure exists but untested |
| Autonomy Controls | §11 | "Three levels (Suggest/Auto/FullAuto); risk-based decisions; confidence badges" | Autonomy infrastructure exists; confidence not yet implemented |

### Phase Recommendations (from v3 plan §13)

- **Phase 14 (Session Persistence):** Implement after Phase 6; estimated M–L effort
- **Phase 16 (Multimodal):** New phase; estimated M–L effort
- **Phase 19 (SSE Upgrades):** Phase 2/10 extension; estimated M effort
- **Phase 22 (Autonomy):** New phase; estimated M–L effort

---

## Compact Mapping by Area

### 1. SESSION PERSISTENCE

**Implementation:** ✅ Core (80% complete)  
**Tests:** ❌ NONE

| Category | Files |
|----------|-------|
| **Key Implementation** | `SessionPersistenceService.cs`, `sessionPersistence.js`, `ConversationTree.cs`, `SessionPersistenceEffect.cs` |
| **Models** | `ConversationTree`, `ConversationNode`, `SessionMetadataDto` |
| **Tests** | (MISSING) |
| **Gaps** | No integration tests; no hydration tests; no DAG operation tests; layout persistence status unclear |

---

### 2. MULTIMODAL ATTACHMENTS

**Implementation:** ⚠️ Partial (40% complete)  
**Tests:** ❌ NONE

| Category | Files |
|----------|-------|
| **Key Implementation** | `MultimodalAttachmentAgent.cs`, `FileUploadEndpoints.cs`, `AttachmentInfo.cs`, `MessageAttachmentMarkers.cs` |
| **UI Components** | (ChatInput enhancement not clearly visible) |
| **Tests** | (MISSING) |
| **Gaps** | No attachment marker tests; no upload validation tests; no vision model tests; file storage implementation unclear |

---

### 3. STREAMING/SSE INFRASTRUCTURE

**Implementation:** ✅ Substantial (70% complete)  
**Tests:** ❌ NONE

| Category | Files |
|----------|-------|
| **Key Implementation** | `AgentStreamingService.cs`, `SessionStreamingContext.cs`, `SseStreamSnapshot.cs`, `ToolResultStreamingChatClient.cs`, `ContextWindowChatClient.cs` |
| **Metrics** | `SseStreamMetrics.cs` |
| **Tests** | (MISSING) |
| **Gaps** | No .NET 10 SSE migration; no backpressure tests; no latency benchmarks; no reconnection tests; no concurrency tests |

---

### 4. AUTONOMY CONTROLS & CONFIDENCE

**Implementation:** ⚠️ Partial (60% complete)  
**Tests:** ❌ NONE

| Category | Files |
|----------|-------|
| **Key Implementation** | `AutonomyLevel.cs`, `RiskAssessmentService.cs`, `ApprovalHandler.cs`, `AutonomySelector.razor`, `AutonomySelector.razor.css` |
| **Audit Trail** | `AuditEntry.cs`, `AuditTrailPanel.razor`, `SessionState.AuditTrail` |
| **Risk Logic** | `AgentStreamingService.ShouldAutoDecide()` |
| **Tests** | (MISSING) |
| **Gaps** | No autonomy logic tests; no risk assessment tests; no approval workflow tests; confidence scoring/visualization NOT implemented; 5-sec undo grace period NOT implemented |

---

## Recommendations for Test Coverage

### High Priority (Blocking Features)
1. **Session Persistence Tests:**
   - `SessionPersistenceService` save/load cycle
   - ConversationTree DAG operations (add, branch, switch, truncate)
   - IndexedDB mock/integration tests

2. **Autonomy/Approval Tests:**
   - `ShouldAutoDecide()` decision matrix (3 levels × 4 risk levels = 12 cases)
   - `RiskAssessmentService` risk scoring
   - Approval handler workflow (approve/reject/timeout)

3. **SSE Infrastructure Tests:**
   - Backpressure (3 concurrent limit)
   - Queue overflow handling
   - Retry logic (3 attempts)
   - First-token latency measurement

### Medium Priority (Feature Completeness)
4. **Multimodal Tests:**
   - Attachment marker parsing
   - File upload validation (type/size)
   - `MultimodalAttachmentAgent` resolution

5. **Streaming Tests:**
   - Context window trimming
   - Tool result streaming
   - Cancellation/timeout

### Low Priority (Polish)
6. **Confidence Visualization:**
   - Implement system prompt instruction
   - Confidence badge rendering
   - Tests

7. **UI Component Tests (bUnit):**
   - `AutonomySelector` interaction
   - `ApprovalDialog` approve/reject
   - `AuditTrailPanel` rendering

---

## File Inventory

### Client-Side Implementation

**Models:** 27 files (60+ model classes)
**Services:** 16 services (3,515 LOC)
**Components:** ~40 Razor components (Chat, Governance, Observability, Approvals)
**Store:** 8 Fluxor files (SessionManager state/actions/reducers/effects)
**JavaScript:** 5 JS interop modules (sessionPersistence.js, layoutPersistence.js, etc.)

### Server-Side Implementation

**Agents:** ~10 agent wrappers (Multimodal, HITL, Predictive, Shared State, etc.)
**Services:** 5+ backend services (Weather, Email, Document, etc.)
**Tools:** 8 tools (Chart, DataGrid, Document, DynamicForm, Email, Weather, etc.)
**Middleware:** 2 streaming chat clients (ToolResult, ContextWindow)
**Endpoints:** 5 endpoint groups (Auth, Email, FileUpload, Title, Weather)

### Tests

**Server:** 2 files, 3 tests total  
**Client:** 0 files  
**JavaScript:** 0 files  
**Total Coverage:** < 1% (estimate)

---

## Conclusion

AGUIDojo has **solid infrastructure for v3 validation** but **critical test gaps**:

- ✅ Session persistence is 80% implemented (L1+L2 + hydration)
- ✅ SSE streaming is 70% implemented (backpressure, metrics, context trimming)
- ⚠️ Autonomy controls are 60% implemented (3 levels, risk rules, approval UI)
- ⚠️ Multimodal attachments are 40% implemented (server resolution, models)
- ❌ **NO TESTS** exist for any of these four areas

**Next steps:** Implement xUnit tests for core logic, consider bUnit for Blazor components and Jest for JS interop.

