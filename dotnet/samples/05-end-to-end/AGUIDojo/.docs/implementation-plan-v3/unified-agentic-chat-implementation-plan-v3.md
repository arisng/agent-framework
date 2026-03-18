# Research Report: V3 Upgrade Opportunities for Unified Agentic Chat Implementation Plan

> **Date:** 2026-03-17  
> **Scope:** AGUIDojo Unified Agentic Chat — researching what v3 should add beyond v2  
> **Source Document:** `dotnet/samples/05-end-to-end/AGUIDojo/.docs/unified-agentic-chat-implementation-plan-v2.md`

---

## Executive Summary

The v2 implementation plan is a solid, 10-phase roadmap that addresses the five core architectural changes: unified endpoint, multi-session state, push notifications, BB v3 migration, and codebase alignment. However, v2 explicitly defers several production-grade capabilities as "Non-Goals" or "Open Questions" — and the agentic AI landscape has evolved rapidly between the v2 spec date and now. This research identifies **12 major upgrade domains** for v3, organized into three categories: **(A) Missing production-grade features** that v2 acknowledges but defers, **(B) UX/UI refinements** that would bring AGUIDojo to feature parity with leading agentic chat applications (ChatGPT, Claude, Copilot Chat), and **(C) Infrastructure upgrades** enabled by .NET 10, AG-UI protocol evolution, and MAF framework maturation.

Each domain includes a rationale, scope assessment, and relationship to v2's existing phases.

---

## Table of Contents

