# AGUIDojo v3 Validation Index

Generated: 2026-03-19  
Scope: `/dotnet/samples/05-end-to-end/AGUIDojo`

---

## Audit Summary

This index reflects the completed validation follow-up work from the session audit, not the earlier pre-test snapshot.

### Authoritative completed session work
- `6681d4b4` — `chore(aguidojo-client): add client test scaffold`
- `9be787d6` — `test(aguidojo-tests): add multimodal attachment coverage`
- `71e0ea8d` — `fix(aguidojo): harden sse reconnect flow`
- `5d319c1f` — `feat(aguidojo-governance): add undo grace period`
- `b364547d` — `test(aguidojo-client): add persistence and autonomy coverage`

### Verified test state
- `dotnet test --project AGUIDojoClient.Tests/AGUIDojoClient.Tests.csproj` → **41 passed**
- `dotnet test --project AGUIDojoServer.Tests/AGUIDojoServer.Tests.csproj` → **16 passed**
- **Total validated tests:** **57 passed**

---

## Documents in This Analysis

### 1. `AGUIDOJO_V3_VALIDATION_MAP.md`
Comprehensive mapping of implementation files, current test coverage, completed validation waves, and remaining gaps.

**Use this when you need:** exact file/test references, area-by-area status, or gap analysis tied to the v3 plan.

### 2. `AGUIDOJO_V3_VALIDATION_QUICK_REFERENCE.md`
Fast lookup sheet for current coverage, completed waves, and open items that are still real.

**Use this when you need:** a concise status view before coding or review.

---

## Current Validation Status by Area

| Area | Current status | Coverage now in place | Real gaps still open |
| --- | --- | --- | --- |
| Session persistence & DAG | Implemented with targeted client coverage | `ConversationTreeTests`, `SessionPersistenceServiceTests` | No hydration/circuit-reconnect tests; no JS/browser persistence tests; no layout persistence tests |
| Multimodal attachments | Server-side flow implemented and covered | `MultimodalAttachmentAgentTests` validates marker stripping, missing-file handling, upload validation, placeholder serving | No client attachment UI/component tests; no end-to-end vision flow; no explicit 5-attachment cap validation |
| Streaming / SSE | Core flow implemented and hardened | `AgentStreamingServiceTests` covers queue saturation, queued-session promotion, and duplicate in-flight request handling | No first-token benchmark tests; no Last-Event-ID replay support; no explicit .NET 10 native SSE migration validation |
| Autonomy / governance | Implemented with targeted client coverage | `AutonomyPolicyServiceTests`, `RiskAssessmentServiceTests`, `AutonomySelectorTests`, `ApprovalQueueTests`, undo grace period tests | No end-to-end approval lifecycle tests through streaming; no audit trail rendering tests; confidence path is implemented but not deeply test-focused |

---

## Completed Validation Waves

### Wave 1 — Client test foundation ✅
**Commit:** `6681d4b4`
- Added `AGUIDojoClient.Tests`
- Introduced client-side xUnit + bUnit coverage surface
- Closed the stale “client tests are missing entirely” claim

### Wave 2 — Persistence + autonomy coverage ✅
**Commit:** `b364547d`
- Added tests for `ConversationTree`
- Added tests for `SessionPersistenceService`
- Added tests for `RiskAssessmentService`
- Added policy-matrix coverage for `AutonomyPolicyService`
- Added component tests for `AutonomySelector` and `ApprovalQueue`
- Closed the stale claim that `ShouldAutoDecide` was still private inside `AgentStreamingService`

### Wave 3 — Multimodal/server validation ✅
**Commit:** `9be787d6`
- Added multimodal attachment tests on the server
- Covered marker stripping, binary resolution, missing attachments, upload content-type validation, size validation, and placeholder file serving
- Closed the stale “0 multimodal tests” and “file storage implementation unclear” claims

### Wave 4 — SSE hardening ✅
**Commit:** `71e0ea8d`
- Hardened reconnect/queue handling in the streaming loop
- Added coverage for queue limits and queued-session promotion behavior
- Closed the stale claim that the area had zero streaming tests
- **Still open:** standards-based replay via `Last-Event-ID` remains a separate gap

### Wave 5 — Undo grace period ✅
**Commit:** `5d319c1f`
- Implemented the undo grace period UX/state flow
- Added reducer and toast component coverage
- Closed the stale “undo grace period missing” claim

---

## Key Corrections vs. Older Validation Notes

These older claims are now stale and should no longer be used:
- “0 tests”
- “Only 2 server test files”
- “Confidence visualization missing”
- “Undo grace period missing”
- “`ShouldAutoDecide` is still private”

Current reality:
- There is a dedicated **client** test project.
- Server tests include **3** files, not 2.
- Confidence metadata/pill rendering is implemented.
- Undo grace period is implemented and covered.
- Auto-decision policy lives in `AutonomyPolicyService` and is directly testable.

---

## Remaining Real Gaps

### Highest-value remaining gaps
1. **Persistence hydration coverage**
   - No explicit test for restoring state on circuit reconnect.
2. **Client attachment UX coverage**
   - `ChatInput` upload/pending-preview behavior is implemented but still untested here.
3. **SSE standards alignment**
   - Reconnect logic is stronger, but `Last-Event-ID` replay is still not documented as complete.
4. **Governance end-to-end coverage**
   - Approval queue pieces are tested, but the full streamed approval lifecycle still lacks deeper validation.
5. **Observability / latency validation**
   - Metrics objects exist, but latency targets and telemetry assertions remain open.

---

## Quick Facts

- **Client test project:** `AGUIDojoClient.Tests`
- **Client test files:** 9
- **Server test files:** 3
- **Verified test totals:** 41 client + 16 server = 57 passing
- **bUnit usage:** now present in the sample
- **Confidence UI:** implemented
- **Undo grace period:** implemented
- **Auto-decision policy:** extracted into `AutonomyPolicyService`

---

## Related Documents

- `/dotnet/samples/05-end-to-end/AGUIDojo/.docs/implementation-plan-v3/AGUIDOJO_V3_VALIDATION_MAP.md`
- `/dotnet/samples/05-end-to-end/AGUIDojo/.docs/implementation-plan-v3/AGUIDOJO_V3_VALIDATION_QUICK_REFERENCE.md`
- `/dotnet/samples/05-end-to-end/AGUIDojo/.docs/implementation-plan-v3/unified-agentic-chat-implementation-plan-v3.md`
