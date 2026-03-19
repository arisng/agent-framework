# AGUIDojo v3 Validation — Quick Reference

Updated: 2026-03-19

## Verified test state
- `AGUIDojoClient.Tests` → **41 passed**
- `AGUIDojoServer.Tests` → **16 passed**
- **Total:** **57 passed**

---

## Current status matrix

| Area | Current state | Targeted tests now in place | Real gaps still open |
| --- | --- | --- | --- |
| Session persistence | Implemented and partly validated | `ConversationTreeTests`, `SessionPersistenceServiceTests` | Hydration/reconnect tests; JS/browser persistence tests; layout persistence tests |
| Multimodal attachments | Server flow validated | `MultimodalAttachmentAgentTests` | Client upload UX tests; full multimodal E2E; explicit attachment-count cap validation |
| Streaming / SSE | Hardened and partly validated | `AgentStreamingServiceTests` | `Last-Event-ID` replay; latency benchmarks; middleware-specific tests |
| Autonomy / governance | Implemented and broadly targeted | `AutonomyPolicyServiceTests`, `RiskAssessmentServiceTests`, `AutonomySelectorTests`, `ApprovalQueueTests`, undo tests | Audit trail tests; full streaming approval lifecycle tests; deeper confidence-focused tests |

---

## Completed waves

| Wave | Commit | Outcome |
| --- | --- | --- |
| Client test scaffold | `6681d4b4` | Added `AGUIDojoClient.Tests` |
| Persistence + autonomy coverage | `b364547d` | Added DAG, persistence, policy, risk, selector, and approval queue coverage |
| Multimodal coverage | `9be787d6` | Added server multimodal attachment validation |
| SSE hardening | `71e0ea8d` | Added queue/reconnect hardening and streaming tests |
| Undo grace period | `5d319c1f` | Added undo grace period implementation + tests |

---

## What is no longer true

Remove these stale assumptions from review notes:
- ❌ “There are 0 tests”
- ❌ “There are only 2 server test files”
- ❌ “Confidence visualization is missing”
- ❌ “Undo grace period is missing”
- ❌ “`ShouldAutoDecide` is still private”

Current reality:
- ✅ There is a client test project with xUnit + bUnit coverage
- ✅ Server tests now include multimodal coverage in a third test file
- ✅ Confidence metadata + UI indicator exist
- ✅ Undo grace period exists and is covered
- ✅ Auto-decision policy is exposed through `AutonomyPolicyService`

---

## Targeted coverage inventory

### Client tests now present
- `Models/ConversationTreeTests.cs`
- `Services/SessionPersistenceServiceTests.cs`
- `Services/AgentStreamingServiceTests.cs`
- `Services/RiskAssessmentServiceTests.cs`
- `Services/AutonomyPolicyServiceTests.cs`
- `Components/Governance/AutonomySelectorTests.cs`
- `Components/Governance/ApprovalQueueTests.cs`
- `Components/Governance/UndoGracePeriodToastTests.cs`
- `Store/SessionManager/UndoGracePeriodReducerTests.cs`

### Server tests now present
- `BasicTests.cs`
- `SharedStateAgentTests.cs`
- `MultimodalAttachmentAgentTests.cs`

---

## Most important remaining work

1. Add reconnect/hydration validation for persisted sessions
2. Add client attachment UI tests around `ChatInput`
3. Validate streaming replay/`Last-Event-ID` behavior if required for plan compliance
4. Add audit trail and full approval-lifecycle coverage
5. Add telemetry/latency assertions if operational guarantees matter

---

## Fast facts

- **bUnit present:** yes
- **Client test project present:** yes
- **Confidence visualization present:** yes
- **Undo grace period present:** yes
- **`ShouldAutoDecide` still private:** no
