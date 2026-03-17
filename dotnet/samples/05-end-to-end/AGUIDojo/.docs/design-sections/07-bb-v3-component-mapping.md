# Design Section 07: BlazorBlueprint v3 Component Mapping

> **Spec Section:** BB v3 Migration & Component Reference for Session UX  
> **Created:** 2026-02-24  
> **Status:** Design  
> **References:** Q-BB-001, Q-BB-002, Q-BB-003, Q-BB-004, Q-BB-005, Q-SPEC-001, Q-SPEC-002, Q-SPEC-003, Q-SPEC-007, Q-SPEC-008, R8, ISS-005  
> **Depends On:** 01-unified-endpoint.md, 05-chat-ui-session-switcher.md, 06-agui-feature-integration.md  
> **Inherited By:** task-9

---

## Table of Contents

1. [Purpose](#1-purpose)
2. [Current State: BB v2.1.1 Baseline](#2-current-state-bb-v211-baseline)
3. [BB v2 → v3 Migration Checklist](#3-bb-v2--v3-migration-checklist)
4. [Component Mapping: Session UX → BB v3 Primitives](#4-component-mapping-session-ux--bb-v3-primitives)
5. [Chart Component Migration: ApexCharts → ECharts](#5-chart-component-migration-apexcharts--echarts)
6. [Portal Strategy: Two-Layer Architecture](#6-portal-strategy-two-layer-architecture)
7. [Scoped CSS Strategy for BB v3](#7-scoped-css-strategy-for-bb-v3)
8. [Input Timing Change Impact](#8-input-timing-change-impact)
9. [Complete BB v3 Component Reference](#9-complete-bb-v3-component-reference)
10. [DI Registration Changes](#10-di-registration-changes)
11. [Migration Risk Assessment](#11-migration-risk-assessment)
12. [Design Decisions Summary](#12-design-decisions-summary)

---

## 1. Purpose

This document bridges the UI design in [05-chat-ui-session-switcher.md](05-chat-ui-session-switcher.md) with concrete BlazorBlueprint v3 API usage. It serves three roles:

1. **Migration guide**: Step-by-step BB v2.1.1 → v3 checklist with impact analysis per change category
2. **Component mapping**: Maps every session UX element from section 05 to specific BB v3 component APIs and parameters
3. **Quick reference**: Canonical list of all BB v3 components used in the new spec, with import paths, key parameters, and gotchas

> **Ref:** Q-BB-001 — "BB v3 migration should be done FIRST because new spec code written against BB v2 APIs is immediately obsolete."

> **Ref:** ISS-005 — "BB section describes v2.x counts and descriptions. BB v3 has different component count, restructured namespaces, new components."

---

## 2. Current State: BB v2.1.1 Baseline

### 2.1 Installed Packages

From `Directory.Packages.props` (central package management):

```xml
<!-- BlazorBlueprint UI -->
<PackageVersion Include="BlazorBlueprint.Components" Version="2.1.1" />
<PackageVersion Include="BlazorBlueprint.Icons.Lucide" Version="2.0.0" />
```

> **Ref:** R8 — "Current version: BB v2.1.1 — confirmed via central package management."

### 2.2 Current _Imports.razor (BB-Related Lines)

The AGUIDojoClient `_Imports.razor` currently has **15 BB-specific using directives** targeting v2 sub-namespaces:

```razor
@using BlazorBlueprint.Components
@using BlazorBlueprint.Components.Badge
@using BlazorBlueprint.Components.Button
@using BlazorBlueprint.Components.Card
@using BlazorBlueprint.Components.DataTable
@using BlazorBlueprint.Components.DropdownMenu
@using BlazorBlueprint.Components.Input
@using BlazorBlueprint.Components.Label
@using BlazorBlueprint.Components.NativeSelect
@using BlazorBlueprint.Components.Pagination
@using BlazorBlueprint.Components.Select
@using BlazorBlueprint.Components.Separator
@using BlazorBlueprint.Components.Sidebar
@using BlazorBlueprint.Components.Toggle
@using BlazorBlueprint.Primitives.Table
@using BlazorBlueprint.Icons.Lucide
@using BlazorBlueprint.Icons.Lucide.Components
```

### 2.3 Current BB v2 Components in Use

A survey of all `.razor` files in AGUIDojoClient identifies these BB v2 components currently in use (without `Bb` prefix):

| BB v2 Component | Used In | Count |
|-----------------|---------|-------|
| `<SidebarProvider>`, `<Sidebar>`, `<SidebarHeader>`, `<SidebarContent>`, `<SidebarFooter>`, `<SidebarInset>`, `<SidebarTrigger>`, `<SidebarMenu>`, `<SidebarMenuItem>`, `<SidebarMenuButton>`, `<SidebarGroup>`, `<SidebarGroupLabel>` | Chat.razor, ChatHeader.razor | ~30 |
| `<Button>` | Multiple components | ~15 |
| `<Badge>` | ChatHeader, ChartDisplay, DataGridDisplay | ~8 |
| `<Card>`, `<CardHeader>`, `<CardContent>`, `<CardTitle>`, `<CardDescription>` | DiffPreview, RecipeEditor, CanvasPane | ~12 |
| `<Separator>` | ChatHeader, CanvasPane | ~4 |
| `<ScrollArea>` | CanvasPane, ChatMessageList | ~3 |
| `<DataTable>`, `<DataTableColumn>` | DataGridDisplay | ~2 |
| `<Tabs>`, `<TabsList>`, `<TabsTrigger>`, `<TabsContent>` | CanvasPane | ~8 |
| `<DropdownMenu>`, `<DropdownMenuTrigger>`, `<DropdownMenuContent>`, `<DropdownMenuItem>` | ChatHeader | ~4 |
| `<Select>`, `<SelectTrigger>`, `<SelectValue>`, `<SelectContent>`, `<SelectItem>` | ChatHeader (endpoint selector) | ~5 |
| `<Input>` | ChatInput, DynamicFormDisplay | ~4 |
| `<Label>` | DynamicFormDisplay | ~3 |
| `<Toggle>` | ChatHeader (theme) | ~2 |
| `<Tooltip>`, `<TooltipTrigger>`, `<TooltipContent>` | Various | ~6 |
| `<Dialog>`, `<DialogTrigger>`, `<DialogContent>`, `<DialogHeader>`, `<DialogTitle>`, `<DialogDescription>` | ApprovalDialog | ~6 |
| `<AlertDialog>` | (not currently used — will be added for session delete) | 0 |
| `LucideIcon` | Multiple components | ~20 |
| **Total BB component tags** | | **~130** |

---

## 3. BB v2 → v3 Migration Checklist

### 3.1 Overview

BB v3 introduces **6 breaking change categories**. Each is analyzed below with severity, scope, and migration steps.

| # | Change Category | Severity | Files Affected | Effort |
|---|----------------|----------|----------------|--------|
| M1 | [Component `Bb` Prefix](#31-m1-component-bb-prefix) | **High** | All `.razor` files (~20+) | Medium — bulk find-and-replace |
| M2 | [Namespace Flattening](#32-m2-namespace-flattening) | **High** | `_Imports.razor` + individual `.razor` files | Low — remove sub-namespace imports |
| M3 | [Chart Rewrite: ApexCharts → ECharts](#33-m3-chart-rewrite) | **High** | `ChartDisplay.razor`, chart models | Medium — complete API rewrite |
| M4 | [Portal Two-Layer Architecture](#34-m4-portal-two-layer-architecture) | **Medium** | `App.razor` or layout root | Low — add two `<BbPortalHost>` tags |
| M5 | [Input UpdateTiming Default Change](#35-m5-input-updatetiming-default) | **Medium** | `ChatInput.razor`, any `<Input>` usage | Low — add explicit parameter |
| M6 | [DI Registration API Change](#36-m6-di-registration-api) | **Low** | `Program.cs` | Trivial — one-line change |

### 3.1 M1: Component Bb Prefix

**What changed:** ALL Razor components now use a `Bb` prefix. This applies to every component tag across both `BlazorBlueprint.Components` (~300+ components) and `BlazorBlueprint.Primitives` (~65 components). Non-component types (enums, services, context classes, event args) are **unchanged**.

**Migration steps:**

1. **Bulk find-and-replace** in all `.razor` files:
   - `<ComponentName` → `<BbComponentName`
   - `</ComponentName>` → `</BbComponentName>`
2. Update `typeof()` and `nameof()` references in `.cs` code:
   - `builder.OpenComponent<Dialog>(0)` → `builder.OpenComponent<BbDialog>(0)`
3. `LucideIcon` component becomes `BbLucideIcon` (if part of the BB package) — verify whether Icons package also adopts prefix

**Scope in AGUIDojoClient:** ~130 component tags across ~20 `.razor` files (see §2.3)

**Example transformations:**

| v2 | v3 |
|----|-----|
| `<Button Variant="ButtonVariant.Primary">` | `<BbButton Variant="ButtonVariant.Primary">` |
| `<Badge Variant="BadgeVariant.Secondary">` | `<BbBadge Variant="BadgeVariant.Secondary">` |
| `<SidebarProvider>` | `<BbSidebarProvider>` |
| `<Dialog @bind-Open="_open">` | `<BbDialog @bind-Open="_open">` |
| `<ScrollArea Class="h-full">` | `<BbScrollArea Class="h-full">` |
| `<DataTable Data="items">` | `<BbDataTable Data="items">` |

**What does NOT change:**

| Type | Example | Changed? |
|------|---------|----------|
| Enums | `ButtonVariant`, `BadgeVariant`, `SheetSide` | No |
| Services | `ToastService`, `DialogService`, `IPortalService` | No |
| Context classes | `TooltipContext`, `DialogContext` | No |
| Event args | `TextChangeEventArgs`, `SelectionChangeEventArgs` | No |
| RenderFragment params | `ChildContent`, `Icon`, `LoadingTemplate` | No |

### 3.2 M2: Namespace Flattening

**What changed:** All 77 `BlazorBlueprint.Components.*` sub-namespaces consolidated into the root `BlazorBlueprint.Components` namespace. 8 consumer-facing types from `BlazorBlueprint.Primitives.*` promoted to `BlazorBlueprint.Primitives`.

**Migration steps:**

1. Replace all 15 BB using directives in `_Imports.razor` with **2 lines**:
   ```razor
   @using BlazorBlueprint.Components
   @using BlazorBlueprint.Primitives
   ```
2. Remove per-file `@using BlazorBlueprint.Components.*` imports scattered in individual `.razor` files (e.g., `ChartDisplay.razor` has `@using BlazorBlueprint.Components.Badge`)
3. In `.cs` files: replace `using BlazorBlueprint.Components.Extensions` with `using BlazorBlueprint.Components`

**Impact:** Removes 13 unnecessary `@using` lines from `_Imports.razor`. Simplifies future component additions — no need to remember which sub-namespace a component lives in.

**New `_Imports.razor` (BB section):**

```razor
@using BlazorBlueprint.Components
@using BlazorBlueprint.Primitives
@using BlazorBlueprint.Icons.Lucide
@using BlazorBlueprint.Icons.Lucide.Components
```

### 3.3 M3: Chart Rewrite

**What changed:** ApexCharts integration completely removed and replaced with ECharts-based declarative composition API. This is the **largest single breaking change** in BB v3.

See [§5 Chart Component Migration](#5-chart-component-migration-apexcharts--echarts) for full details.

### 3.4 M4: Portal Two-Layer Architecture

**What changed:** The single `<PortalHost>` is replaced by a two-layer system:
- `<BbContainerPortalHost>`: For components that render inline (popovers, dropdowns, selects, tooltips)
- `<BbOverlayPortalHost>`: For components that overlay the entire viewport (dialogs, sheets, drawers, alert dialogs)

**Migration steps:**

1. Replace single `<PortalHost />` in the layout root (`App.razor` or `MainLayout.razor`) with:
   ```razor
   <BbContainerPortalHost />
   <BbOverlayPortalHost />
   ```
2. No changes needed in individual component usage — the portal routing is automatic based on component type

> **Ref:** Q-SPEC-007 — "BB v3's two-layer portal architecture: `PortalCategory.Container` vs `PortalCategory.Overlay`, separate `BbContainerPortalHost`/`BbOverlayPortalHost`"

**Impact on session UX:**
- `SessionSearchDialog.razor` (`BbCommandDialog`) → routes to `BbOverlayPortalHost` automatically
- `DeleteConfirmationDialog.razor` (`BbAlertDialog`) → routes to `BbOverlayPortalHost` automatically
- `BbDropdownMenu` (session actions) → routes to `BbContainerPortalHost` automatically
- `BbTooltip` (collapsed sidebar icon tooltips) → routes to `BbContainerPortalHost` automatically

### 3.5 M5: Input UpdateTiming Default

**What changed:** The `UpdateTiming` parameter for `<BbInput>` (and related input components) changed its default from `UpdateTiming.Immediate` to `UpdateTiming.OnChange`.

- **`Immediate`**: Fires `@bind-Value` on every keystroke
- **`OnChange`**: Fires `@bind-Value` only when the input loses focus (blur)

See [§8 Input Timing Change Impact](#8-input-timing-change-impact) for full analysis.

### 3.6 M6: DI Registration API

**What changed:** Service registration consolidated into a single extension method.

**v2:**
```csharp
builder.Services.AddBlazorBlueprintComponents();
// or manual registration of individual services
```

**v3:**
```csharp
builder.Services.AddBlazorBlueprintComponents();
```

The method name is unchanged but the internal wiring now includes `DialogService`, `ToastService`, and portal services automatically. Previously, some services required separate registration.

**Migration step:** Verify the existing `AddBlazorBlueprintComponents()` call in `Program.cs` — it should work as-is, but confirm that toast and dialog services are registered without additional calls.

---

## 4. Component Mapping: Session UX → BB v3 Primitives

This section maps each UI element from [05-chat-ui-session-switcher.md](05-chat-ui-session-switcher.md) to specific BB v3 components with their key parameters.

### 4.1 Session Sidebar

**UI Element:** Collapsible session list sidebar (05 §1, §2)

| Sub-Element | BB v3 Component | Key Parameters | Notes |
|-------------|----------------|----------------|-------|
| Sidebar wrapper | `<BbSidebarProvider>` | — | Wraps entire page layout; provides sidebar context |
| Sidebar container | `<BbSidebar>` | `Collapsible="icon"` | Supports collapse to icon-only (48px) on desktop, drawer overlay on mobile |
| Sidebar header | `<BbSidebarHeader>` | — | Contains brand + search trigger |
| Sidebar content | `<BbSidebarContent>` | — | Scrollable main section |
| Sidebar footer | `<BbSidebarFooter>` | — | Always visible; "New Chat" + theme toggle |
| Sidebar inset | `<BbSidebarInset>` | — | Contains the session context header + DualPaneLayout |
| Sidebar toggle | `<BbSidebarTrigger>` | — | Hamburger icon; collapse/expand on desktop, drawer toggle on mobile |
| Session group | `<BbSidebarGroup>` | — | Container for session list with label |
| Session group label | `<BbSidebarGroupLabel>` | — | "Sessions" heading |
| Session menu | `<BbSidebarMenu>` | — | List container for menu items |
| Session item | `<BbSidebarMenuItem>` | — | Individual session row |
| Session button | `<BbSidebarMenuButton>` | `IsActive="@isActive"` | Clickable session row with active highlight |

**Pattern reference:** BB Chat App blueprint (`blazorblueprintui.com/blueprints/apps/app-chat`)

### 4.2 Session List Scrolling

**UI Element:** Scrollable session list within sidebar (05 §2.1)

| Sub-Element | BB v3 Component | Key Parameters | Notes |
|-------------|----------------|----------------|-------|
| Scroll container | `<BbScrollArea>` | — | Wraps session list inside `BbSidebarMenu` |

**Known gotcha (from repo memory):** BB `<ScrollArea>` renders an internal content wrapper with `display: table; min-width: 100%`, which defeats CSS Grid `minmax(0, 1fr)` constraints. In the session sidebar context this is **not an issue** because the sidebar has fixed width (280px), not Grid-based layout. However, if `BbScrollArea` is used inside grid-based canvas pane content, use a plain `<div style="overflow-y: auto">` instead.

### 4.3 Session Search Dialog

**UI Element:** Cmd+K session search overlay (05 §2.4, §9.2)

| Sub-Element | BB v3 Component | Key Parameters | Notes |
|-------------|----------------|----------------|-------|
| Dialog container | `<BbCommandDialog>` | `@bind-Open="@_isSearchOpen"` | Full-screen overlay with search input + results |
| Search input | `<BbCommandInput>` | `Placeholder="Search sessions..."` | Auto-focused; built-in keyboard nav |
| Session results group | `<BbCommandGroup>` | `Heading="Sessions"` | Filtered session list |
| Individual session result | `<BbCommandItem>` | `@onclick` | Shows status icon + title + timestamp |
| Actions group | `<BbCommandGroup>` | `Heading="Actions"` | Static: "New Chat" with shortcut hint |
| Empty state | `<BbCommandEmpty>` | — | "No sessions found" fallback |

**Keyboard integration:**
- `Ctrl+K` opens the dialog (registered via JS interop in `Chat.razor`)
- `↑/↓` navigates results (built-in `BbCommandDialog` behavior)
- `Enter` selects → dispatches `SetActiveSessionAction` → dialog closes
- `Escape` closes dialog (built-in behavior)

### 4.4 Session Status Badges

**UI Element:** Status indicators on session list items (05 §3.1, §3.4, §3.5)

| Badge Type | BB v3 Component | Key Parameters | Usage |
|------------|----------------|----------------|-------|
| Unread count | `<BbBadge>` | `Variant="BadgeVariant.Secondary"`, `Class="ml-auto h-5 px-1.5 text-[10px]"` | Shows when `UnreadCount > 0` |
| Approval required | `<BbBadge>` | `Variant="BadgeVariant.Destructive"`, `Class="ml-auto h-5 px-1.5 text-[10px] animate-pulse"` | Shows when `HasPendingApproval` |
| Agent status pill | `<BbBadge>` | Variant varies by state (see table below) | Inset header — shows agent state |
| Chart type badge | `<BbBadge>` | `Variant="BadgeVariant.Secondary"` | ChartDisplay — shows chart type label |

**Agent Status Pill Variants (inset header, 05 §5.4):**

| Session State | Pill Text | `BadgeVariant` | Extra Class |
|--------------|-----------|----------------|-------------|
| `IsRunning == true` | "Generating..." | `Default` | — (spinner icon) |
| `Completed` | "Ready" | `Secondary` | — |
| `Error` | "Error" | `Destructive` | — |
| `HasPendingApproval` | "Approval Required" | `Destructive` | `animate-pulse` |
| `Created` | "New" | `Outline` | — |

### 4.5 Session Status Icons

**UI Element:** Per-session status icon in sidebar (05 §4)

| Icon | BB v3 Component | Key Parameters |
|------|----------------|----------------|
| All status icons | `<LucideIcon>` | `Name="icon-name"`, `Size="16"`, `Class="color-class"` |

The `LucideIcon` component is from `BlazorBlueprint.Icons.Lucide` (separate package). Verify if BB v3 adds the `Bb` prefix to icon components — if so, use `<BbLucideIcon>`. The icon name mapping (05 §4.1):

| SessionStatus | Lucide Icon | Color Class | Animation Class |
|--------------|-------------|-------------|-----------------|
| `Created` | `circle` | `text-muted-foreground/40` | — |
| `Active` | `circle` | `text-primary` | — |
| `Streaming` | `loader` | `text-primary` | `animate-spin` |
| `Background` | `loader` | `text-muted-foreground` | `animate-spin` |
| `Completed` | `check-circle` | `text-green-500` | — |
| `Error` | `alert-triangle` | `text-destructive` | — |
| `NeedsApproval`* | `bell` | `text-amber-500` | `animate-pulse` |

*Derived visual state from `HasPendingApproval`, not a real `SessionStatus` enum value.

### 4.6 Delete Confirmation Dialog

**UI Element:** Session deletion confirmation (05 §5.5)

**Option A — Declarative `BbAlertDialog` (in-template):**

| Sub-Element | BB v3 Component | Key Parameters |
|-------------|----------------|----------------|
| Dialog wrapper | `<BbAlertDialog>` | `@bind-Open="_showDeleteConfirm"` |
| Trigger | `<BbAlertDialogTrigger>` | — (wraps dropdown menu item) |
| Content | `<BbAlertDialogContent>` | — |
| Header | `<BbAlertDialogHeader>` | — |
| Title | `<BbAlertDialogTitle>` | — ("Delete session?") |
| Description | `<BbAlertDialogDescription>` | — ("This will permanently delete...") |
| Footer | `<BbAlertDialogFooter>` | — |
| Cancel button | `<BbAlertDialogCancel>` | — ("Cancel") |
| Confirm button | `<BbAlertDialogAction>` | `Class="bg-destructive text-destructive-foreground"` ("Delete") |

**Option B — Programmatic `DialogService.Confirm()` (new in BB v3):**

```csharp
var confirmed = await DialogService.Confirm(
    title: "Delete session?",
    description: $"This will permanently delete '{session.Title}' and all messages. This cannot be undone.",
    confirmText: "Delete",
    cancelText: "Cancel"
);
if (confirmed)
{
    Dispatcher.Dispatch(new DestroySessionAction(sessionId));
}
```

> **Ref:** Q-BB-003 — "Add `BbDialogProvider` for programmatic confirm dialogs (session delete)."

**Recommendation:** Use **Option B** (`DialogService.Confirm()`) for session deletion. It is more concise, doesn't require template markup, and integrates cleanly with the dropdown menu item click handler. Requires `<BbDialogProvider>` in the layout root (see §6).

### 4.7 Notification Toasts

**UI Element:** Background session completion notifications (04 §7)

| Sub-Element | BB v3 Component | Key Parameters |
|-------------|----------------|----------------|
| Toast provider | `<BbToastProvider>` | Placed in layout root |
| Toast trigger (programmatic) | `ToastService` | Injected into notification-handling components |

**Semantic toast variants (BB v3):**

| Notification Type | ToastService Method | Visual |
|-------------------|-------------------|--------|
| Session completed | `ToastService.Success(...)` | Green checkmark toast |
| Approval required | `ToastService.Warning(...)` | Amber bell toast with "Go to session" action |
| Session error | `ToastService.Error(...)` | Red alert toast |
| General info | `ToastService.Info(...)` | Blue info toast |

BB v3 `BbToastProvider` enhancements over v2:
- Semantic variants (`Success`, `Warning`, `Info`, `Error`) with pre-styled icons
- Per-toast positioning support
- Pause-on-hover (auto-dismiss timer pauses when mouse hovers the toast)
- Action buttons within toasts (e.g., "Go to session" dispatches `SetActiveSessionAction`)

### 4.8 Session Actions Dropdown

**UI Element:** "..." dropdown in inset header (05 §5.5)

| Sub-Element | BB v3 Component | Key Parameters |
|-------------|----------------|----------------|
| Dropdown wrapper | `<BbDropdownMenu>` | — |
| Trigger | `<BbDropdownMenuTrigger>` | — |
| Trigger button | `<BbButton>` | `Variant="ButtonVariant.Ghost"`, `Size="ButtonSize.Icon"` |
| Content panel | `<BbDropdownMenuContent>` | `Align="PopoverAlign.End"` |
| Section label | `<BbDropdownMenuLabel>` | "Session Actions" |
| Separator | `<BbDropdownMenuSeparator>` | — |
| Menu item | `<BbDropdownMenuItem>` | `@onclick`, optional `Class="text-destructive"` for delete |

### 4.9 Inset Header Components

**UI Element:** Session context bar above chat area (05 §5.2, §5.3)

| Sub-Element | BB v3 Component | Notes |
|-------------|----------------|-------|
| Sidebar toggle | `<BbSidebarTrigger>` | Collapse/expand sidebar |
| Vertical divider | `<BbSeparator>` | `Orientation="Orientation.Vertical"`, `Class="h-6"` |
| Agent status pill | `<BbBadge>` | Variant driven by session state (see §4.4) |
| Canvas toggle | `<BbButton>` | `Variant="ButtonVariant.Ghost"`, `Size="ButtonSize.Icon"` (existing) |

### 4.10 Existing Components (Data Source Change Only)

These components retain their BB v3 markup (with prefix added) but change their **data source** from global Fluxor stores to session-keyed selectors:

| Component | BB v3 Components Used | Data Change |
|-----------|----------------------|-------------|
| `ContextPane.razor` | `<BbScrollArea>` | `ChatStore.Value.Messages` → `SessionSelectors.ActiveMessages(state)` |
| `CanvasPane.razor` | `<BbTabs>`, `<BbTabsList>`, `<BbTabsTrigger>`, `<BbTabsContent>` | `ArtifactStore` → `SessionSelectors.ActiveSessionState(state)` |
| `ChatInput.razor` | `<BbInput>` (or `<BbTextarea>`) | Add `UpdateTiming="UpdateTiming.Immediate"` (see §8) |
| `ApprovalDialog.razor` | `<BbDialog>`, `<BbDialogContent>`, `<BbButton>` | Session-scoped approval state |
| `DataGridDisplay.razor` | `<BbDataTable>`, `<BbDataTableColumn>` | Session-scoped data |
| `DynamicFormDisplay.razor` | `<BbInput>`, `<BbLabel>`, `<BbButton>` | Session-scoped form data |

---

## 5. Chart Component Migration: ApexCharts → ECharts

### 5.1 Impact Assessment

BB v3 **completely replaced** the ApexCharts integration with an ECharts-based declarative composition API. This is the **largest single breaking change** and affects:

- `ChartDisplay.razor` — currently uses custom HTML table rendering (not ApexCharts directly)
- `ChartResult` model — may need property mapping changes
- Any future chart generative UI in the unified spec

> **Ref:** Q-SPEC-003 — "BB v3 completely replaced ApexCharts with ECharts using a new declarative composition API (`<BbBarChart>`, `<BbXAxis>`, `<BbLine>`, etc.)."

### 5.2 Current State: ChartDisplay.razor

The current `ChartDisplay.razor` does **not** use ApexCharts components directly. It renders charts as styled HTML tables with visual bar indicators. This means the ApexCharts → ECharts migration has **lower immediate impact** than expected — the existing spec's ApexCharts code samples are the primary concern, not the running codebase.

However, the new spec should design chart rendering using BB v3's ECharts API for proper interactive charts, replacing the current table-based fallback.

### 5.3 BB v3 ECharts API

The new chart API uses a **declarative composition pattern** with DataKey strings instead of lambda expressions:

**v2 (ApexCharts — from existing spec code samples):**
```razor
<AreaChart TItem="SalesData" Options="@_chartOptions">
    <ApexPointSeries TItem="SalesData" Items="@_data"
        Name="Revenue" SeriesType="SeriesType.Area"
        XValue="@(d => d.Month)" YValue="@(d => d.Revenue)" />
</AreaChart>
```

**v3 (ECharts — new API):**
```razor
<BbBarChart Data="@_chartData">
    <BbXAxis DataKey="month" />
    <BbBar DataKey="revenue" Fill="var(--chart-1)" Radius="4" />
    <BbTooltip />
    <BbCartesianGrid />
</BbBarChart>
```

### 5.4 Key Differences

| Aspect | ApexCharts (v2) | ECharts (v3) |
|--------|----------------|--------------|
| Data binding | Lambda expressions (`XValue="@(d => d.Month)"`) | String DataKey (`DataKey="month"`) |
| Type parameter | `TItem="SalesData"` on chart + series | No type parameter; data is `IEnumerable<IDictionary<string, object>>` or JSON-serializable objects |
| Series composition | `<ApexPointSeries>` child components | `<BbBar>`, `<BbLine>`, `<BbArea>` child components |
| Axes | Implicit from series | Explicit `<BbXAxis>`, `<BbYAxis>` |
| Tooltips | Via `ApexChartOptions` | `<BbTooltip>` child component |
| Grid lines | Via `ApexChartOptions` | `<BbCartesianGrid>` child component |
| Chart types | `SeriesType` enum | Dedicated components: `<BbBarChart>`, `<BbLineChart>`, `<BbAreaChart>`, `<BbPieChart>`, `<BbRadarChart>`, `<BbRadialChart>` |
| Coloring | ApexCharts color array | CSS variables (`var(--chart-1)` through `var(--chart-5)`) |

### 5.5 Migration Strategy for ChartDisplay

1. **Transform `ChartResult` data** to ECharts-compatible format (list of dictionaries with string keys)
2. **Map chart types** to BB v3 chart components:
   - `"bar"` → `<BbBarChart>`
   - `"line"` → `<BbLineChart>`
   - `"area"` → `<BbAreaChart>`
   - `"pie"` → `<BbPieChart>`
3. **Map datasets** to series components (`<BbBar>`, `<BbLine>`, `<BbArea>`)
4. **Use theme CSS variables** for coloring (`var(--chart-1)` through `var(--chart-5)`)
5. **Add axes and grid** as explicit child components

### 5.6 Chart Migration Example

**Before (custom HTML table in ChartDisplay.razor):**
```razor
<table class="chart-table">
  <thead><tr><th>Label</th>@foreach dataset... <th>@dataset.Name</th></tr></thead>
  <tbody>@for each label... <td><div class="chart-bar" style="width:@barWidth%">@value</div></td></tbody>
</table>
```

**After (BB v3 ECharts):**
```razor
@switch (Chart.ChartType.ToLowerInvariant())
{
    case "bar":
        <BbBarChart Data="@GetChartData()">
            <BbXAxis DataKey="label" />
            @for (int i = 0; i < Chart.Datasets.Count; i++)
            {
                <BbBar DataKey="@Chart.Datasets[i].Name" Fill="var(--chart-@(i+1))" Radius="4" />
            }
            <BbTooltip />
            <BbCartesianGrid />
        </BbBarChart>
        break;
    case "line":
        <BbLineChart Data="@GetChartData()">
            <BbXAxis DataKey="label" />
            @for (int i = 0; i < Chart.Datasets.Count; i++)
            {
                <BbLine DataKey="@Chart.Datasets[i].Name" Stroke="var(--chart-@(i+1))" />
            }
            <BbTooltip />
            <BbCartesianGrid />
        </BbLineChart>
        break;
    case "pie":
        <BbPieChart Data="@GetPieData()">
            <BbPie DataKey="value" NameKey="label" />
            <BbTooltip />
        </BbPieChart>
        break;
}
```

---

## 6. Portal Strategy: Two-Layer Architecture

### 6.1 BB v3 Portal Model

BB v3 replaces the single `<PortalHost>` with a two-layer system that separates inline-positioned components from viewport-overlaying components:

| Portal Layer | Host Component | Purpose | Components Routed Here |
|--------------|---------------|---------|----------------------|
| **Container** | `<BbContainerPortalHost>` | Inline positioning relative to trigger (popover, dropdown, tooltip) | `BbDropdownMenu`, `BbSelect`, `BbCombobox`, `BbTooltip`, `BbPopover`, `BbHoverCard` |
| **Overlay** | `<BbOverlayPortalHost>` | Full-viewport overlay with backdrop (modal, drawer) | `BbDialog`, `BbAlertDialog`, `BbSheet`, `BbCommandDialog`, `BbDrawer` |

### 6.2 Layout Root Setup

In `App.razor` or `MainLayout.razor`, add both portal hosts **after** the main content:

```razor
@* Main app content *@
<Routes />

@* BB v3 portal hosts *@
<BbContainerPortalHost />
<BbOverlayPortalHost />

@* BB v3 service providers *@
<BbToastProvider />
<BbDialogProvider />
```

### 6.3 Portal Routing in Session UX

All portal routing is **automatic** — BB v3 components self-register with the correct portal host based on their type:

| Session UX Component | Portal Route | Rationale |
|---------------------|-------------|-----------|
| `BbCommandDialog` (session search) | Overlay | Full-screen search overlay with backdrop |
| `BbAlertDialog` (delete confirmation) | Overlay | Modal dialog with backdrop |
| `BbDropdownMenu` (session actions) | Container | Positioned relative to "..." button |
| `BbTooltip` (collapsed sidebar item labels) | Container | Positioned relative to icon |
| `BbDialog` (approval dialog) | Overlay | Modal dialog with backdrop |
| `BbSelect` (endpoint selector — removed) | N/A | Removed in unified endpoint design |

### 6.4 `BbDialogProvider` — New in BB v3

The `<BbDialogProvider>` enables programmatic dialog APIs — `DialogService.Confirm()`, `DialogService.Alert()`, `DialogService.Custom<T>()` — without requiring declarative dialog markup in the template. It must be placed in the layout root alongside `BbToastProvider`.

**Used for:** Session delete confirmation (§4.6 Option B), potential future confirmation dialogs.

---

## 7. Scoped CSS Strategy for BB v3

### 7.1 Known Issues (from Repository Memory)

Several verified gotchas exist when using Blazor scoped CSS (`.razor.css` files) with BlazorBlueprint components:

| Issue | Root Cause | Fix |
|-------|-----------|-----|
| **BB root elements don't receive scope attribute** | When a BB component (e.g., `<Card>`, `<Dialog>`) is the ROOT element of a `.razor` file, the rendered HTML `<div>` does NOT get the `b-XXXX` scope attribute. CSS rules like `.my-class[b-XXXX]` silently fail. | Wrap BB components in a plain `<div>` to get the scope attribute. |
| **`::deep` fails on BB component root** | `::deep .my-class` compiles to `[b-XXXX] .my-class` (descendant selector). If `.my-class` is on the SAME element as `[b-XXXX]`, the descendant selector doesn't match. | Use wrapper `<div>` pattern; apply classes to the wrapper, not the BB component. |
| **ScrollArea `display:table` breaks Grid** | BB `<ScrollArea>` renders an internal wrapper with `display: table; min-width: 100%`, which defeats CSS Grid `minmax(0, 1fr)` constraints. | Replace `<BbScrollArea>` with `<div style="overflow-y: auto">` inside grid layouts. |
| **Tabs intermediate wrapper div** | BB `<Tabs>` renders an intermediate `<div>` with no class between the outer container and ChildContent, breaking flex height chains. | Target with `::deep .your-tabs-class > div` and set flex properties. |

### 7.2 BB v3 Scoped CSS Guidelines

These guidelines apply to all new components in the session UX:

**Rule 1: Never use a BB component as the root element of a `.razor` file when scoped CSS is needed.**

```razor
@* ❌ BAD — BbCard root won't get b-XXXX *@
<BbCard Class="session-card">
    <BbCardContent>...</BbCardContent>
</BbCard>

@* ✅ GOOD — wrapper div gets b-XXXX *@
<div class="session-card-wrapper">
    <BbCard>
        <BbCardContent>...</BbCardContent>
    </BbCard>
</div>
```

**Rule 2: Use `::deep` only to target descendants of elements with the scope attribute.**

```css
/* ✅ Works — .wrapper has b-XXXX, .inner is a descendant */
.wrapper ::deep .inner-bb-class { ... }

/* ❌ Fails — if .bb-root IS the element with b-XXXX */
::deep .bb-root { ... }
```

**Rule 3: For BB Tabs flex layouts, target the intermediate wrapper.**

```css
/* Target the invisible intermediate div inside BbTabs */
::deep .my-tabs-class > div {
    display: flex;
    flex-direction: column;
    flex: 1 1 0%;
    min-height: 0;
}
```

**Rule 4: Avoid `<BbScrollArea>` inside CSS Grid layouts.**

Use `<div style="overflow-y: auto">` instead when the scroll container is inside a CSS Grid cell with `minmax(0, 1fr)` column constraints.

### 7.3 BB v3 Migration Impact on Scoped CSS

The `Bb` prefix change does **not** affect scoped CSS behavior — the same root-element scope attribute issue exists in both v2 and v3. The structural HTML rendered by BB components (intermediate divs, display:table wrappers) is also unchanged in v3. The guidelines above remain applicable.

**Action during migration:** Audit all existing `.razor.css` files for patterns matching the known issues. No new issues are expected from the prefix change alone.

---

## 8. Input Timing Change Impact

### 8.1 What Changed

BB v3 changed the default `UpdateTiming` for all input components from `Immediate` to `OnChange`:

| Mode | Behavior | When `@bind-Value` fires |
|------|----------|-------------------------|
| `UpdateTiming.Immediate` | Real-time updates | Every keystroke |
| `UpdateTiming.OnChange` (new default) | Deferred updates | On focus loss (blur) |

> **Ref:** Q-BB-005 — "BB v3 changes `UpdateTiming` default from `Immediate` to `OnChange`. Chat input needs immediate updates for real-time typing."

### 8.2 Affected Components

| Component | Input Component | Current Behavior | Impact |
|-----------|----------------|-----------------|--------|
| `ChatInput.razor` | `<Input>` → `<BbInput>` | User types → value updates per keystroke → Send button enabled/disabled reactively | **High**: Without `Immediate`, the input value won't update until blur. Send button state, character count, and typing indicators break. |
| `DynamicFormDisplay.razor` | `<Input>` → `<BbInput>` | Form field binding | **Low**: Form fields typically use `OnChange` (submit-on-blur is standard). No change needed. |
| `SessionSearchDialog.razor` (new) | `<BbCommandInput>` | Search filtering | **None**: `BbCommandInput` is internal to `BbCommandDialog` and handles its own timing. |

### 8.3 Required Fix

Add explicit `UpdateTiming` to `ChatInput.razor`:

```razor
@* Before (v2 — Immediate was the default) *@
<Input @bind-Value="_messageText" Placeholder="Type a message..." />

@* After (v3 — must explicitly opt into Immediate) *@
<BbInput @bind-Value="_messageText"
         UpdateTiming="UpdateTiming.Immediate"
         Placeholder="Type a message..." />
```

**Scope:** Only `ChatInput.razor` needs this change. All other input usages work correctly with `OnChange` default.

---

## 9. Complete BB v3 Component Reference

All BB v3 components used in the new unified spec, organized by functional area.

### 9.1 Layout Components

| Component | Import | Key Parameters | Used In |
|-----------|--------|----------------|---------|
| `<BbSidebarProvider>` | `BlazorBlueprint.Components` | — | `Chat.razor` |
| `<BbSidebar>` | `BlazorBlueprint.Components` | `Collapsible="icon"` | `SessionSidebar.razor` |
| `<BbSidebarHeader>` | `BlazorBlueprint.Components` | — | `SessionSidebar.razor` |
| `<BbSidebarContent>` | `BlazorBlueprint.Components` | — | `SessionSidebar.razor` |
| `<BbSidebarFooter>` | `BlazorBlueprint.Components` | — | `SessionSidebar.razor` |
| `<BbSidebarInset>` | `BlazorBlueprint.Components` | — | `Chat.razor` |
| `<BbSidebarTrigger>` | `BlazorBlueprint.Components` | — | Inset header |
| `<BbSidebarGroup>` | `BlazorBlueprint.Components` | — | `SessionSidebar.razor` |
| `<BbSidebarGroupLabel>` | `BlazorBlueprint.Components` | — | `SessionSidebar.razor` |
| `<BbSidebarMenu>` | `BlazorBlueprint.Components` | — | `SessionSidebar.razor` |
| `<BbSidebarMenuItem>` | `BlazorBlueprint.Components` | — | `SessionListItem.razor` |
| `<BbSidebarMenuButton>` | `BlazorBlueprint.Components` | `IsActive`, `@onclick` | `SessionListItem.razor` |

### 9.2 Data Display Components

| Component | Import | Key Parameters | Used In |
|-----------|--------|----------------|---------|
| `<BbBadge>` | `BlazorBlueprint.Components` | `Variant` (Secondary, Destructive, Outline, Default) | Session badges, status pills, chart labels |
| `<BbCard>`, `<BbCardHeader>`, `<BbCardContent>`, `<BbCardTitle>`, `<BbCardDescription>` | `BlazorBlueprint.Components` | — | DiffPreview, RecipeEditor, CanvasPane |
| `<BbDataTable>`, `<BbDataTableColumn>` | `BlazorBlueprint.Components` | `Data`, `Columns`, `ShowPagination` | DataGridDisplay |
| `<BbSeparator>` | `BlazorBlueprint.Components` | `Orientation` | Inset header, layout dividers |

### 9.3 Chart Components (NEW in v3)

| Component | Import | Key Parameters | Used In |
|-----------|--------|----------------|---------|
| `<BbBarChart>` | `BlazorBlueprint.Components` | `Data` | ChartDisplay (bar charts) |
| `<BbLineChart>` | `BlazorBlueprint.Components` | `Data` | ChartDisplay (line charts) |
| `<BbAreaChart>` | `BlazorBlueprint.Components` | `Data` | ChartDisplay (area charts) |
| `<BbPieChart>` | `BlazorBlueprint.Components` | `Data` | ChartDisplay (pie charts) |
| `<BbBar>` | `BlazorBlueprint.Components` | `DataKey`, `Fill`, `Radius` | Chart series |
| `<BbLine>` | `BlazorBlueprint.Components` | `DataKey`, `Stroke` | Chart series |
| `<BbArea>` | `BlazorBlueprint.Components` | `DataKey`, `Fill`, `Stroke` | Chart series |
| `<BbPie>` | `BlazorBlueprint.Components` | `DataKey`, `NameKey` | Pie chart |
| `<BbXAxis>` | `BlazorBlueprint.Components` | `DataKey` | Chart axis |
| `<BbYAxis>` | `BlazorBlueprint.Components` | — | Chart axis |
| `<BbTooltip>` (chart) | `BlazorBlueprint.Components` | — | Chart tooltip |
| `<BbCartesianGrid>` | `BlazorBlueprint.Components` | — | Chart grid lines |

### 9.4 Input Components

| Component | Import | Key Parameters | Used In |
|-----------|--------|----------------|---------|
| `<BbInput>` | `BlazorBlueprint.Components` | `@bind-Value`, `UpdateTiming`, `Placeholder` | ChatInput, DynamicFormDisplay |
| `<BbLabel>` | `BlazorBlueprint.Components` | — | DynamicFormDisplay |
| `<BbButton>` | `BlazorBlueprint.Components` | `Variant`, `Size`, `@onclick` | Multiple (send, new chat, actions) |
| `<BbToggle>` | `BlazorBlueprint.Components` | `@bind-Pressed` | Theme toggle |

### 9.5 Navigation & Overlay Components

| Component | Import | Key Parameters | Used In |
|-----------|--------|----------------|---------|
| `<BbCommandDialog>` | `BlazorBlueprint.Components` | `@bind-Open` | SessionSearchDialog |
| `<BbCommandInput>` | `BlazorBlueprint.Components` | `Placeholder` | SessionSearchDialog |
| `<BbCommandGroup>` | `BlazorBlueprint.Components` | `Heading` | SessionSearchDialog |
| `<BbCommandItem>` | `BlazorBlueprint.Components` | `@onclick` | SessionSearchDialog |
| `<BbCommandEmpty>` | `BlazorBlueprint.Components` | — | SessionSearchDialog |
| `<BbDropdownMenu>` | `BlazorBlueprint.Components` | — | Session actions menu |
| `<BbDropdownMenuTrigger>` | `BlazorBlueprint.Components` | — | Session actions menu |
| `<BbDropdownMenuContent>` | `BlazorBlueprint.Components` | `Align` | Session actions menu |
| `<BbDropdownMenuLabel>` | `BlazorBlueprint.Components` | — | Session actions menu |
| `<BbDropdownMenuSeparator>` | `BlazorBlueprint.Components` | — | Session actions menu |
| `<BbDropdownMenuItem>` | `BlazorBlueprint.Components` | `@onclick` | Session actions menu |
| `<BbDialog>`, `<BbDialogContent>`, `<BbDialogHeader>`, `<BbDialogTitle>`, `<BbDialogDescription>` | `BlazorBlueprint.Components` | `@bind-Open` | ApprovalDialog |
| `<BbAlertDialog>`, `<BbAlertDialogContent>`, `<BbAlertDialogHeader>`, `<BbAlertDialogTitle>`, `<BbAlertDialogDescription>`, `<BbAlertDialogFooter>`, `<BbAlertDialogCancel>`, `<BbAlertDialogAction>` | `BlazorBlueprint.Components` | `@bind-Open` | DeleteConfirmationDialog |
| `<BbTooltip>`, `<BbTooltipTrigger>`, `<BbTooltipContent>` | `BlazorBlueprint.Components` | — | Collapsed sidebar icon labels |

### 9.6 Container & Scrolling Components

| Component | Import | Key Parameters | Used In |
|-----------|--------|----------------|---------|
| `<BbScrollArea>` | `BlazorBlueprint.Components` | — | Session list, message list |
| `<BbTabs>`, `<BbTabsList>`, `<BbTabsTrigger>`, `<BbTabsContent>` | `BlazorBlueprint.Components` | `@bind-Value` | CanvasPane tabs |

### 9.7 Service Providers (Layout Root)

| Component | Import | Purpose |
|-----------|--------|---------|
| `<BbToastProvider>` | `BlazorBlueprint.Components` | Toast notification rendering |
| `<BbDialogProvider>` | `BlazorBlueprint.Components` | Programmatic dialog API support |
| `<BbContainerPortalHost>` | `BlazorBlueprint.Components` | Inline-positioned portals (dropdown, tooltip, popover) |
| `<BbOverlayPortalHost>` | `BlazorBlueprint.Components` | Viewport-overlaying portals (dialog, sheet, command) |

### 9.8 Icon Components

| Component | Import | Key Parameters | Used In |
|-----------|--------|----------------|---------|
| `<LucideIcon>` | `BlazorBlueprint.Icons.Lucide.Components` | `Name`, `Size`, `Class` | Status icons, action icons, branding |

**Note:** Verify whether `BlazorBlueprint.Icons.Lucide` v3 renames `<LucideIcon>` to `<BbLucideIcon>`. The icon package versioning may differ from the main components package.

---

## 10. DI Registration Changes

### 10.1 Current Registration (Program.cs)

```csharp
builder.Services.AddBlazorBlueprintComponents();
```

### 10.2 BB v3 Registration (Program.cs)

```csharp
builder.Services.AddBlazorBlueprintComponents();
```

The method signature is unchanged. BB v3 internally registers `DialogService`, `ToastService`, `IPortalService`, and `KeyboardShortcutService` within this single call. No additional `AddDialogService()` or `AddToastService()` calls are needed.

### 10.3 New CSS Import

Verify the CSS import in `App.razor` or `_Host.cshtml`:

```html
<!-- v2 -->
<link href="_content/BlazorBlueprint.Components/blazorblueprint.css" rel="stylesheet" />

<!-- v3 — verify filename hasn't changed -->
<link href="_content/BlazorBlueprint.Components/blazorblueprint.css" rel="stylesheet" />
```

---

## 11. Migration Risk Assessment

### 11.1 Risk Matrix

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Bb prefix find-and-replace misses edge cases (e.g., inline C# `typeof<Dialog>()`) | Medium | Low | Use regex search: `<(?!Bb)[A-Z]\w+` in `.razor` files to find non-prefixed BB components after migration |
| Chart ECharts API doesn't match current `ChartResult` data model | Medium | Medium | Create adapter method `GetChartData()` that transforms `ChartResult` to `IEnumerable<Dictionary<string, object>>` |
| Scoped CSS regressions after prefix change | Low | Medium | Run visual diff on all pages post-migration; audit all `.razor.css` files for BB root element patterns |
| `UpdateTiming.OnChange` breaks ChatInput without explicit override | High | High | Add to migration checklist — test chat input responsiveness immediately after upgrade |
| Portal hosts not placed correctly → overlays don't render | Low | High | Follow §6.2 exact layout; test all dialog/sheet/command usages |
| `LucideIcon` package version mismatch with BB v3 Components | Low | Low | Check `BlazorBlueprint.Icons.Lucide` release notes for v3-compatible version |
| Combobox API completely redesigned → breaks any Combobox usage | Low | Low | Current codebase doesn't use `<Combobox>`; only affects future implementations |

### 11.2 Migration Execution Order

Recommended order to minimize risk of cascading failures:

| Step | Change | Validation |
|------|--------|-----------|
| 1 | Update `Directory.Packages.props` to BB v3 package versions | `dotnet restore` succeeds |
| 2 | Apply namespace flattening (M2) — update `_Imports.razor` | `dotnet build` succeeds |
| 3 | Apply Bb prefix (M1) — bulk find-and-replace across all `.razor` files | `dotnet build` succeeds |
| 4 | Add portal hosts (M4) — update layout root | `dotnet build` succeeds; dialogs/dropdowns render |
| 5 | Add `UpdateTiming="UpdateTiming.Immediate"` to ChatInput (M5) | Chat input responds to keystrokes |
| 6 | Update DI registration if needed (M6) — verify `Program.cs` | Toast/dialog services resolve |
| 7 | Migrate chart components (M3) — rewrite `ChartDisplay.razor` | Charts render with ECharts; test all 4 chart types |
| 8 | Audit scoped CSS (§7) — test all components with `.razor.css` | Visual regression check |

---

## 12. Design Decisions Summary

| # | Decision | Rationale | Alternatives Considered |
|---|----------|-----------|------------------------|
| D1 | BB v3 migration is a **prerequisite** (done before new feature implementation) | New spec code against v2 APIs is immediately obsolete. BB v3 components (`BbSidebar`, `BbCommandDialog`) are needed for session UX. Migration is mechanical. | Parallel migration — rejected: double the code changes, merge conflicts |
| D2 | Use `DialogService.Confirm()` for session deletion instead of declarative `BbAlertDialog` | More concise, no template markup needed, integrates cleanly with dropdown click handler | Declarative `BbAlertDialog` — valid but verbose for simple confirm/cancel |
| D3 | Use `BbToastProvider` semantic variants for notifications | Pre-styled icons and colors per variant; consistent UX | Custom toast styling — rejected: unnecessary when BB v3 provides semantic variants |
| D4 | Explicit `UpdateTiming.Immediate` on ChatInput only | Only ChatInput needs real-time keystroke updates. All other inputs work correctly with `OnChange` default. | Set `UpdateTiming.Immediate` globally — rejected: OnChange is the better default for form fields |
| D5 | Use BB v3 ECharts API for chart rendering | Replaces current HTML table fallback with interactive charts. Future-proof with BB v3 ecosystem. | Keep HTML table rendering — rejected: loses interactivity (zoom, hover, tooltips) |
| D6 | Two-layer portal strategy with `BbContainerPortalHost` + `BbOverlayPortalHost` | Required by BB v3 architecture. Automatic routing — no per-component portal configuration. | Single portal host (v2 pattern) — not supported in v3 |
| D7 | Wrapper `<div>` pattern for scoped CSS with BB components | Proven workaround for BB root elements not receiving Blazor scope attributes. Pattern validated across iterations 5-6. | Apply CSS via `Class` parameter only — rejected: inconsistent; some styles need scoped selectors |
| D8 | Migration execution order: namespaces → prefix → portals → inputs → charts → CSS audit | Minimizes cascading failures. Each step can be validated independently. Build-time checks catch issues early before runtime testing. | Big-bang (all at once) — rejected: difficult to debug failures |
