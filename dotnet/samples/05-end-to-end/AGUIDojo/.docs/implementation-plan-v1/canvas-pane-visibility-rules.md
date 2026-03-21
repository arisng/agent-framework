# Canvas-Pane Visibility Rules

**Version:** 1.0  
**Created:** 2026-02-09  
**Status:** Official Guidance  
**Scope:** AG-UI Blazor Dual-Pane Layout Architecture

---

## Table of Contents

1. [Purpose](#purpose)
2. [Core Concepts](#core-concepts)
3. [Visibility Decision Criteria](#visibility-decision-criteria)
4. [Component Classification](#component-classification)
5. [Decision Flowchart](#decision-flowchart)
6. [Integration Contract](#integration-contract)
7. [Examples](#examples)
8. [Mobile vs Desktop Behavior](#mobile-vs-desktop-behavior)
9. [Testing Guidelines](#testing-guidelines)
10. [FAQ](#faq)

---

## Purpose

This document defines **when the canvas-pane should be visible** in the AG-UI Blazor dual-pane layout. It serves as the source of truth for:

- Current component placement decisions
- Future component integration
- Mobile and desktop behavior consistency
- Quality assurance and testing

**Key Principle:** The canvas-pane is a **dedicated space for interactive shared artifacts** where users and AI agents collaborate on documents, forms, diagrams, or other editable content with bidirectional state synchronization.

---

## Core Concepts

### What is the Canvas-Pane?

The canvas-pane is the **right pane** in the dual-pane desktop layout (≥768px viewport width). It provides:

- **Large workspace** for editing artifacts (60% default width, resizable down to 35% minimum)
- **Persistent context** across conversation turns
- **State synchronization** between user edits and AI updates
- **Focused environment** separate from message flow

### What is an "Interactive Shared Artifact"?

An **interactive shared artifact** is content that meets ALL of these criteria:

| Criterion | Description | Examples |
|-----------|-------------|----------|
| **Editable** | User can modify content through form inputs, text editors, or visual controls | Recipe editor with form fields, Monaco code editor, diagram editor with drag-drop |
| **Stateful** | Changes persist across conversation turns and are managed by Fluxor state (`ArtifactState` or similar) | RecipeEditor updates `ArtifactState.CurrentRecipe`, document editor updates `ArtifactState.CurrentDocumentState` |
| **Shared** | Both user and AI can read/modify the same artifact (bidirectional collaboration) | User edits recipe → AI suggests improvements → User accepts → State updates → UI reflects changes |
| **Requires Focus** | Content is complex enough to warrant dedicated screen space (not suitable for inline message display) | Multi-field forms, syntax-highlighted code editors, WYSIWYG editors, architectural diagrams |

### What is NOT an Interactive Shared Artifact?

Content that is **read-only, transient, or informational** does NOT belong in the canvas-pane:

| Type | Characteristics | Display Location |
|------|----------------|------------------|
| **Tool Results** | Read-only data from backend tool execution (weather, search results, database queries) | Message-list |
| **Generative UI** | Dynamic visualizations generated during streaming (plan progress, charts, timelines) | Message-list |
| **Observability** | Debugging/monitoring information (memory inspector, tool execution tracker, logs) | Message-list |
| **Simple Visualizations** | Charts, graphs, tables that display data without editing capability | Message-list |

---

## Visibility Decision Criteria

The canvas-pane should be **visible** if and only if:

1. **At least one interactive shared artifact is active** (e.g., RecipeEditor, document editor)
2. **The artifact has renderable content** (not empty state)
3. **The viewport is in desktop mode** (≥768px width)

The canvas-pane should be **hidden** when:

1. **No interactive artifacts are active** (conversation is informational)
2. **Viewport is in mobile mode** (<768px) → Use bottom drawer instead
3. **Artifact is empty** (optional: can show empty state placeholder)

---

## Component Classification

### ✅ Canvas-Pane Eligible (Interactive Shared Artifacts)

| Component | Location | State Management | Mobile Behavior | Reason |
|-----------|----------|------------------|-----------------|--------|
| **RecipeEditor** | `Components/SharedState/RecipeEditor.razor` | `ArtifactState.CurrentRecipe` | Bottom drawer (60vh height) | ✅ Editable form fields (name, description, ingredients, steps), user-AI collaboration on recipe refinement, requires focused workspace |
| **MonacoEditor** (if implemented) | `Components/SharedState/DocumentEditor.razor` (hypothetical) | `ArtifactState.CurrentDocumentState` | Bottom drawer (full screen) | ✅ Multi-line code/text editing, syntax highlighting, collaborative document authoring, requires large workspace |
| **DiagramEditor** (future) | `Components/SharedState/DiagramEditor.razor` (hypothetical) | `ArtifactState.CurrentDiagram` | Bottom drawer (landscape mode) | ✅ Visual diagram editing with drag-drop, node connections, collaborative architecture design |
| **FormBuilder** (future) | `Components/SharedState/FormBuilder.razor` (hypothetical) | `ArtifactState.CurrentFormSchema` | Bottom drawer (full screen) | ✅ Dynamic form design, field configuration, collaborative form schema creation |

### ❌ Canvas-Pane Ineligible (Non-Interactive Content)

| Component | Location | Render Location | Mobile Behavior | Reason |
|-----------|----------|----------------|-----------------|--------|
| **WeatherDisplay** | `Components/ToolResults/WeatherDisplay.razor` | Message-list | Inline in message-list | ❌ Read-only tool result, no editing capability, compact card suitable for inline display |
| **PlanProgress** | `Components/GenerativeUI/PlanProgress.razor` | Message-list | Inline in message-list | ❌ Read-only generative UI, real-time progress tracker during streaming, informational only |
| **MemoryInspector** | `Components/Observability/MemoryInspector.razor` | Message-list | Inline in message-list | ❌ Observability/debugging tool, displays state without editing, accordion-style for collapsibility |
| **ToolExecutionTracker** | `Components/Observability/ToolExecutionTracker.razor` | Message-list | Inline in message-list | ❌ Observability/debugging tool, displays tool execution timeline, read-only |
| **ChartDisplay** (future) | `Components/GenerativeUI/ChartDisplay.razor` (hypothetical) | Message-list | Inline in message-list | ❌ Read-only data visualization (bar, line, pie charts), no editing capability |
| **DataGridDisplay** (future) | `Components/ToolResults/DataGridDisplay.razor` (hypothetical) | Message-list | Inline in message-list | ❌ Read-only tabular data display, pagination/sorting only (not data editing) |

### 🤔 Edge Cases (Requires Context)

| Component Type | Canvas-Pane? | Decision Criteria |
|----------------|--------------|-------------------|
| **Editable DataGrid** | ✅ Yes | If users can edit cell values AND changes persist to shared state (e.g., spreadsheet collaboration) |
| **Interactive Chart** | ❌ No (usually) | Charts with zoom/pan are still read-only visualizations. Only if chart annotations can be edited and saved (e.g., diagram annotations) then → canvas-pane |
| **Form with Submit Button** | ⚠️ Depends | If form is one-time submission (e.g., search form) → message-list. If form is iterative editing of shared artifact (e.g., profile editor) → canvas-pane |
| **Image Annotator** | ✅ Yes | If users can draw, annotate, or edit images with changes synced to shared state |

---

## Decision Flowchart

```
START: New component needs placement
│
├─ Q1: Can the USER edit/modify this content?
│  ├─ NO → Message-List (read-only content)
│  └─ YES → Continue to Q2
│
├─ Q2: Do changes persist across conversation turns (stateful)?
│  ├─ NO → Message-List (transient/one-time interaction)
│  └─ YES → Continue to Q3
│
├─ Q3: Can the AI also read/modify this content (shared)?
│  ├─ NO → Message-List (user-only input, not collaborative)
│  └─ YES → Continue to Q4
│
├─ Q4: Does content require dedicated workspace (not suitable for inline)?
│  ├─ NO → Message-List (simple form/toggle, fits inline)
│  └─ YES → Canvas-Pane (interactive shared artifact)
│
END: Placement determined
```

**Quick Reference:**

- **All 4 criteria YES** → Canvas-Pane ✅
- **Any criterion NO** → Message-List ❌

---

## Integration Contract

### For New Component Authors

When creating a new component, follow this integration checklist:

#### 1. **Determine Component Type**

Run through the decision flowchart:
1. Is it editable by the user?
2. Does state persist across turns?
3. Can AI also modify it?
4. Does it need dedicated space?

#### 2. **Register in ToolComponentRegistry** (if tool-based)

```csharp
// File: AGUIDojoClient/Services/ToolComponentRegistry.cs

private void RegisterComponents()
{
    // For tool-based components (FunctionResultContent)
    RegisterToolComponent<WeatherDisplay>("get_weather", "weatherData");
    RegisterToolComponent<DataGridDisplay>("show_data_grid", "gridData");
    
    // Note: Canvas-pane components (RecipeEditor, DocumentEditor) 
    // are state-based, not tool-based, so they are NOT registered here.
}
```

**Important:** If your component is an **interactive shared artifact** (canvas-pane eligible), it should be **state-based** using Fluxor `ArtifactState`, NOT tool-based. Tool-based components are for message-list content only.

#### 3. **Set Routing Metadata** (proposed enhancement for task-7)

Once task-7 implements metadata routing, update registrations:

```csharp
// Proposed API (not yet implemented)
RegisterToolComponent(
    toolName: "get_weather",
    componentType: typeof(WeatherDisplay),
    parameterName: "weatherData",
    metadata: new ToolMetadata
    {
        RenderLocation = RenderLocation.MessageList,
        IsVisual = true,
        IsInteractive = false
    }
);
```

#### 4. **State Management** (for canvas-pane components)

Canvas-pane components must use Fluxor state:

```csharp
// File: AGUIDojoClient/Store/ArtifactState/ArtifactState.cs

public record ArtifactState
{
    public Recipe? CurrentRecipe { get; init; }
    public DocumentState? CurrentDocumentState { get; init; }
    // Add new artifact properties here
}

// Example: Add new artifact type
public record DiagramState
{
    public string DiagramId { get; init; }
    public List<Node> Nodes { get; init; }
    public List<Edge> Edges { get; init; }
}

// Then update ArtifactState:
public record ArtifactState
{
    public Recipe? CurrentRecipe { get; init; }
    public DocumentState? CurrentDocumentState { get; init; }
    public DiagramState? CurrentDiagram { get; init; } // NEW
}
```

#### 5. **Render in CanvasPane** (for canvas-pane components)

Update `CanvasPane.razor` to render new artifact:

```razor
<!-- File: AGUIDojoClient/Components/Layout/CanvasPane.razor -->

@if (ArtifactStore.Value.CurrentRecipe is not null)
{
    <RecipeEditor />
}
else if (ArtifactStore.Value.CurrentDocumentState is not null)
{
    <DocumentEditor />
}
else if (ArtifactStore.Value.CurrentDiagram is not null)
{
    <!-- NEW ARTIFACT -->
    <DiagramEditor />
}
else
{
    <!-- Empty state -->
    <div class="canvas-empty-state">
        <p>No active artifacts</p>
    </div>
}
```

#### 6. **Conditional Visibility** (for canvas-pane components)

Update `DualPaneLayout.razor` to detect interactive artifacts:

```csharp
// File: AGUIDojoClient/Components/Layout/DualPaneLayout.razor.cs

private bool HasInteractiveArtifact()
{
    var artifact = ArtifactStore.Value;
    return artifact.CurrentRecipe is not null
        || artifact.CurrentDocumentState is not null
        || artifact.CurrentDiagram is not null; // Add new artifact checks
}
```

Then in markup:

```razor
@if (IsDesktop && HasInteractiveArtifact())
{
    <!-- Dual-pane layout with canvas-pane visible -->
    <ResizablePanelGroup Direction="ResizableDirection.Horizontal">
        <ResizablePanel DefaultSize="40" MinSize="25">
            <ContextPane />
        </ResizablePanel>
        <ResizableHandle />
        <ResizablePanel DefaultSize="60" MinSize="35">
            <CanvasPane />
        </ResizablePanel>
    </ResizablePanelGroup>
}
else
{
    <!-- Single-pane layout (canvas-pane hidden) -->
    <ContextPane />
}
```

---

## Examples

### Example 1: Correct Usage (RecipeEditor)

**Scenario:** User asks AI to create a recipe. AI generates initial recipe and displays it in RecipeEditor.

**Component:** `RecipeEditor.razor`

**Placement:** Canvas-pane ✅

**Reasoning:**
- ✅ **Editable:** User can modify name, ingredients, steps via form fields
- ✅ **Stateful:** Changes stored in `ArtifactState.CurrentRecipe`, persist across turns
- ✅ **Shared:** AI can update recipe based on user requests (e.g., "make it vegan")
- ✅ **Requires Focus:** Multi-field form with lists, needs dedicated workspace

**Desktop Layout:**
```
┌─────────────────────────┬──────────────────────────────┐
│ Context Pane (40%)      │ Canvas Pane (60%)            │
│ ┌─────────────────────┐ │ ┌──────────────────────────┐ │
│ │ User: Create a      │ │ │ Recipe Editor            │ │
│ │ pasta recipe        │ │ │ Name: [Spaghetti...]     │ │
│ ├─────────────────────┤ │ │ Description: [...]       │ │
│ │ AI: Here's a recipe │ │ │ Ingredients:             │ │
│ │ with 5 ingredients  │ │ │ • [Spaghetti] [200g]     │ │
│ │                     │ │ │ • [Tomato sauce] [...]   │ │
│ │ [RecipeEditor →]    │ │ │ Steps:                   │ │
│ └─────────────────────┘ │ │ 1. [Boil water...]       │ │
│                         │ │ [Save] [Cancel]          │ │
│                         │ └──────────────────────────┘ │
└─────────────────────────┴──────────────────────────────┘
```

**Mobile Layout:**
```
┌──────────────────────────────────────┐
│ Chat (Message List)                  │
│ ┌──────────────────────────────────┐ │
│ │ User: Create a pasta recipe      │ │
│ ├──────────────────────────────────┤ │
│ │ AI: Here's a recipe with 5       │ │
│ │ ingredients                      │ │
│ │ [Tap to edit recipe]             │ │
│ └──────────────────────────────────┘ │
└──────────────────────────────────────┘
          ↑ Tap opens drawer ↓
┌──────────────────────────────────────┐
│ Bottom Drawer (60vh)                 │
│ ┌──────────────────────────────────┐ │
│ │ Recipe Editor                    │ │
│ │ Name: [Spaghetti...]             │ │
│ │ Ingredients: [...]               │ │
│ │ Steps: [...]                     │ │
│ │ [Save] [Cancel]                  │ │
│ └──────────────────────────────────┘ │
└──────────────────────────────────────┘
```

---

### Example 2: Incorrect Usage (WeatherDisplay in Canvas-Pane)

**Scenario:** User asks AI for weather. AI calls `get_weather` tool and returns weather data.

**Component:** `WeatherDisplay.razor`

**Placement:** Canvas-pane ❌ (current bug) → Should be Message-list ✅

**Reasoning:**
- ❌ **Not Editable:** User cannot change temperature, conditions, or forecast
- ❌ **Not Stateful (in artifact sense):** Weather data is transient, specific to one message
- ❌ **Not Shared (collaborative):** AI returns tool result, user just views it (no bidirectional editing)
- ❌ **Doesn't Require Focus:** Compact card (200-280px) suitable for inline display

**Correct Desktop Layout:**
```
┌─────────────────────────────────────────────┐
│ Context Pane (100%, canvas-pane hidden)     │
│ ┌─────────────────────────────────────────┐ │
│ │ User: What's the weather in Seattle?    │ │
│ ├─────────────────────────────────────────┤ │
│ │ AI: Here's the current weather:         │ │
│ │ ┌─────────────────────────────────────┐ │ │
│ │ │ WeatherDisplay (inline in messages) │ │ │
│ │ │ ☀️ Seattle, WA                       │ │ │
│ │ │ 72°F / 22°C                         │ │ │
│ │ │ Sunny, light breeze                 │ │ │
│ │ │ Humidity: 60%, Wind: 5mph           │ │ │
│ │ └─────────────────────────────────────┘ │ │
│ └─────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
```

**Correct Mobile Layout:**
```
┌──────────────────────────────────────┐
│ Chat (Message List)                  │
│ ┌──────────────────────────────────┐ │
│ │ User: What's the weather in      │ │
│ │ Seattle?                         │ │
│ ├──────────────────────────────────┤ │
│ │ AI: Here's the current weather:  │ │
│ │ ┌──────────────────────────────┐ │ │
│ │ │ WeatherDisplay (inline)      │ │ │
│ │ │ ☀️ Seattle, WA                │ │ │
│ │ │ 72°F / 22°C, Sunny           │ │ │
│ │ └──────────────────────────────┘ │ │
│ └──────────────────────────────────┘ │
└──────────────────────────────────────┘
```

---

### Example 3: Edge Case (Editable DataGrid)

**Scenario:** User asks AI to display a data table. AI returns data AND allows inline editing with state sync.

**Component:** `EditableDataGrid.razor` (hypothetical)

**Placement:** ⚠️ **Depends on editing scope**

**Case A: Row-level editing (simple inline edits)**
- **Placement:** Message-list ✅
- **Reasoning:** Each row edits are independent, no complex state orchestration needed, fits inline

**Case B: Spreadsheet collaboration (complex multi-cell editing, formulas, shared state)**
- **Placement:** Canvas-pane ✅
- **Reasoning:** Meets all 4 criteria (editable, stateful, shared with AI, requires focus)

**Decision:** If the grid is essentially a **spreadsheet** where user and AI collaborate on complex data analysis with formulas, aggregations, and shared context → **Canvas-pane**. If it's a simple table with row-level CRUD operations → **Message-list**.

---

## Mobile vs Desktop Behavior

### Desktop (≥768px)

**Canvas-Pane Components:**
- Render in **right pane** (60% default width, resizable)
- Always visible when interactive artifact is active
- ResizablePanelGroup manages responsive width

**Message-List Components:**
- Render in **left pane** (40% default width, resizable)
- Inline within message flow
- ScrollArea for overflow

**Layout Switching:**
- When canvas-pane is hidden → Single-pane layout (context pane 100% width)
- When canvas-pane is visible → Dual-pane layout with ResizableHandle

### Mobile (<768px)

**Canvas-Pane Components:**
- Render in **bottom drawer** (BlazorBlueprint `Sheet` component)
- Triggered by tapping a "View/Edit Artifact" button in message-list
- Drawer height: 60vh (adjustable based on content needs)
- Drawer can be swiped down to dismiss

**Message-List Components:**
- Render **inline** in message flow (same as desktop)
- No drawer required
- Vertical scrolling for overflow

**Layout Notes:**
- No dual-pane layout on mobile (vertical space is limited)
- Focus switches explicitly between chat and artifact editing
- Drawer UX: User must explicitly open artifact, preventing accidental loss of context

### Viewport Detection

**Implementation:**
```csharp
// File: AGUIDojoClient/Services/ViewportService.cs

public class ViewportService
{
    private const int MobileBreakpoint = 768; // pixels

    public bool IsMobile => ViewportWidth < MobileBreakpoint;
    public bool IsDesktop => ViewportWidth >= MobileBreakpoint;
    
    // ViewportWidth updates via JSInterop on window resize
}
```

**Usage in DualPaneLayout:**
```razor
@if (IsDesktop && HasInteractiveArtifact())
{
    <!-- Desktop: Dual-pane with canvas-pane -->
}
else if (IsMobile && HasInteractiveArtifact())
{
    <!-- Mobile: Message-list + bottom drawer trigger -->
}
else
{
    <!-- No artifacts: Single-pane (context pane only) -->
}
```

---

## Testing Guidelines

### For Developers

When implementing or modifying components, test canvas-pane visibility with these scenarios:

#### Test 1: Interactive Artifact Display
1. **Desktop:** Load a page with `RecipeEditor` active
2. **Expected:** Dual-pane layout visible, RecipeEditor in right pane
3. **Verify:** Canvas-pane has correct width (60% default, resizable to 35% min)

#### Test 2: Non-Interactive Content Display
1. **Desktop:** Load a page with only `WeatherDisplay` in messages
2. **Expected:** Single-pane layout (canvas-pane hidden)
3. **Verify:** WeatherDisplay renders inline in message-list

#### Test 3: Dynamic Canvas-Pane Toggle
1. **Desktop:** Start with no artifacts (single-pane)
2. **Action:** User requests recipe creation → AI generates RecipeEditor
3. **Expected:** Layout transitions from single-pane to dual-pane smoothly
4. **Verify:** No layout jank, pane widths correct

#### Test 4: Mobile Drawer Behavior
1. **Mobile:** Load page with `RecipeEditor` active
2. **Action:** Tap "Edit Recipe" button in message-list
3. **Expected:** Bottom drawer opens with RecipeEditor (60vh height)
4. **Verify:** Drawer can be dismissed by swiping down

#### Test 5: Multiple Artifacts Priority
1. **Desktop:** User has both `RecipeEditor` and `DocumentEditor` active
2. **Expected:** Canvas-pane shows the **most recent** artifact (or user-selected tab)
3. **Verify:** Switching between artifacts updates canvas-pane content

### Test Checklist

| Test Case | Desktop Expected | Mobile Expected | Verified? |
|-----------|------------------|----------------|-----------|
| No artifacts active | Single-pane (context 100%) | Message-list only | [ ] |
| RecipeEditor active | Dual-pane (context 40%, canvas 60%) | Message-list + drawer | [ ] |
| WeatherDisplay only | Single-pane (inline weather card) | Inline weather card | [ ] |
| PlanProgress only | Single-pane (inline progress) | Inline progress | [ ] |
| Mixed: RecipeEditor + WeatherDisplay | Dual-pane (canvas shows RecipeEditor, message-list shows weather) | Drawer (RecipeEditor), inline weather | [ ] |
| Viewport resize (desktop → mobile) | Canvas-pane collapses → drawer trigger appears | N/A | [ ] |
| Viewport resize (mobile → desktop) | Drawer content moves to canvas-pane | N/A | [ ] |

---

## FAQ

### Q1: Why can't I put charts in the canvas-pane?

**A:** Charts and visualizations are **read-only displays** in most cases. The canvas-pane is reserved for **editable artifacts** where users actively modify content. Charts fit better inline within the message flow, where users scroll through them as part of the conversation history.

**Exception:** If the chart supports **interactive annotations** (e.g., drawing trend lines, adding notes) AND those annotations persist as shared state, then it qualifies as an interactive artifact.

---

### Q2: What if my component is sometimes interactive and sometimes read-only?

**A:** Use **component props or state** to determine placement dynamically. Examples:

- **FormDisplay with `IsEditable` prop:**
  - `IsEditable=true` → Canvas-pane (editable shared artifact)
  - `IsEditable=false` → Message-list (read-only form summary)

- **Document with `Mode` prop:**
  - `Mode=Edit` → Canvas-pane (document editor)
  - `Mode=Preview` → Message-list (read-only preview)

Update `DualPaneLayout` to check the prop/state:

```csharp
private bool HasInteractiveArtifact()
{
    var artifact = ArtifactStore.Value;
    return (artifact.CurrentForm?.IsEditable ?? false)
        || (artifact.CurrentDocument?.Mode == DocumentMode.Edit)
        || (artifact.CurrentRecipe is not null);
}
```

---

### Q3: The canvas-pane shows up but is empty. Why?

**A:** This happens when `HasInteractiveArtifact()` returns `true` (so dual-pane renders), but `CanvasPane.razor` has no content to display.

**Root Cause:** Fluxor state has a non-null artifact, but the artifact's properties are empty (e.g., `CurrentRecipe` exists but has no name/ingredients).

**Fix:** Add empty state detection:

```razor
<!-- File: CanvasPane.razor -->

@if (ArtifactStore.Value.CurrentRecipe is not null)
{
    @if (string.IsNullOrEmpty(ArtifactStore.Value.CurrentRecipe.Name))
    {
        <!-- Empty state placeholder -->
        <div class="canvas-empty-state">
            <p>Recipe is being generated...</p>
            <Spinner />
        </div>
    }
    else
    {
        <RecipeEditor />
    }
}
```

---

### Q4: How do I know if my component should be tool-based or state-based?

**A:** Use this decision tree:

```
START: Where does data come from?
│
├─ Q1: Is data from a backend tool execution?
│  ├─ YES → Tool-based (register in ToolComponentRegistry)
│  │       → Renders from FunctionResultContent
│  │       → Example: WeatherDisplay (get_weather tool)
│  └─ NO → Continue to Q2
│
├─ Q2: Is data managed by Fluxor state (ArtifactState, PlanState)?
│  ├─ YES → State-based (no registry, inject IState<T>)
│  │       → Renders from component directly
│  │       → Example: RecipeEditor (ArtifactState.CurrentRecipe)
│  └─ NO → Continue to Q3
│
├─ Q3: Is data from parent component props?
│  └─ YES → Props-based (standard Blazor component)
│           → Example: Avatar, Badge, Button
│
END: Component architecture determined
```

**Key Point:** Canvas-pane components are typically **state-based** (Fluxor), while message-list tool results are **tool-based** (ToolComponentRegistry).

---

### Q5: What if I need to display observability info (logs, traces) in canvas-pane?

**A:** **Don't.** Observability and debugging components should always render in the **message-list** (inline, collapsible, contextual). The canvas-pane is for **user-facing collaborative artifacts**, not developer tools.

**Reasoning:**
- Observability is **informational metadata**, not editable content
- Users (non-developers) should not see debugging info occupying the canvas-pane
- Logs/traces are contextual to specific conversation turns, not persistent artifacts

**Exception:** If you're building a **debugging-focused agent** where the entire purpose is to collaborate on code/logs (e.g., AI-powered debugger), then a "Debug Session" artifact in canvas-pane might make sense. But for general-purpose agentic chat, observability stays in message-list.

---

### Q6: How do I test canvas-pane visibility with playwright-cli?

**A:** Use the `playwright-cli` skill for ad-hoc browser testing. Example workflow:

```bash
# Start the Blazor app (if not already running)
dotnet run --project AGUIDojoClient

# Open browser and navigate to chat page
playwright-cli open http://localhost:5000/chat

# Take screenshot of initial layout (no artifacts)
playwright-cli screenshot http://localhost:5000/chat initial-layout.png

# Interact with chat to create recipe
playwright-cli fill "input[placeholder='Type a message']" "Create a pasta recipe"
playwright-cli click "button[type='submit']"

# Wait for AI response and RecipeEditor to render
playwright-cli wait 5000

# Take screenshot of dual-pane layout
playwright-cli screenshot http://localhost:5000/chat dual-pane-with-recipe.png

# Verify canvas-pane is visible (check element exists)
playwright-cli exists ".canvas-pane"

# Verify RecipeEditor is rendered
playwright-cli exists "form[class*='recipe-editor']"
```

See `~/.copilot/skills/playwright-cli/SKILL.md` for full CLI reference.

---

### Q7: What happens if viewport resizes from desktop to mobile mid-session?

**A:** The `ViewportService` detects the resize via JSInterop and triggers component re-render. Layout transitions:

**Desktop → Mobile:**
1. Dual-pane layout dismounts
2. MobileLayout mounts with bottom drawer
3. Canvas-pane content (e.g., RecipeEditor) moves to drawer
4. Drawer is initially collapsed (user must tap to open)

**Mobile → Desktop:**
1. MobileLayout dismounts
2. DualPaneLayout mounts with ResizablePanelGroup
3. Drawer content moves to canvas-pane
4. Canvas-pane restores previous width from localStorage

**Implementation:** `DualPaneLayout.razor` has an initialization guard:

```csharp
if (!_isInitialized)
{
    // Wait for viewport detection before rendering
    _isInitialized = true;
    StateHasChanged();
}
```

This prevents layout jank from double-rendering.

---

### Q8: Can I hide canvas-pane temporarily and bring it back later?

**A:** Yes, via **Fluxor actions** to clear/restore artifact state.

**Example: Dismiss Recipe**

```csharp
// File: AGUIDojoClient/Store/ArtifactState/ArtifactActions.cs

public record ClearArtifactAction;

// File: AGUIDojoClient/Store/ArtifactState/ArtifactReducer.cs

[ReducerMethod]
public static ArtifactState Reduce(ArtifactState state, ClearArtifactAction action)
{
    return state with
    {
        CurrentRecipe = null,
        CurrentDocumentState = null,
        CurrentDiagram = null
    };
}
```

**Usage in RecipeEditor:**

```razor
<button @onclick="DismissRecipe">Dismiss Recipe</button>

@code {
    [Inject] private IDispatcher Dispatcher { get; set; } = default!;

    private void DismissRecipe()
    {
        Dispatcher.Dispatch(new ClearArtifactAction());
        // Canvas-pane will hide automatically (HasInteractiveArtifact() returns false)
    }
}
```

**Effect:** Canvas-pane hides, layout transitions to single-pane.

---

## Summary

**Golden Rule:** The canvas-pane is for **interactive shared artifacts** that require collaborative editing and dedicated workspace.

**Quick Checklist:**
- ✅ **Canvas-Pane:** RecipeEditor, MonacoEditor, DiagramEditor, FormBuilder (editable, stateful, shared, requires focus)
- ❌ **Message-List:** WeatherDisplay, PlanProgress, MemoryInspector, ToolExecutionTracker, Charts (read-only, transient, informational)

**When in Doubt:** Ask these 4 questions:
1. Can the user edit it?
2. Does state persist across turns?
3. Can AI also modify it?
4. Does it need dedicated space?

If all 4 are YES → Canvas-pane. Otherwise → Message-list.

---

**Document Version History:**

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-09 | Initial version (task-5) |

**Maintainer:** Ralph-v2-Executor  
**Reviewers:** Ralph-v2-Reviewer (pending)  
**Next Review:** After task-6, task-7, task-8 implementation
