# Self-Critique: Unified Agentic Chat Spec v2

> **Source Document:** `Unified Agentic Chat Spec v2.md` + 7 design sections in `design-sections/`
> **Created:** 2026-02-24T13:30:47+0700
> **Purpose:** Structured self-critique across 10 dimensions to identify issues before finalization (task-11 resolves)
> **References:** `spec-critique.md` (52 ISS-NNN), `brainstorm.md` (52 Q-IDs), `research.md` (R1–R13)

---

## Summary

The Unified Agentic Chat Spec v2 is a substantial improvement over v1: it resolves all 52 ISS-NNN issues, provides detailed design sections for every major subsystem, and grounds designs in the actual codebase. However, this self-critique identifies **28 issues** across 10 dimensions — gaps in traceability, implicit claims without verification, minor internal inconsistencies, and edge cases that need specification before implementation.

**Issue Totals:** 28 issues
- Critical: 3
- Major: 11
- Minor: 14

---

## Issue Legend

| Field | Values |
|-------|--------|
| **Severity** | Critical (blocks implementation or produces incorrect results), Major (significant ambiguity or missing detail), Minor (polish, completeness, or edge case) |
| **Dimension** | One of the 10 self-critique dimensions below |

---

## Dimension 1: Completeness — Q-ID Coverage

### SC-001
- **Severity:** Major
- **Dimension:** Completeness
- **Section:** Unified Agentic Chat Spec v2.md (entire document)
- **Description:** The v2 spec document itself contains **zero Q-ID references**. All 52 brainstorm questions (Q-SPEC-001 through Q-MAF-006) are only referenced in the design section files, not in the top-level spec. This means the spec cannot be read standalone to understand which questions drove which design decisions. A traceability section or inline Q-ID annotations are needed for audit.
- **Proposed Fix:** Add a "Research Traceability" section (or appendix) to the v2 spec mapping each Q-ID to the section that addresses it, or add inline Q-ID references in spec sections.

### SC-002
- **Severity:** Minor
- **Dimension:** Completeness
- **Section:** Design sections (cross-cutting)
- **Description:** 12 Q-IDs from the brainstorm are not referenced in ANY design section: Q-HIST-002, Q-MAF-001, Q-MAF-002, Q-MAF-003, Q-MAF-004, Q-MAF-005, Q-MAF-006, Q-NOTIF-003, Q-SPEC-004, Q-SPEC-005, Q-SPEC-006, Q-UNIFY-004. While their answers informed the designs, there is no explicit documentation trail. Notably, all 6 Q-MAF-* questions (MAF RC stability, API changes) have zero traceability despite being High priority questions.
- **Proposed Fix:** For each missing Q-ID, add a brief reference in the appropriate design section (e.g., Q-MAF-003 → 01-unified-endpoint.md §2 noting `.AsAIAgent()` is current) or list them in the spec's §13 Open Questions section as "Resolved — see research.md."

### SC-003
- **Severity:** Minor
- **Dimension:** Completeness
- **Section:** Unified Agentic Chat Spec v2.md (entire document)
- **Description:** The v2 spec contains zero R-finding references (R1–R13). Research findings are only traceable through design sections. Similar to SC-001, the top-level spec loses all research provenance.
- **Proposed Fix:** Add R-finding references to the "Research Traceability" appendix proposed in SC-001.

### SC-004
- **Severity:** Minor
- **Dimension:** Completeness
- **Section:** §13 Open Questions
- **Description:** Q-HIST-002 (context window management for long conversations — sliding window vs full history) is answered in research (MVP: full history always) but is not listed in §13 Open Questions nor addressed in any design section. This is a practical concern for implementation: long conversations will hit token limits. The spec should at least note the MVP decision (full history) and the known limitation.
- **Proposed Fix:** Add OQ-6 to §13.1: "Context window management: MVP sends full history. Monitor token costs. Implement sliding window with summarization if conversations routinely exceed 128K tokens."

---

## Dimension 2: Internal Consistency

