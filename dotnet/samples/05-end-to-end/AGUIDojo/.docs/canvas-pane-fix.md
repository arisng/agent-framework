# Design Spec: CanvasPane Responsive Layout Fix

## 1. Problem Analysis
Users report that child components (specifically `RecipeEditor`) in the `CanvasPane` overflow horizontally or do not resize responsively when the pane is resized. This contrasts with `ContextPane`, which resizes correctly.

### Root Cause
The `CanvasPane` layout hierarchy involves deep nesting due to the `BlazorBlueprint` `Tabs` component. The chain of width constraint (`min-width: 0`, `max-width: 100%`) is likely broken by one of the intermediate elements, causing the content to define the width rather than the container.

**Key Structural Differences:**
- **ContextPane:** Direct child of `ResizablePanel`. Simple hierarchy.
- **CanvasPane:** `ResizablePanel` -> `CanvasPane` -> `Tabs` -> `Tabs (Inner Wrapper)` -> `TabsContent` -> `.canvas-pane__scroll` -> `RecipeEditor`.

Specific points of failure:
1. **BB Tabs Inner Wrapper:** The `Tabs` component renders an undocumented intermediate `<div>` that defaults to `display: block`. If this element does not have `min-width: 0`, it may expand to fit its content.
2. **TabsContent:** Needs to be explicitly constrained to preventing expanding beyond its parent.
3. **Scroll Container:** Must define a containment context for `@container` queries to work in children.

## 2. Solution Design

### 2.1 CSS Layout Strategy
We will enforce a strict "Flex Column + Min-Width 0" chain from the root of `CanvasPane` down to the leaf content.

**Hierarchy & Rules:**

1.  **`CanvasPane` (Root)**
    -   `display: flex`
    -   `flex-direction: column`
    -   `overflow: hidden`
    -   `min-width: 0` (Crucial for flex children shrinking)

2.  **`Tabs` Container (`.canvas-pane__tabs`)**
    -   `flex: 1`
    -   `display: flex`
    -   `flex-direction: column`
    -   `min-height: 0`
    -   `min-width: 0`
    -   `overflow: hidden`

3.  **`Tabs` Inner Wrapper (`.canvas-pane__tabs > div`)**
    -   Must target this specifically.
    -   `display: flex`
    -   `flex-direction: column`
    -   `flex: 1`
    -   `min-height: 0`
    -   `min-width: 0`
    -   `max-width: 100%`

4.  **`TabsContent` (`.canvas-pane__tab-content`)**
    -   `display: flex`
    -   `flex-direction: column`
    -   `flex: 1`
    -   `min-height: 0`
    -   `min-width: 0`
    -   `max-width: 100%`
    -   `overflow: hidden`

5.  **Scroll Container (`.canvas-pane__scroll`)**
    -   `display: flex`
    -   `flex-direction: column`
    -   `flex: 1`
    -   `width: 100%`
    -   `overflow-y: auto`
    -   `overflow-x: hidden`
    -   `container-type: inline-size` (Enables query for RecipeEditor)
    -   `container-name: canvas-scroll`

### 2.2 Component Updates

#### `CanvasPane.razor.css`
Update the generic `::deep` selectors to be more robust and strictly enforce `max-width: 100%` at every level.

```css
::deep .canvas-pane__tabs {
    max-width: 100%; /* Add this */
    /* ... existing rules */
}

::deep .canvas-pane__tabs > div {
    max-width: 100%; /* Add this */
    /* ... existing rules */
}

::deep .canvas-pane__tab-content {
    max-width: 100%; /* Add this */
    width: 100%;     /* Explicit width */
    /* ... existing rules */
}
```

#### `RecipeEditor.razor` & `.css`
Ensure the `RecipeEditor` respects the container.

-   **Wrapper**: `.recipe-editor-wrapper` should have `width: 100%` and `max-width: 100%`.
-   **Grid**: Verify CSS Grid uses `minmax(0, 1fr)` to allow shrinking.
-   **Toggle Group**: Ensure `flex-wrap: wrap` is active.

## 3. Implementation Steps
1.  **Refactor `CanvasPane.razor.css`**: Apply `max-width: 100%` and `min-width: 0` to all flex children in the hierarchy.
2.  **Verify `RecipeEditor`**: Ensure it consumes the container width properly.
3.  **Runtime Validation**: Resize `DualPaneLayout` and observe `RecipeEditor` grid switching from 2 columns to 1 column (via `@container` or media query) without overflowing.

## 4. Acceptance Criteria
-   `CanvasPane` does not trigger horizontal scrollbar on parent.
-   `RecipeEditor` content wraps/resizes when pane width is reduced.
-   No visual clipping of essential content (except standard text truncation).