- [Research Report: V3 Upgrade Opportunities for Unified Agentic Chat Implementation Plan](#research-report-v3-upgrade-opportunities-for-unified-agentic-chat-implementation-plan)
  - [Executive Summary](#executive-summary)
  - [Table of Contents](#table-of-contents)
  - [1. Conversation Branching \& DAG History](#1-conversation-branching--dag-history)
    - [v2 Status](#v2-status)
    - [Why v3 Needs This](#why-v3-needs-this)
    - [Proposed v3 Design](#proposed-v3-design)
  - [2. Session Persistence \& Cross-Circuit Continuity](#2-session-persistence--cross-circuit-continuity)
    - [v2 Status](#v2-status-1)
    - [Why v3 Needs This](#why-v3-needs-this-1)
    - [Proposed v3 Design](#proposed-v3-design-1)
  - [3. Context Window Management](#3-context-window-management)
    - [v2 Status](#v2-status-2)
    - [Why v3 Needs This](#why-v3-needs-this-2)
    - [Proposed v3 Design](#proposed-v3-design-2)
  - [4. Claude Design System \& Theming](#4-claude-design-system--theming)
    - [v2 Status](#v2-status-3)
    - [Why v3 Needs This](#why-v3-needs-this-3)
    - [Proposed v3 Design](#proposed-v3-design-3)
  - [5. Message Virtualization \& Scroll Performance](#5-message-virtualization--scroll-performance)
    - [v2 Status](#v2-status-4)
    - [Why v3 Needs This](#why-v3-needs-this-4)
    - [Proposed v3 Design](#proposed-v3-design-4)
  - [6. Multimodal Attachments](#6-multimodal-attachments)
    - [v2 Status](#v2-status-5)
    - [Why v3 Needs This](#why-v3-needs-this-5)
    - [Proposed v3 Design](#proposed-v3-design-5)
  - [7. Multi-Agent Orchestration \& Visualization](#7-multi-agent-orchestration--visualization)
    - [v2 Status](#v2-status-6)
    - [Why v3 Needs This](#why-v3-needs-this-6)
    - [Proposed v3 Design](#proposed-v3-design-6)
  - [8. LLM-Generated Session Titles](#8-llm-generated-session-titles)
    - [v2 Status](#v2-status-7)
    - [Why v3 Needs This](#why-v3-needs-this-7)
    - [Proposed v3 Design](#proposed-v3-design-7)
  - [9. Accessibility (WCAG 2.2 AA)](#9-accessibility-wcag-22-aa)
    - [v2 Status](#v2-status-8)
    - [Why v3 Needs This](#why-v3-needs-this-8)
    - [Proposed v3 Design](#proposed-v3-design-8)
  - [10. Command Palette \& Keyboard-First UX](#10-command-palette--keyboard-first-ux)
    - [v2 Status](#v2-status-9)
    - [Why v3 Needs This](#why-v3-needs-this-9)
    - [Proposed v3 Design](#proposed-v3-design-9)
  - [11. Autonomy Controls \& Confidence Visualization](#11-autonomy-controls--confidence-visualization)
    - [v2 Status](#v2-status-10)
    - [Why v3 Needs This](#why-v3-needs-this-10)
    - [Proposed v3 Design](#proposed-v3-design-10)
  - [12. Streaming \& SSE Infrastructure Upgrades](#12-streaming--sse-infrastructure-upgrades)
    - [v2 Status](#v2-status-11)
    - [Why v3 Needs This](#why-v3-needs-this-11)
    - [Proposed v3 Design](#proposed-v3-design-11)
  - [13. Summary: v2 → v3 Upgrade Matrix](#13-summary-v2--v3-upgrade-matrix)
    - [Recommended v3 Phase Ordering](#recommended-v3-phase-ordering)
  - [14. Confidence Assessment](#14-confidence-assessment)
    - [Key Assumptions](#key-assumptions)
  - [15. Footnotes](#15-footnotes)

---

## 1. Conversation Branching & DAG History

### v2 Status
Explicitly listed as a **Non-Goal** in v2 §1: "Conversation branching / DAG history."[^1] The original spec critique (implementation-gap.md) identifies this as a **Major Gap**: "No Fluxor undo/redo... no conversation branching (spec §14.2): users cannot edit prior prompts to create forks."[^2]

### Why v3 Needs This
Conversation branching has moved from a niche feature to a **table-stakes expectation** in 2026. ChatGPT launched "Branching Chats" enabling users to fork any message into an alternate thread[^3]. Open-source tools like Forky implement Git-style chat management as a DAG with fork, merge, and comparison operations[^4]. LangChain/LangGraph support branching semantics where every prompt edit creates a branch in the conversation tree[^5].

### Proposed v3 Design

**Data Model Change:**
Replace `ImmutableList<ChatMessage> Messages` in `SessionState` with a DAG structure:

```
SessionState.ConversationTree
├── Nodes: ImmutableDictionary<string, ConversationNode>
│   └── ConversationNode { Id, ParentId, Message, Children, CreatedAt }
├── ActiveBranchPath: ImmutableList<string>  // path from root to current leaf
└── BranchPoints: ImmutableHashSet<string>   // nodes with >1 child
```

**Key interactions:**
- **Edit-and-regenerate**: User edits a prior message → creates a new branch from the parent node → agent runs on the new branch context
- **Branch navigation**: Arrow buttons on messages with siblings to switch between branches (ChatGPT-style `◄ 1/3 ►` navigation)
- **Branch visualization**: Optional tree view in the canvas pane showing the conversation structure as a DAG
- **Context assembly**: When sending to LLM, walk `ActiveBranchPath` from root to leaf to assemble the linear message history

**Impact on v2 Phases:**
- **Phase 4 (Fluxor)**: `SessionState.Messages` → `SessionState.ConversationTree`. Adds `BranchActions`, `BranchReducers`.
- **Phase 5 (AgentStreamingService)**: Must route streamed responses to the correct branch
- **Phase 8 (Session Sidebar)**: Branch count indicator on session items

**Effort:** L (4+ days)  
**Risk:** High — fundamental change to message storage model

---

## 2. Session Persistence & Cross-Circuit Continuity

### v2 Status
Explicitly deferred in v2 §18 OQ-1: "Session persistence (localStorage vs server) — In-memory MVP. Revisit when users need cross-circuit continuity."[^1]

### Why v3 Needs This
In v2, all session state is lost when the Blazor circuit disconnects (page refresh, navigation, SignalR timeout). For a production-grade app, this is unacceptable. The 2026 trend is toward **local-first architectures** with optional cloud sync[^6]:

- **IndexedDB** stores structured conversation histories locally, surviving page reloads and enabling offline-first operation[^7]
- **Server-side persistence** enables cross-device access via API sync[^8]
- Hybrid approaches (Dexie.js Cloud, SyncedDB) combine local speed with cloud sync[^9]

### Proposed v3 Design

**Tiered Persistence Strategy:**

| Tier | Storage | Scope | Latency | Use Case |
|------|---------|-------|---------|----------|
| **L0** | Fluxor in-memory | Per-circuit | 0ms | Active session state (v2 existing) |
| **L1** | Blazored.LocalStorage | Per-browser | <5ms | Session metadata, layout prefs, theme |
| **L2** | Browser IndexedDB (via JS interop) | Per-browser | <10ms | Full conversation history, artifacts |
| **L3** | Server-side API (future) | Cross-device | ~100ms | Cloud sync (deferred) |

**Implementation for v3 (L1 + L2):**

1. **On session creation**: Write `SessionMetadata` to localStorage
2. **On message add**: Append to IndexedDB `conversations` store (keyed by sessionId)
3. **On circuit reconnect**: Hydrate `SessionManagerState` from IndexedDB
4. **On session archive**: Delete from IndexedDB (or mark tombstoned)
5. **Layout persistence**: Splitter position, sidebar collapsed state, theme → localStorage (addresses the layout persistence gap identified in implementation-gap.md[^2])

**New Dependencies:**
- `Blazored.LocalStorage` (already in original spec's dependency list[^2])
- JS interop module for IndexedDB access

**Impact on v2 Phases:**
- **Phase 4**: Add persistence effects (`SessionPersistenceEffect`) that react to state changes
- **Phase 6**: Session lifecycle must handle hydration on cold start
- **Phase 10**: Verification of persistence/restore cycle

**Effort:** M–L  
**Risk:** Medium — IndexedDB JS interop adds complexity

---

## 3. Context Window Management

### v2 Status
Deferred in v2 §18 OQ-6: "Context window management: MVP sends full history. Monitor token costs. Implement sliding window with summarization if conversations routinely exceed 128K tokens."[^1]

### Why v3 Needs This
With conversation branching and session persistence, conversations will naturally grow longer. Production systems in 2026 use combined strategies[^10]:

1. **Sliding window buffer**: Keep last N turns verbatim
2. **Summary memory**: LLM-generated summary of older turns
3. **Hybrid** (`ConversationSummaryBufferMemory` pattern): Recent turns verbatim + rolling summary for older context

Additionally, the "lost in the middle" phenomenon means critical context placed in the middle of long conversations gets less attention[^11].

### Proposed v3 Design

**Server-Side Context Pipeline:**

```
Full Conversation History (from client)
    │
    ▼
┌─────────────────────────────┐
│ Context Window Manager       │
│                              │
│ 1. Count tokens (tiktoken)   │
│ 2. If < budget → pass through│
│ 3. If > budget:              │
│    a. Keep system prompt      │
│    b. Keep last K turns       │
│    c. Summarize older turns   │
│    d. Assemble: system +      │
│       summary + recent turns  │
└─────────────────────────────┘
    │
    ▼
LLM API (within token budget)
```

**Token Budget Strategy:**
- Reserve 20% for response generation
- System prompt: ~250 tokens (fixed)
- Recent turns: last 10 turns verbatim
- Summary: condensed to ~500 tokens
- Tool definitions: ~500 tokens (8 tools)

**Implementation:**
- New `ContextWindowManager` service in AGUIDojoServer
- Integrates as a pre-processing step in the unified agent pipeline
- Token counting via `Microsoft.ML.Tokenizers` (or direct tiktoken)
- Summary generation via a secondary LLM call with a summarization prompt

**Impact on v2 Phases:**
- **Phase 2 (Unified Endpoint)**: Add context manager as a pre-processing step before agent invocation
- New server-side service, minimal client impact

**Effort:** M  
**Risk:** Medium — summarization quality affects conversation coherence

---

## 4. Claude Design System & Theming

### v2 Status
The implementation gap analysis identifies this as the **#1 most visible gap**: "The spec's central visual identity is completely absent. The current app.css uses a basic ASP.NET Core template with a blue-to-cyan gradient."[^2] v2's spec critique (ISS-series) also highlights missing dark mode, oklch() properties, and system font stacks.

### Why v3 Needs This
The Claude design system's warm terracotta palette is specifically designed for AI chat interfaces — it feels more human, approachable, and reduces the cold/clinical impression that blue/white UIs create[^12]. In 2026, users expect:

- **Dark mode** as a first-class feature (not an afterthought)
- **System-level theme detection** (`prefers-color-scheme`)
- **Glassmorphism** for overlay depth
- **Perceptually uniform colors** via oklch() for consistent contrast[^13]

### Proposed v3 Design

**CSS Custom Properties (oklch):**

```css
/* Light Mode (Claude Light) */
:root {
  --background: oklch(97% 0.02 70);      /* Cream */
  --foreground: oklch(18% 0.01 30);      /* Deep text */
  --primary: oklch(70% 0.14 45);         /* Warm terracotta */
  --primary-foreground: oklch(97% 0.01 45);
  --card: oklch(95% 0.02 70);            /* Slightly darker cream */
  --muted: oklch(92% 0.02 70);
  --border: oklch(85% 0.03 60);
  --accent: oklch(65% 0.12 35);          /* Deeper accent */
}

/* Dark Mode (Claude Dark) */
.dark {
  --background: oklch(15% 0.02 45);      /* Deep warm brown */
  --foreground: oklch(89% 0.02 70);      /* Creamy text */
  --primary: oklch(70% 0.14 45);         /* Terracotta (same hue) */
  --card: oklch(18% 0.02 45);
  --muted: oklch(22% 0.02 45);
  --border: oklch(30% 0.03 45);
}
```

**Theme Toggle Implementation:**
- `ThemeService` (already exists[^14]) → extend with localStorage persistence
- `.dark` class on `<html>` element
- System preference detection via `@media (prefers-color-scheme: dark)`
- BB v3's built-in theme support (`BbThemeProvider`)

**Typography:**
- UI: `Inter, system-ui, -apple-system, sans-serif`
- Code: `JetBrains Mono, Cascadia Code, monospace`
- AI responses: slightly different weight/style to distinguish from user text

**Glassmorphism for overlays:**
```css
.overlay-glass {
  backdrop-filter: blur(12px) saturate(180%);
  background: oklch(15% 0.02 45 / 0.75);
  border: 1px solid oklch(30% 0.03 45 / 0.3);
}
```

**Impact on v2 Phases:**
- **Phase 1 (BB v3 Migration)**: Natural integration point — apply theme during BB v3 migration
- Can be a standalone phase inserted after Phase 1

**Effort:** M  
**Risk:** Low–Medium — CSS-only with BB v3 theme integration

---

## 5. Message Virtualization & Scroll Performance

### v2 Status
The implementation gap analysis identifies this as a **Moderate Gap**: "ChatMessageList renders all messages without Blazor's `<Virtualize>` component. Long conversations will cause DOM bloat and degraded scroll performance."[^2]

### Why v3 Needs This
With session persistence (§2) and branching (§1), conversation lengths will grow dramatically. Blazor's built-in `<Virtualize>` component renders only visible items plus a small buffer, handling 100K+ items smoothly[^15]. Without virtualization, DOM bloat degrades scroll performance linearly with message count.

### Proposed v3 Design

```razor
<div class="message-list" style="height: 100%; overflow-y: auto;">
    <Virtualize Items="@ActiveMessages"
                ItemSize="80"
                OverscanCount="5"
                @ref="_virtualizeRef">
        <ItemContent Context="message">
            <ChatMessageItem Message="@message" />
        </ItemContent>
        <Placeholder>
            <div class="message-skeleton" />
        </Placeholder>
    </Virtualize>
</div>
```

**Challenges for chat:**
- Variable message heights (short text vs. long code blocks)
- Auto-scroll to bottom on new messages
- "Jump to bottom" FAB when scrolled up
- Streaming message at bottom grows in height during generation

**Solution:**
- Use `ItemSize` as an average/estimate; Blazor handles variable heights
- Auto-scroll logic: if user is within 50px of bottom, auto-scroll on new content
- Floating "↓ New messages" indicator when scrolled up

**Impact on v2 Phases:**
- **Phase 8 (Session Sidebar UI)**: Integrate into ChatMessageList
- Can be done as part of Phase 10 (Polish)

**Effort:** S–M  
**Risk:** Low — well-supported by Blazor framework

---

## 6. Multimodal Attachments

### v2 Status
Not mentioned in v2 at all. The current implementation supports text-only chat input.

### Why v3 Needs This
Multimodal input (images, PDFs, files) is now standard in production AI chat applications[^16]. AG-UI protocol already supports multimodal attachments through its message structure[^17]. Leading models (GPT-4o, Claude 3, Gemini) all process images and documents natively.

### Proposed v3 Design

**Client-Side:**

1. **ChatInput enhancement**: Add file upload button, drag-and-drop zone, clipboard paste
2. **File processing**: Validate type/size, convert images to base64, extract text from PDFs
3. **Attachment preview**: Thumbnails/chips below the input showing pending attachments
4. **Message rendering**: Inline image display, file download links, PDF previews

**Server-Side:**

1. **Message conversion**: Include `ImageContent` / `DataContent` in `ChatMessage` items
2. **Tool support**: New `analyze_image` tool for vision-capable model queries
3. **Size limits**: Max 10MB per file, max 5 attachments per message

**Data Flow:**

```
User drops image → ChatInput
    → Base64 encode → Add to pending attachments
    → On Send: Include as ImageContent in ChatMessage
        → AG-UI RunAgentInput with multimodal message
            → LLM processes image + text together
                → Response streamed back normally
```

**Impact on v2 Phases:**
- **Phase 2 (Unified Endpoint)**: Agent must handle multimodal ChatMessage
- **Phase 5 (AgentStreamingService)**: Handle attachment serialization in SSE
- **ChatInput.razor**: Major enhancement for file upload UI

**Effort:** M–L  
**Risk:** Medium — file handling adds complexity; LLM model must support vision

---

## 7. Multi-Agent Orchestration & Visualization

### v2 Status
Explicitly a **Non-Goal**: "Multi-agent orchestration (future — MAF Workflow API)."[^1] The implementation gap identifies: "All agent messages share a generic 'Assistant' identity with a single inline SVG."[^2]

### Why v3 Needs This
Multi-agent systems are now mainstream in production[^18]. Microsoft's own Agent Framework supports pipeline composition via `AIAgentBuilder.Use()` — which v2 already uses for wrapper agents. The 2026 UX patterns for multi-agent systems include[^19]:

- **Per-agent identity**: Distinct avatars, names, and capabilities per agent
- **Handoff visualization**: Clear transitions between agents with labels
- **Agent status indicators**: Show which agent is actively processing
- **Hierarchical delegation**: Manager agent delegates to specialist sub-agents

### Proposed v3 Design

**Phase 1: Agent Identity (Low effort, high impact)**

```csharp
// Agent metadata carried in AgentResponseUpdate
public record AgentIdentity
{
    public string Name { get; init; }        // "Planner", "Researcher", "Coder"
    public string IconName { get; init; }    // Lucide icon name
    public string ColorClass { get; init; }  // CSS class for agent-specific accent
    public string Description { get; init; } // One-line capability description
}
```

- Each wrapper agent sets `AuthorName` on `AgentResponseUpdate`
- Client renders per-agent avatars based on `AuthorName` mapping
- `AgentAvatar.razor` and `AgentHandoff.razor` already exist in the codebase[^20] — extend them

**Phase 2: Handoff Visualization (Medium effort)**

- Separator component between agent turns: "🔄 Handed off to Researcher"
- Agent status in sidebar: "Planner → Researcher → Coder" pipeline indicator
- Canvas pane: optional "Agent Pipeline" tab showing real-time flow

**Phase 3: MAF Workflow Integration (Future, deferred)**

- Use MAF Workflow API for durable, cross-session orchestration
- Agent-as-tool pattern: wrap sub-agents as `AIFunction` tools[^21]

**Impact on v2 Phases:**
- **Phase 2 (Unified Endpoint)**: Wrappers set `AuthorName`
- **Phase 8 (Session Sidebar)**: Agent status indicator per session
- `AgentAvatar.razor`, `AgentHandoff.razor`: Already scaffolded[^20]

**Effort:** S (Phase 1) to L (Phase 3)  
**Risk:** Low (Phase 1), High (Phase 3 — depends on MAF Workflow API maturity)

---

## 8. LLM-Generated Session Titles

### v2 Status
Deferred in v2 §18 OQ-2: "LLM-generated titles — First-message truncation. Revisit after multi-session is stable."[^1] Current MVP: first 50 chars of first user message, ellipsed.

### Why v3 Needs This
First-message truncation produces poor titles for complex conversations. ChatGPT, Claude, and Copilot all use LLM-generated titles that summarize the conversation's theme. This is a small, high-impact UX improvement.

### Proposed v3 Design

**Trigger:** After the first assistant response completes (not on the first user message — wait for context).

**Implementation:**

```csharp
// SessionTitleEffect.cs — extend existing effect
[EffectMethod]
public async Task GenerateTitle(SessionActions.StreamCompletedAction action, IDispatcher dispatcher)
{
    var session = _sessionStore.Value.Sessions[action.SessionId];
    if (session.Metadata.Title != "New Chat" && !session.Metadata.Title.EndsWith("..."))
        return; // Already has a real title

    var messages = session.State.Messages.Take(4); // First 2 turns
    var titlePrompt = "Summarize this conversation in 5-8 words for a sidebar title. " +
                      "Return ONLY the title text, no quotes or punctuation.";

    var title = await _titleGenerator.GenerateAsync(messages, titlePrompt);
    dispatcher.Dispatch(new SessionActions.UpdateTitleAction(action.SessionId, title));
}
```

**Cost optimization:**
- Use a cheap/fast model (GPT-5-mini) for title generation
- Cache generated titles — don't regenerate on every completion
- Fall back to truncation if LLM call fails

**Impact on v2 Phases:**
- **Phase 4**: `SessionTitleEffect` already exists[^22] — extend it
- Needs server-side endpoint or client-side model call for title generation

**Effort:** S  
**Risk:** Low

---

## 9. Accessibility (WCAG 2.2 AA)

### v2 Status
Not mentioned in v2. No accessibility requirements are specified.

### Why v3 Needs This
Regulatory requirements are tightening in 2026[^23]:
- **European Accessibility Act (EAA)**: Enforceable since 2025
- **ADA Title II & Section 508**: Require WCAG 2.1/2.2 compliance for digital services by 2026
- **EN 301 549**: Covers public sector and enterprise ICT in the EU

For an agentic chat app, key accessibility requirements include:

### Proposed v3 Design

**1. Chat Message Log:**
```html
<div role="log" aria-live="polite" aria-atomic="false" aria-relevant="additions">
    <!-- Messages rendered here -->
</div>
```

**2. Keyboard Navigation:**
- All interactive elements reachable via Tab
- `Escape` to cancel current generation
- `Enter` to send message (already exists)
- `Ctrl+/` to focus chat input
- Arrow keys for branch navigation
- Focus management: after sending a message, return focus to input

**3. Screen Reader Announcements:**
- `aria-live="polite"` for new messages (non-disruptive)
- `aria-live="assertive"` for errors, HITL approvals (urgent)
- Status announcements: "Agent is generating...", "Generation complete"

**4. Visual Accessibility:**
- Color contrast ratio ≥ 4.5:1 for normal text, ≥ 3:1 for large text
- Focus indicators visible in both light and dark themes
- Motion reduction: respect `prefers-reduced-motion`

**5. Component-Level:**
- All BB v3 components include built-in ARIA — leverage this
- Custom components (`ChatInput`, `ApprovalDialog`, `PlanDisplay`) need manual ARIA
- Skip-navigation link to main content

**Impact on v2 Phases:**
- **Phase 1 (BB v3)**: BB v3 provides better accessibility foundations
- **Phase 8 (Session Sidebar)**: Sidebar keyboard navigation
- **Phase 10 (Polish)**: Accessibility audit pass

**Effort:** M  
**Risk:** Low — incremental, can be layered on top of existing implementation

---

## 10. Command Palette & Keyboard-First UX

### v2 Status
The v2 spec marks `BbCommandDialog` (Cmd+K) as an "Optional MVP Enhancement" in v2 §8.1[^24]. The implementation gap identifies it as a missing feature[^2].

### Why v3 Needs This
Power users expect keyboard-driven interfaces. A command palette provides:
- Quick session search (by title, date)
- Tool discovery ("What tools can the agent use?")
- Navigation shortcuts ("Go to session...", "New chat", "Toggle theme")
- Active state viewing ("Which sessions are streaming?")

### Proposed v3 Design

**BB v3 Integration:**
```razor
<BbCommandDialog @bind-Open="@_commandOpen">
    <BbCommandInput Placeholder="Type a command or search..." />
    <BbCommandList>
        <BbCommandGroup Heading="Sessions">
            @foreach (var session in FilteredSessions)
            {
                <BbCommandItem OnSelect="() => SwitchTo(session.Id)">
                    @session.Metadata.Title
                </BbCommandItem>
            }
        </BbCommandGroup>
        <BbCommandGroup Heading="Actions">
            <BbCommandItem OnSelect="NewChat">New Chat</BbCommandItem>
            <BbCommandItem OnSelect="ToggleTheme">Toggle Theme</BbCommandItem>
            <BbCommandItem OnSelect="ToggleSidebar">Toggle Sidebar</BbCommandItem>
        </BbCommandGroup>
    </BbCommandList>
</BbCommandDialog>
```

**Keyboard shortcut:** `Ctrl+K` / `Cmd+K`

**Impact on v2 Phases:**
- **Phase 8 (Session Sidebar)**: Integrate command palette trigger in sidebar header
- Depends on BB v3 `BbCommandDialog` component availability

**Effort:** S–M  
**Risk:** Low

---

## 11. Autonomy Controls & Confidence Visualization

### v2 Status
Not addressed in v2. The current implementation has binary control: send message → agent runs to completion, or cancel.

### Why v3 Needs This
2026 UX patterns emphasize user control over agent autonomy[^25]:
- **Intent Preview**: Before executing high-impact actions, show what the agent plans to do
- **Autonomy Dial**: Let users set how much autonomous action the agent can take
- **Confidence Scores**: Display agent confidence in its responses/tool selections
- **Action Audit Trail**: Reviewable log of all agent actions with undo capability

### Proposed v3 Design

**1. Autonomy Levels (per session):**

| Level | Behavior | Use Case |
|-------|----------|----------|
| **Suggest** | Agent proposes actions; user must approve each | Sensitive operations |
| **Auto with Review** | Agent acts; user can undo within 5s grace period | Default |
| **Full Auto** | Agent acts immediately; no review delay | Trusted workflows |

**2. Confidence Visualization:**
- Agent system prompt includes instruction to self-rate confidence
- Client renders confidence as a subtle indicator (color-coded dot or bar)
- Low-confidence responses get a "Review recommended" badge

**3. Action Audit in Canvas Pane:**
- New "Activity" tab in canvas showing a timeline of:
  - Tool calls (with arguments and results)
  - State changes
  - Branch points
  - Approval decisions
- Each entry is expandable for details

**Impact on v2 Phases:**
- **Phase 2 (Unified Endpoint)**: System prompt modification for confidence
- **Phase 5 (AgentStreamingService)**: Autonomy level affects when to auto-dispatch vs. queue
- **Phase 8 (Session Sidebar)**: Autonomy level selector per session

**Effort:** M–L  
**Risk:** Medium — prompt engineering for reliable confidence scores

---

## 12. Streaming & SSE Infrastructure Upgrades

### v2 Status
v2 uses the existing AGUIChatClient SSE transport. No infrastructure-level SSE optimizations are specified.

### Why v3 Needs This
.NET 10 introduces native SSE support via `System.Net.ServerSentEvents` and `TypedResults.ServerSentEvents()`[^26], which could simplify and optimize the streaming pipeline:

### Proposed v3 Design

**1. .NET 10 Native SSE:**
```csharp
// Replace custom SSE handling with .NET 10 native
app.MapGet("/chat-stream/{sessionId}", (string sessionId) =>
    TypedResults.ServerSentEvents(GetSessionStream(sessionId)));
```

**2. Backpressure Handling:**
- Detect slow/unresponsive clients
- Implement bounded buffers per session
- Drop or throttle for congested clients

**3. First-Token Latency Optimization:**
- Target: first visible token under 250ms[^27]
- Flush chunks immediately (no batching delay)
- Monitor via OpenTelemetry metrics

**4. Reconnection with Last-Event-ID:**
- SSE standard reconnection using `Last-Event-ID` header
- Server replays missed events from a short-lived buffer
- Prevents data loss on transient disconnects

**5. Observability Metrics (new):**
- `agui.sse.first_token_latency_ms` — time to first token
- `agui.sse.tokens_per_second` — streaming throughput
- `agui.sse.concurrent_connections` — active SSE streams
- `agui.sse.reconnection_count` — client reconnections

**Impact on v2 Phases:**
- **Phase 2 (Unified Endpoint)**: SSE infrastructure lives here
- **Phase 5 (AgentStreamingService)**: Backpressure and reconnection
- Can be done as infrastructure improvements during Phase 10

**Effort:** M  
**Risk:** Medium — .NET 10 SSE API compatibility with AG-UI hosting layer needs verification

---

## 13. Summary: v2 → v3 Upgrade Matrix

| # | Domain | v2 Status | v3 Priority | Effort | Risk | Best Phase Slot |
|---|--------|-----------|-------------|--------|------|-----------------|
| 1 | Conversation Branching | Non-Goal | **P1 — Critical** | L | High | New Phase 4.5 |
| 2 | Session Persistence | Deferred (OQ-1) | **P1 — Critical** | M–L | Medium | New Phase 6.5 |
| 3 | Context Window Mgmt | Deferred (OQ-6) | **P1 — Critical** | M | Medium | Phase 2 extension |
| 4 | Claude Design System | Not started (Gap #1) | **P1 — Critical** | M | Low–Med | Phase 1 extension |
| 5 | Message Virtualization | Gap (Moderate) | **P2 — Important** | S–M | Low | Phase 10 |
| 6 | Multimodal Attachments | Not mentioned | **P2 — Important** | M–L | Medium | New Phase 11 |
| 7 | Multi-Agent (Phase 1) | Non-Goal | **P2 — Important** | S | Low | Phase 2 extension |
| 8 | LLM Session Titles | Deferred (OQ-2) | **P2 — Important** | S | Low | Phase 4 extension |
| 9 | Accessibility (WCAG) | Not mentioned | **P1 — Critical** | M | Low | Cross-cutting |
| 10 | Command Palette | Optional in v2 | **P3 — Nice-to-have** | S–M | Low | Phase 8 extension |
| 11 | Autonomy Controls | Not mentioned | **P3 — Nice-to-have** | M–L | Medium | New Phase 11 |
| 12 | SSE Infrastructure | Not addressed | **P2 — Important** | M | Medium | Phase 2/10 |

### Recommended v3 Phase Ordering

```
v2 Phases 0–10 (unchanged, as baseline)
    │
    ├── Phase 11: Claude Design System (alongside Phase 1)
    ├── Phase 12: Context Window Management (after Phase 2)
    ├── Phase 13: Conversation Branching (after Phase 4)
    ├── Phase 14: Session Persistence (after Phase 6)
    ├── Phase 15: Message Virtualization (after Phase 8)
    ├── Phase 16: Multimodal Attachments
    ├── Phase 17: LLM Session Titles
    ├── Phase 18: Accessibility Audit
    ├── Phase 19: SSE Infrastructure Upgrades
    ├── Phase 20: Command Palette
    ├── Phase 21: Multi-Agent Identity
    └── Phase 22: Autonomy Controls & Confidence
```

Alternatively, v3 can be structured as a **layered upgrade** on top of v2:

**Layer A (Must-Have for Production):** Domains 1–4, 9 — transform from demo to production-grade  
**Layer B (Feature Parity):** Domains 5–8, 12 — match competitor feature sets  
**Layer C (Differentiation):** Domains 10–11 — cutting-edge agentic UX

---

## 14. Confidence Assessment

| Claim | Confidence | Basis |
|-------|-----------|-------|
| v2's non-goals need promotion to v3 goals | **High** | Directly stated in v2 §1 and §18 OQ list[^1] |
| Conversation branching is now table-stakes | **High** | ChatGPT, Claude, LangChain all ship it[^3][^4][^5] |
| Claude design system is the #1 visual gap | **High** | Explicitly stated in implementation-gap.md[^2] |
| Message virtualization is necessary | **High** | Standard Blazor performance practice[^15] |
| .NET 10 native SSE applies to AG-UI hosting | **Medium** | AG-UI uses custom SSE; compatibility needs verification |
| WCAG 2.2 AA is legally required | **High** | EAA (2025), ADA Title II (2026)[^23] |
| Autonomy controls are user-expected | **Medium** | Emerging pattern; not yet universal[^25] |
| Multi-agent orchestration UX is needed | **Medium** | v2 uses single agent with wrappers; future MAF Workflow API[^21] |
| IndexedDB persistence is the right approach for Blazor Server | **Medium** | Standard for SPAs; Blazor Server's JS interop adds complexity |

### Key Assumptions
1. BB v3 is available and stable by implementation time (v2 Phase 1 prerequisite)
2. MAF framework remains compatible with the wrapper composition pattern
3. LLM providers support multimodal input in the chat completion API
4. AG-UI protocol remains the transport layer (no switch to WebSocket)

---

## 15. Footnotes

[^1]: `dotnet/samples/05-end-to-end/AGUIDojo/.docs/unified-agentic-chat-implementation-plan-v2.md` — v2 §1 Non-Goals, §18 Open Questions
[^2]: `dotnet/samples/05-end-to-end/AGUIDojo/.docs/implementation-gap.md` — Implementation vs. Spec critique
[^3]: [ChatGPT Branching Chats](https://lifehacker.com/tech/chatgpt-has-added-branching-chats) — ChatGPT 5 branching feature (2025–2026)
[^4]: [Forky: Git-style LLM chat management](https://github.com/ishandhanani/forky) — Open-source DAG-based chat history
[^5]: [LangChain Branching Chat](https://docs.langchain.com/oss/python/langchain/frontend/branching-chat) — Developer framework for branching semantics
[^6]: [GhostChat v2.0 — Local-first AI chat](https://news.ycombinator.com/item?id=45220472) — Local-first architecture with IndexedDB
[^7]: [IndexedDB Chat Persistence Implementation](https://hejoseph.com/dev/docs/Portfolio/Chatbot/persist-chat-data/) — Technical guide
[^8]: [Conversation Persistence — InAppAI](https://www.inappai.com/docs/react/persistence/) — Cross-device sync patterns
[^9]: [Dexie.js Cloud — Offline-first with sync](https://dexie.org/) — IndexedDB with real-time cloud sync
[^10]: [Context Window Management Strategies](https://apxml.com/courses/langchain-production-llm/chapter-3-advanced-memory-management/context-window-management) — Sliding window + summarization patterns
[^11]: [Context Window Limits](https://inventivehq.com/blog/context-window-limits-managing-long-documents) — "Lost in the middle" phenomenon
[^12]: [React Claude Theme — shadcn.io](https://www.shadcn.io/theme/claude) — Claude's warm terracotta design
[^13]: [OKLCH Design System](https://skills.rest/skill/design-system-modern-oklch) — Perceptually uniform color space for UI
[^14]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Services/ThemeService.cs` — Existing theme service in codebase
[^15]: [Blazor Virtualization: 100k Items](https://amarozka.dev/blazor-virtualization-100k-items-ef-core-paging-streaming/) — Blazor `<Virtualize>` performance
[^16]: [File Upload & Multimodal Content — LangChain Agent Chat UI](https://deepwiki.com/langchain-ai/agent-chat-ui/6-file-upload-and-multimodal-content) — Multimodal chat patterns
[^17]: [AG-UI Protocol Documentation](https://docs.ag-ui.com/introduction) — AG-UI supports multimodal attachments
[^18]: [Multi-Agent Orchestration Patterns 2026](https://www.frankx.ai/blog/multi-agent-orchestration-patterns-2026) — Production multi-agent systems
[^19]: [Handoff Orchestration — Agentic Design](https://agentic-design.ai/patterns/multi-agent/handoff-orchestration) — UX patterns for agent handoffs
[^20]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Components/Pages/Chat/AgentAvatar.razor` and `AgentHandoff.razor` — Existing scaffolded components
[^21]: `dotnet/src/Microsoft.Agents.AI/AgentExtensions.cs` — `AsAIFunction()` wraps agents as callable tools
[^22]: `dotnet/samples/05-end-to-end/AGUIDojo/AGUIDojoClient/Store/SessionManager/SessionTitleEffect.cs` — Existing title effect
[^23]: [AI Chatbot Accessibility Compliance Guide](https://enabled.in/ai-chatbot-accessibility-ada-section-508-eaa-en-301-549-compliance-guide/) — EAA, ADA, Section 508 requirements
[^24]: v2 spec §8.1 / self-critique SC-025 — BbCommandDialog marked as "Optional MVP Enhancement"
[^25]: [Designing for Autonomy: UX Principles for Agentic AI](https://uxmag.com/articles/designing-for-autonomy-ux-principles-for-agentic-ai-systems) — Autonomy dial, intent preview, confidence patterns
[^26]: [Server-Sent Events in .NET 10: Native Solution](https://easyappdev.com/blog/server-sent-events-in-net-10-finally-a-native-solution) — .NET 10 `TypedResults.ServerSentEvents()`
[^27]: [Streaming at Scale: SSE, WebSockets & Real-Time AI API Design](https://www.learnwithparam.com/blog/streaming-at-scale-sse-websockets-real-time-ai-apis) — First-token latency targets