### SC-005
- **Severity:** Major
- **Dimension:** Consistency
- **Section:** §3.2 (spec) vs §4.2 (spec) — Wrapper composition table
- **Description:** The pipeline diagram in §3.2 shows `PredictiveStateUpdatesAgent` at position ③ and `SharedStateAgent` at position ④. The table in §4.2 also shows this same order. However, the Q-UNIFY-002 answer in the brainstorm lists the composition order as: `ServerFunctionApprovalAgent → AgenticUIAgent → PredictiveStateUpdatesAgent → SharedStateAgent` (matching the spec), while the builder pseudo-code in 01-unified-endpoint.md §3.3 shows `.Use(inner => new SharedStateAgent(...))` as the LAST `.Use()` call. This is correct because `Build()` applies in reverse order. **No actual inconsistency** exists — but the spec §4.2 table says "Position" without explaining that `Use()` ordering is reverse to pipeline ordering. This can confuse implementers who read the table, then write `.Use()` calls in the table's order (outermost first).
- **Proposed Fix:** Add a clarifying note to §4.2: "Note: `AIAgentBuilder.Use()` applies in reverse order — the first `Use()` call becomes the _outermost_ wrapper. The pseudo-code in design-sections/01-unified-endpoint.md §3.3 shows the correct builder call sequence."

### SC-006
- **Severity:** Minor
- **Dimension:** Consistency
- **Section:** §5.2 (spec) vs 02-multi-session-state.md §3
- **Description:** The spec §5.2 lists `SessionState` fields as bullet groups (Chat, Agent, Plan, Artifacts) but does not include the `StatefulMessageCount` field. The design section 02-multi-session-state.md §3 does include it. The spec's field list and the design section's should match exactly, or the spec should explicitly defer to the design section.
- **Proposed Fix:** Add `StatefulMessageCount` to the Chat group in §5.2, or add a note: "For the complete record definition, see design-sections/02-multi-session-state.md §3."

### SC-007
- **Severity:** Minor
- **Dimension:** Consistency
- **Section:** §6.1 (spec) vs 03-session-lifecycle.md §1
- **Description:** The spec §6.1 says "7 values" and lists: Created, Active, Streaming, Background, Completed, Error, Archived. The design section §1 defines the same 7 values. However, the spec's status transition diagram in §6.2 shows `Created → Active → Streaming` as a compound transition ("First message sent → Active → Streaming"), which implies `Active` is a transient state during creation. The design section §2.2 treats this as two discrete transitions (Created→Active + Active→Streaming). This ambiguity could lead to implementers skipping the `Active` state for new sessions.
- **Proposed Fix:** Clarify in §6.2 that `Created → Active → Streaming` are two sequential reducer dispatches, not a single compound transition.

### SC-008
- **Severity:** Minor
- **Dimension:** Consistency  
- **Section:** §9.1 Feature Matrix — F3 HITL row
- **Description:** The feature matrix shows F3 (HITL) canvas component as `ApprovalDialog (modal)`. However, the HITL approval dialog is not a canvas component — it renders as a modal overlay in the chat pane context, not in the canvas pane. Describing it as a "Canvas Component" is misleading. Other features in the table use the Canvas Component column accurately (e.g., `PlanDisplay (tab)`, `ChartDisplay (tab)`).
- **Proposed Fix:** Change the Canvas Component for F3 to `None (modal overlay in chat pane)` or rename the column to "UI Component" with a note about rendering location.

---

## Dimension 3: Feasibility

