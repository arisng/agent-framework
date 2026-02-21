# Design Spec: Responsive Canvas Pane Layout

## 1. Problem Statement
The current `CanvasPane` implementation suffers from unresponsive behavior when used within `DualPaneLayout`. Specifically:
1.  Child components (e.g., `RecipeEditor`) overflow the horizontal bounds of the pane when resized.
2.  Hardcoded `min-width` constraints in CSS conflict with the `ResizablePanel` logical sizing, causing overflow when the panel is dragged smaller than the CSS minimum.
3.  Blazor Blueprint components (Card, Tabs) introduce layout structures that require specific handling to maintain responsiveness (e.g., lack of `min-width: 0` in flex chains).

## 2. Layout Architecture

### 2.1. DualPaneLayout (Container)
*   **Goal:** Allow smooth resizing via drag handle without content overflow/clipping.
*   **Constraint Stratgy:**
    *   Remove rigid `min-width: 400px !important` on `.canvas-panel`.
    *   Rely on `ResizablePanel`'s `MinSize` (percentage-based) for logical minimums.
    *   Add `min-width: 0` to flex children to allow structural shrinking.
    *   Set `overflow: hidden` on pane containers to contain inner scroll areas.

### 2.2. CanvasPane (Content Area)
*   **Structure:**
    *   Flex column layout.
    *   Header (Plan Indicator) + Content (Tabs).
    *   **Crucial:** Intermediate `Tabs` wrappers must propagate flex/width constraints.
*   **Scrolling Strategy:**
    *   Use `.canvas-pane__scroll` as the primary scroll container.
    *   Apply `width: 100%`, `overflow-x: hidden` (to force wrap), `overflow-y: auto`.
    *   Apply `container-type: inline-size` to enable Container Queries for children.

### 2.3. Recipe Editor (Artifact Component)
*   **Responsive Behavior:**
    *   Must adapt to any width from 300px to 1000px+.
    *   **Breakpoints (Container Queries):**
        *   `< 500px`: Stack form controls ("Skill" and "Time") vertically.
        *   `> 500px`: Place controls side-by-side (Grid).
*   **Component Structure:**
    *   **Wrapper:** Wrap root `<Card>` in a `div` to properly receive Blazor scoped CSS (`::deep` fix).
    *   **Card:** `width: 100%`, `max-width: 100%`.
    *   **Inputs:** `min-width: 0` to prevent flex blowouts.

## 3. Implementation Steps

### Step 1: Fix `DualPaneLayout` Constraints
*   Edit `Components/Layout/DualPaneLayout.razor.css`.
*   Relax `min-width` on `.canvas-panel` and `.context-panel`.
*   Ensure `.single-pane-layout` and `.dual-pane-root` manage overflow correctly.

### Step 2: Reinforce `CanvasPane` Layout
*   Edit `Components/Layout/CanvasPane.razor.css`.
*   Ensure `.canvas-pane__scroll` has `overflow-x: hidden` to enforce wrapping.
*   Verify `Tabs` inner wrapper flex propagation.

### Step 3: Refactor `RecipeEditor` for Responsiveness
*   Edit `Components/SharedState/RecipeEditor.razor`.
    *   Wrap `<Card>` in `<div class="recipe-editor-wrapper">`.
*   Edit `Components/SharedState/RecipeEditor.razor.css`.
    *   Use `@container` queries for `.recipe-selects-row`.
    *   Ensure all flex children have `min-width: 0`.

## 4. Validation Plan
1.  **Narrow Pane Test:** Drag splitter to minimum size (~30%). verify `RecipeEditor` stacks inputs and does not scroll horizontally.
2.  **Wide Pane Test:** Drag splitter to maximum. Verify `RecipeEditor` uses grid layout.
3.  **Mobile View:** Verify behavior in simulated mobile viewport (though governed by `MobileLayout`, `RecipeEditor` styles are shared).