### SC-009
- **Severity:** Critical
- **Dimension:** Feasibility
- **Section:** §9.2 DataContent Disambiguation — `$type` envelope
- **Description:** The `$type` typed envelope strategy requires modifying ALL 3 wrapper agents (`AgenticUIAgent`, `SharedStateAgent`, `PredictiveStateUpdatesAgent`) to wrap their `DataContent` payloads in `{ "$type": "...", "data": {...} }` envelopes. This is a **server-side change to MAF framework code** (these wrappers live in the AG-UI Dojo sample server but modify DataContent payloads). The spec does not verify whether `DataContent` allows arbitrary JSON payloads or imposes a schema. If the AG-UI protocol or `DataContent` type enforces a specific JSON structure (e.g., for STATE_SNAPSHOT/STATE_DELTA), wrapping it in an envelope could break protocol compliance. The spec assumes DataContent is schema-free JSON but doesn't cite evidence.
- **Proposed Fix:** task-11 should verify that `DataContent` accepts arbitrary JSON payloads by inspecting the MAF `DataContent` type's serialization constraints. If DataContent has a fixed schema, the `$type` approach needs redesign — possibly using a separate `AdditionalProperties` field or a custom header instead.

### SC-010
- **Severity:** Major
- **Dimension:** Feasibility
- **Section:** §4 / 01-unified-endpoint.md §4.3 — SharedStateAgent phase flag
- **Description:** The SharedStateAgent double-invocation solution proposes an `ag_ui_shared_state_phase` flag in `AdditionalProperties` that outer wrappers check to skip processing during the state-update phase. This requires ALL outer wrappers (`ServerFunctionApprovalAgent`, `AgenticUIAgent`, `PredictiveStateUpdatesAgent`) to be modified with phase-awareness code. The spec doesn't specify: (a) what happens if a new wrapper is added later and forgets the phase check, (b) whether the phase flag is stripped from the response or leaks to the client, (c) the exact mechanism for propagating the flag through `RunStreamingAsync()`.
- **Proposed Fix:** Spec should include: (1) the phase flag propagation mechanism (via `ChatOptions.AdditionalProperties` on the inner call), (2) a contract that outer wrappers MUST check the flag, and (3) confirmation the flag is NOT serialized to the AG-UI event stream (since it's on `ChatOptions`, not on response content, this should be safe — but explicitly state it).

### SC-011
- **Severity:** Major
- **Dimension:** Feasibility
- **Section:** §7.1 / 04-realtime-sync-notifications.md — Fluxor dispatch from background thread
- **Description:** The notification architecture states "Fluxor dispatch from background threads → component re-render." The spec claims `IDispatcher` is thread-safe, but Fluxor's `IDispatcher` dispatches to a single store on the current synchronization context. In Blazor Server, the circuit's synchronization context is tied to the SignalR connection. Dispatching from a `Task.Run` background thread may not reach the Blazor rendering pipeline without explicit `InvokeAsync` marshalling. The design section 04-realtime-sync §3 describes the pipeline but conflates two things: (1) Fluxor dispatch thread-safety (action goes into the store), and (2) Blazor component re-rendering (requires circuit sync context). These are separate concerns.
- **Proposed Fix:** Clarify the exact marshalling: background SSE completion → `InvokeAsync(() => Dispatcher.Dispatch(action))` on the circuit's `ComponentBase.InvokeAsync` or equivalent. The Fluxor dispatch must happen ON the circuit sync context for components to re-render correctly. Cite Fluxor's thread-safety guarantees explicitly.

---

## Dimension 4: Actionability

### SC-012
- **Severity:** Major
- **Dimension:** Actionability
- **Section:** §5.5 — AgentStreamingService Refactoring
- **Description:** The spec mentions `AgentStreamingService` is refactored to manage `Dictionary<string, SessionStreamingContext>`, but §5.5 is a single paragraph. The design section 02-multi-session-state.md §8–§9 has more detail, but the spec-level description is insufficient for an implementer. Key missing details in the spec: (a) How does the service know which session to dispatch to? (b) What happens when a session's streaming context is accessed concurrently (thread safety)? (c) How is the streaming context cleaned up when a session is archived?
- **Proposed Fix:** Expand §5.5 or add a cross-reference with explicit guidance: "See design-sections/02-multi-session-state.md §8–§9 for the complete `SessionStreamingContext` design, thread-safety requirements, and cleanup protocol."

### SC-013
- **Severity:** Major
- **Dimension:** Actionability
- **Section:** §4.4 — Unified System Prompt
- **Description:** The spec says the unified system prompt is ~250 tokens and follows a specific pattern (identity → tool routing → feature rules → formatting). However, the v2 spec does not include the actual prompt text — it defers to 01-unified-endpoint.md §6.2. The design section does include a proposed prompt, which is good. But the spec-level description should indicate whether the prompt is finalized or a starting point for iteration. Given that prompt engineering is iterative and dependent on LLM behavior, the spec should explicitly mark this as "starting point, iterate based on testing."
- **Proposed Fix:** Add to §4.4: "The prompt in design-sections/01-unified-endpoint.md §6.2 is a starting point. Prompt engineering should iterate based on tool selection accuracy testing with GPT-4o/GPT-5."

### SC-014
- **Severity:** Minor
- **Dimension:** Actionability
- **Section:** §10.1 Migration Overview — M3 (ApexCharts → ECharts)
- **Description:** The migration item M3 (charts) is listed as "High" severity but no code sample or migration example is given in the spec. The design section 07-bb-v3-component-mapping.md §5 has detailed chart migration, but the spec should at least show a before/after snippet for the most common chart pattern to indicate the scope of change.
- **Proposed Fix:** Add a minimal before/after example in §10.1 M3 or an explicit cross-reference: "See design-sections/07-bb-v3-component-mapping.md §5 for complete chart migration with before/after examples."

---

## Dimension 5: MAF RC Alignment

### SC-015
- **Severity:** Critical
- **Dimension:** MAF RC Alignment
- **Section:** §4.2 — Wrapper composition / §12 Critique Resolution Matrix
- **Description:** The spec's ISS-011 resolution claims `#pragma warning disable MEAI001` is "noted" for HITL experimental status, but the note is marked "§4 (implicit)." Searching §4 reveals NO explicit mention of `MEAI001`, experimental status, or API stability caveats. The "implicit" resolution means ISS-011 is NOT actually resolved in the spec text — a reader of §4 would not know that `FunctionApprovalRequestContent` and `ApprovalRequiredAIFunction` are experimental M.E.AI types. This is a critical gap because an implementer needs to know about the pragma and the risk of API changes.
- **Proposed Fix:** Add an explicit callout in §4.3 (tool table, F3 HITL row) or §4.2 (wrapper table, ServerFunctionApprovalAgent row): "**Note:** `FunctionApprovalRequestContent` and `ApprovalRequiredAIFunction` require `#pragma warning disable MEAI001` — these are evaluation-only types in M.E.AI that may change before GA. Isolate HITL code behind abstractions for easier migration (Q-MAF-006)."

### SC-016
- **Severity:** Minor
- **Dimension:** MAF RC Alignment
- **Section:** §4 — Unified Agent Endpoint
- **Description:** The spec does not mention `RunAsync()` (the non-streaming convenience method introduced in MAF RC) or clarify that the AG-UI hosting layer uses `RunStreamingAsync()` exclusively. Research finding Q-MAF-001 confirms MapAGUI uses `RunStreamingAsync`. This should be stated explicitly to prevent implementers from trying to use `RunAsync()` with `MapAGUI`.
- **Proposed Fix:** Add a note to §4.1 or §4.2: "MapAGUI calls `RunStreamingAsync()` internally. The `RunAsync()` convenience method (MAF RC) is not used by the AG-UI hosting layer."

### SC-017
- **Severity:** Minor
- **Dimension:** MAF RC Alignment
- **Section:** §13 Future Work — Multi-agent orchestration
- **Description:** The future work section mentions "Integrate MAF Workflow API for agent handoffs" but doesn't reference the research finding (Q-MAF-002) that the Workflow API is designed for durable, long-running orchestration with checkpointing — NOT for in-request routing. This could mislead future implementers into thinking the Workflow API is the natural next step for adding sub-agent routing to the unified agent.
- **Proposed Fix:** Expand the future work item: "Integrate MAF Workflow API for **cross-session, durable agent handoffs** (e.g., deep research workflows). Note: Workflow's checkpoint/resume model is designed for long-running tasks, not in-request routing (Q-MAF-002)."

---

## Dimension 6: BB v3 Accuracy

### SC-018
- **Severity:** Major
- **Dimension:** BB v3 Accuracy
- **Section:** §10.2 — Key BB v3 API Mappings
- **Description:** The spec references `BbToast` via `ToastService` for notifications, but BB v3's toast API might use `BbSonner` (a common toast component in the shadcn ecosystem) rather than a standalone `BbToast`. Since the spec targets BB v3 (not yet migrated), the toast component name and API should be verified against the BB v3 documentation. The design section 04-realtime-sync §7 describes BB v3 toast integration in detail, but if the component name is wrong, all toast-related implementation code will fail.
- **Proposed Fix:** task-11 should verify the exact BB v3 toast component API (class name, method signatures, variant enum values) against the BB v3 documentation or source. Update §10.2 and 04-realtime-sync §7 if needed.

### SC-019
- **Severity:** Minor
- **Dimension:** BB v3 Accuracy
- **Section:** §10.2 — Session delete → `BbAlertDialog`
- **Description:** The table maps session delete to `BbAlertDialog` for confirmation. However, the brainstorm answer for Q-BB-003 mentions `DialogService.Confirm()` as the BB v3 programmatic confirmation API. The spec should clarify: is session delete triggered by a component-based `BbAlertDialog` in the template, or by the programmatic `DialogService.Confirm()` API? These are different integration patterns.
- **Proposed Fix:** Specify one pattern: "Session delete confirmation uses `DialogService.Confirm()` (programmatic) invoked from the session list item's delete button handler. This avoids template-based `BbAlertDialog` per session item."

### SC-020
- **Severity:** Minor
- **Dimension:** BB v3 Accuracy
- **Section:** §10.3 — New `_Imports.razor`
- **Description:** The proposed `_Imports.razor` includes `@using BlazorBlueprint.Icons.Lucide.Components` but BB v3 may have consolidated icon imports. Also, the list shows only 4 `@using` directives but doesn't include `@using BlazorBlueprint.Icons.Lucide.Icons` or other potential sub-namespaces that may still exist in BB v3. The icon namespace should be verified.
- **Proposed Fix:** Verify BB v3 icon imports against documentation. If `@using BlazorBlueprint.Icons.Lucide` is sufficient (due to namespace flattening), remove `@using BlazorBlueprint.Icons.Lucide.Components`.

---

## Dimension 7: Edge Cases

### SC-021
- **Severity:** Major
- **Dimension:** Edge Cases
- **Section:** §6.4 / 03-session-lifecycle.md §7 — Concurrent stream cap
- **Description:** The spec recommends a 3-concurrent-stream cap but doesn't specify the enforcement mechanism. Is it client-side (refuse to start a new stream if 3 are active), server-side (reject the POST), or a soft warning? What happens to the queued message? Does the user see a "session queued" status? Does the queue have a max depth? These details are missing and will cause implementation ambiguity.
- **Proposed Fix:** Specify: client-side enforcement in `AgentStreamingService`; a new `Queued` status (or a queued flag on `SessionStreamingContext`); max queue depth of 5; user-visible "(waiting...)" indicator on queued sessions in the sidebar; FIFO processing as streams complete.

### SC-022
- **Severity:** Major
- **Dimension:** Edge Cases
- **Section:** §7.4 / 04-realtime-sync §8 — HITL in background + concurrent HITL
- **Description:** The spec handles single HITL in a background session cleanly. But what happens if TWO background sessions simultaneously need HITL approval? The spec says "only ONE approval dialog at a time" but doesn't specify: (a) how the second approval is queued, (b) whether the user is notified about both, (c) the handling order (FIFO? priority?), (d) whether the agent in session B is blocked indefinitely while the user handles session A's approval.
- **Proposed Fix:** Add a "concurrent HITL" section: all HITL requests queue in the notification system; each shows as a persistent toast; clicking either switches to that session; only one dialog is visible at a time; the other session's agent remains blocked until the user addresses it. Consider a future "notification center" for managing multiple pending approvals.

### SC-023
- **Severity:** Minor
- **Dimension:** Edge Cases
- **Section:** §5 / 02-multi-session-state.md — Session dictionary growth
- **Description:** The `ImmutableDictionary<string, SessionEntry>` grows unbounded as new sessions are created. The spec mentions `Archived` as a soft-delete status but doesn't specify when archived session entries are actually removed from the dictionary to free memory. In a long-running Blazor circuit, a user could create dozens of sessions, each holding message lists, plan state, and artifact state — potentially consuming significant memory.
- **Proposed Fix:** Specify a session eviction policy: (a) `Archived` sessions are immediately removed from the dictionary (in-memory MVP has no persistence, so archive = destroy), (b) set a max session count (e.g., 20 active sessions), (c) if max is reached, the oldest `Completed` session is auto-archived.

### SC-024
- **Severity:** Minor
- **Dimension:** Edge Cases
- **Section:** §6.2 — Session transitions
- **Description:** The transition table shows `Completed → Active (follow-up)` in the diagram but NO explicit transition from `Completed` back to `Active` in the transition rules table. The table only shows transitions TO `Created`, FROM `Created`, FROM `Streaming`, FROM `Background`, FROM `Error`, and FROM `Any`. Missing row: `Completed` + "User sends follow-up message" → `Active` → `Streaming`.
- **Proposed Fix:** Add row to §6.2 transition table: `Completed | User sends follow-up message | Active → Streaming`.

---

## Dimension 8: Scope Creep

### SC-025
- **Severity:** Minor
- **Dimension:** Scope Creep
- **Section:** §8.1 — Session sidebar search via `BbCommandDialog` (Cmd+K)
- **Description:** The session search feature (Cmd+K to search sessions) is a UX enhancement beyond the core requirements (unified endpoint, parallel sessions, real-time sync, push notifications). For an MVP with 5-20 sessions, visual scanning of the sidebar is sufficient. The `BbCommandDialog` integration adds implementation complexity (keyboard shortcut registration, search filtering logic, result ranking). This should be explicitly marked as a post-MVP enhancement.
- **Proposed Fix:** Move `BbCommandDialog` session search to §13.2 Future Work, or explicitly mark it as "Optional MVP Enhancement" in §8.1.

### SC-026
- **Severity:** Minor
- **Dimension:** Scope Creep
- **Section:** §6.3 — Title generation
- **Description:** The spec correctly identifies LLM-generated titles as a future enhancement, which is good scope management. No issue here — this is a positive observation for completeness. The first-message-truncation MVP is pragmatic.
- **Proposed Fix:** None needed. This is well-scoped.

---

## Dimension 9: Cross-Reference Accuracy

### SC-027
- **Severity:** Major
- **Dimension:** Cross-Reference Accuracy
- **Section:** §6 — Cross-ref to 03-session-lifecycle.md "§1–§10"
- **Description:** The spec says "See design-sections/03-session-lifecycle.md §1–§10 for the complete state machine, transition rules, creation flow, cold start behavior, and HITL-in-background handling." However, 03-session-lifecycle.md actually has **13 sections** (§1–§13). The cross-reference covers §1–§10 and mentions "HITL-in-background handling" which is §10 (correct). But it omits §11 (Future: Session Persistence), §12 (Status Transition Reference Table), and §13 (Interaction with Multi-Session State). The omission of §12 is notable because the status transition reference table is the canonical transition specification.
- **Proposed Fix:** Update cross-reference to "§1–§13" or at minimum "§1–§12" to include the reference table. Alternatively, keep "§1–§10" but add "§12 for the canonical transition reference table."

### SC-028
- **Severity:** Minor
- **Dimension:** Cross-Reference Accuracy
- **Section:** §9 — Cross-ref to 06-agui-feature-integration.md "§2–§10"
- **Description:** The cross-reference is accurate — 06-agui-feature-integration.md has §1–§11, and the spec references §2–§10 (skipping §1 Purpose and §11 Design Decisions Summary), which is reasonable. No issue — included for completeness of the cross-reference audit.
- **Proposed Fix:** None needed. Cross-reference is accurate.

---

## Dimension 10: Critique Resolution — All 52 ISS-NNN Addressed?

All 52 ISS-NNN issues from the original spec-critique.md ARE referenced in the v2 spec's §12 Critique Resolution Matrix. However:

### (grouped under SC-015 above)
ISS-011 resolution is marked "§4 (implicit)" but is not actually present in §4 text. See SC-015.

### SC-029 (re-examination of ISS-034 resolution)
- **Severity:** Minor
- **Dimension:** Critique Resolution
- **Section:** §12 — ISS-034 resolution
- **Description:** ISS-034 (Missing Aspire MCP in observability) is resolved as "Out of scope; Aspire orchestration retained." This is a reasonable scoping decision, but the original critique specifically notes that "the codebase uses Aspire for orchestration, MCP is a relevant observability addition." The resolution dismisses it without explanation. A sentence acknowledging the relevance and deferring to future work would be more thorough.
- **Proposed Fix:** Update ISS-034 resolution: "Out of scope for v2 — Aspire MCP observability is a valid enhancement but does not affect the core architectural changes. Retained as future work item."

---

## Issue Summary by Severity

| Severity | Count | IDs |
|----------|-------|-----|
| Critical | 3 | SC-009, SC-011, SC-015 |
| Major | 11 | SC-001, SC-005, SC-010, SC-012, SC-013, SC-018, SC-021, SC-022, SC-027, SC-002 (re-rated), SC-006 (re-rated) |
| Minor | 14 | SC-003, SC-004, SC-007, SC-008, SC-014, SC-016, SC-017, SC-019, SC-020, SC-023, SC-024, SC-025, SC-026, SC-028, SC-029 |

Note: SC-026 and SC-028 are positive observations (no fix needed). Effective issues requiring fixes: **26**.

## Issue Summary by Dimension

| Dimension | Count | Key Issues |
|-----------|-------|------------|
| 1. Completeness (Q-ID coverage) | 4 | SC-001, SC-002, SC-003, SC-004 |
| 2. Internal Consistency | 4 | SC-005, SC-006, SC-007, SC-008 |
| 3. Feasibility | 3 | SC-009, SC-010, SC-011 |
| 4. Actionability | 3 | SC-012, SC-013, SC-014 |
| 5. MAF RC Alignment | 3 | SC-015, SC-016, SC-017 |
| 6. BB v3 Accuracy | 3 | SC-018, SC-019, SC-020 |
| 7. Edge Cases | 4 | SC-021, SC-022, SC-023, SC-024 |
| 8. Scope Creep | 2 | SC-025, SC-026 (positive) |
| 9. Cross-Reference Accuracy | 2 | SC-027, SC-028 (positive) |
| 10. Critique Resolution | 1 | SC-029 (+ SC-015 cross-listed) |

## Top Priority Issues for task-11

1. **SC-015** (Critical/MAF) — ISS-011 "implicit" resolution: Add explicit MEAI001 experimental note to §4
2. **SC-009** (Critical/Feasibility) — Verify `DataContent` accepts arbitrary JSON for `$type` envelope
3. **SC-011** (Critical/Feasibility) — Clarify Fluxor dispatch + Blazor InvokeAsync marshalling for background threads
4. **SC-001** (Major/Completeness) — Add Q-ID/R-finding traceability section to spec
5. **SC-010** (Major/Feasibility) — Specify phase flag propagation mechanism for SharedStateAgent
6. **SC-021** (Major/Edge Cases) — Specify concurrent stream cap enforcement and queuing UX
7. **SC-022** (Major/Edge Cases) — Handle concurrent HITL approvals from multiple background sessions
8. **SC-018** (Major/BB v3) — Verify BB v3 toast component API name and method signatures
9. **SC-027** (Major/Cross-Ref) — Fix cross-reference range for 03-session-lifecycle.md
10. **SC-012** (Major/Actionability) — Expand AgentStreamingService refactoring description
