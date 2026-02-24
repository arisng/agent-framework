# Commit Scope Constitution

Last Updated: 2026-02-24

## Purpose

This constitution defines the approved commit scopes for AGUIDojo work in this repository (specifically the `dotnet/samples/AGUIClientServer/` sample). It keeps commit history consistent and makes it easy to locate changes across the AGUIDojoClient, AGUIDojoServer, and orchestration projects.

## Covered Repository Area

This constitution governs commits that touch `dotnet/samples/AGUIClientServer/**`. The sample contains:

| Project | Path | Purpose |
|---------|------|---------|
| `AGUIDojoClient` | `AGUIDojoClient/` | Blazor BFF — UI, state management, services |
| `AGUIDojoServer` | `AGUIDojoServer/` | ASP.NET Core backend — AG-UI endpoints, tools, services |
| `AGUIDojoServer.Tests` | `AGUIDojoServer.Tests/` | xUnit server test project |
| `AGUIDojo.AppHost` | `AGUIDojo.AppHost/` | Aspire AppHost orchestration |
| `AGUIClient` | `AGUIClient/` | Minimal reference client |
| `AGUIServer` | `AGUIServer/` | Minimal reference server |
| `AGUIClientServer.AppHost` | `AGUIClientServer.AppHost/` | Aspire host for the minimal pair |

## Scope Naming Conventions

- Use kebab-case (`aguidojo-client`, not `AGUIDojoClient`).
- Prefix product scopes with `aguidojo-` for AGUIDojo subprojects.
- Use the **most specific** scope that accurately describes the change area.
- Prefer a feature-area scope over a project-level scope when the change is localised to a feature area.
- Legacy scopes remain valid for historical continuity but **must not** be used for new commits.
- Each commit must carry **exactly one scope**. For cross-cutting changes use `aguidojo`.

## Approved Scopes by Commit Type

### Scope Quick Reference

| Scope | Level | What it covers |
|-------|-------|----------------|
| `aguidojo` | umbrella | Changes spanning multiple subprojects |
| `aguidojo-client` | project | AGUIDojoClient project (general) |
| `aguidojo-server` | project | AGUIDojoServer project (general) |
| `aguidojo-apphost` | project | AGUIDojo.AppHost project |
| `aguidojo-tests` | project | AGUIDojoServer.Tests project |
| `aguidojo-chat` | feature | Chat page and message components |
| `aguidojo-layout` | feature | Dual-pane layout, canvas, context, plan panes |
| `aguidojo-ux` | feature | Overall UX design, interaction patterns, theme |
| `aguidojo-components` | feature | Shared/reusable Blazor components |
| `aguidojo-approvals` | feature | HITL approval workflow (dialog, queue, swipe) |
| `aguidojo-generative-ui` | feature | Generative UI components (charts, forms, data grids) |
| `aguidojo-governance` | feature | Governance components (diff preview, risk badge) |
| `aguidojo-shared-state` | feature | Shared state protocol and recipe editor |
| `aguidojo-predictive-ui` | feature | Predictive state update agent and components |
| `aguidojo-observability` | feature | Tool execution tracker, memory inspector |
| `aguidojo-mobile` | feature | Mobile layout, swipe gestures, artifact sheet |
| `aguidojo-store` | feature | Fluxor state slices (AgentState, ArtifactState, ChatState, PlanState) |
| `aguidojo-theme` | feature | CSS theme tokens, dark/light mode, colour schemes |
| `aguidojo-weather` | feature-demo | Weather demo feature (client + server) |
| `aguiclientserver` | legacy | Old umbrella scope — do not use |
| `aguiwebclient` | legacy | Old simple client scope — do not use |

---

### `feat`

**Project-level** (use when a feature spans the whole project or doesn't fit a specific feature area):
- `aguidojo`: Cross-cutting AGUIDojo features spanning multiple subprojects.
- `aguidojo-client`: AGUIDojoClient app-level features (routing, DI setup, startup).
- `aguidojo-server`: AGUIDojoServer features (AG-UI endpoints, new APIs, new tools).
- `aguidojo-apphost`: AGUIDojo.AppHost — Aspire resource changes, new integrations.
- `aguidojo-tests`: New test cases in `AGUIDojoServer.Tests`.

**Feature-area** (prefer these over project-level when the change is localised):
- `aguidojo-chat`: Chat page, message rendering, agent avatar, assistant thoughts, citations, handoff.
- `aguidojo-layout`: Structural layout — `DualPaneLayout`, `CanvasPane`, `ContextPane`, `MainLayout`, `PlanSheet`, `MonacoEditor`.
- `aguidojo-ux`: UX enhancements, interaction design, animations, responsive behaviour.
- `aguidojo-components`: Shared reusable components not tied to a specific feature area.
- `aguidojo-approvals`: Human-in-the-Loop approval flow — `ApprovalDialog`, `ApprovalQueue`, `SwipeApproval`, server approval agent.
- `aguidojo-generative-ui`: Generative UI rendering — `ChartDisplay`, `DataGridDisplay`, `DynamicFormDisplay`, `WeatherDisplay`, `PlanProgress`, tool-component registry.
- `aguidojo-governance`: Governance UI — `DiffPreview`, `RiskBadge`, `CheckpointManager`, `PanicButton`.
- `aguidojo-shared-state`: Shared state — `RecipeEditor`, `SharedStateAgent`, AG-UI shared state protocol.
- `aguidojo-predictive-ui`: Predictive state updates — `PredictiveStateUpdatesAgent`, `DocumentState`, `DocumentService`.
- `aguidojo-observability`: Observability panel — `ToolExecutionTracker`, `MemoryInspector`, `ObservabilityService`.
- `aguidojo-mobile`: Mobile-first UI — `MobileLayout`, `ArtifactSheet`, `SwipeApproval`.
- `aguidojo-store`: Fluxor state management — `AgentState`, `ArtifactState`, `ChatState`, `PlanState` slices.
- `aguidojo-theme`: CSS theme tokens, dark/light mode, `ThemeService`, global `app.css` design tokens.
- `aguidojo-weather`: Weather demo — `WeatherTool`, `WeatherService`, `WeatherDisplay`, `WeatherEndpoints`.

**Legacy** (historical only — do not use for new work):
- `aguiclientserver` (legacy): Early AGUIClientServer umbrella scope.
- `aguiwebclient` (legacy): Early AGUIWebClient scope.

### `fix`

- `aguidojo`: Cross-cutting fixes touching multiple subprojects.
- `aguidojo-client`: Client-side bug fixes.
- `aguidojo-server`: Server-side bug fixes.
- `aguidojo-apphost`: AppHost fixes.
- `aguidojo-tests`: Test failure fixes.
- `aguidojo-chat`: Chat page / message display bugs.
- `aguidojo-layout`: Layout / pane sizing and visibility bugs.
- `aguidojo-ux`: UX interaction bugs (focus, scroll, animation).
- `aguidojo-approvals`: Approval flow bugs.
- `aguidojo-generative-ui`: Generative UI rendering bugs.
- `aguidojo-governance`: Governance component bugs.
- `aguidojo-shared-state`: Shared state syncing bugs.
- `aguidojo-predictive-ui`: Predictive update bugs.
- `aguidojo-observability`: Observability display bugs.
- `aguidojo-mobile`: Mobile layout / gesture bugs.
- `aguidojo-store`: State reducer / action bugs.
- `aguidojo-weather`: Weather feature bugs.
- `aguiclientserver` (legacy): Historical scope; avoid.
- `aguiwebclient` (legacy): Historical scope; avoid.

### `refactor`

- `aguidojo`: Cross-cutting refactors touching multiple subprojects.
- `aguidojo-client`: Client-side code restructuring.
- `aguidojo-server`: Server-side code restructuring.
- `aguidojo-apphost`: AppHost restructuring.
- `aguidojo-tests`: Test restructuring.
- `aguidojo-chat`: Chat component restructuring.
- `aguidojo-layout`: Layout component restructuring.
- `aguidojo-store`: State management restructuring.
- `aguidojo-weather`: Weather feature restructuring.
- `aguiclientserver` (legacy): Historical scope; avoid.

### `style`

- `aguidojo-theme`: Global design tokens, CSS variables, dark/light palette changes.
- `aguidojo-components`: Visual polish on shared components (spacing, sizing, typography).
- `aguidojo-layout`: Layout spacing, border, shadow tweaks.
- `aguidojo-chat`: Chat bubble and message styling.
- `aguidojo-css`: Global `app.css` or CSS utility changes not tied to a specific feature.

### `docs`

- `aguidojo`: Documentation covering multiple AGUIDojo subprojects (`.docs/`, `README.md`).
- `aguidojo-client`: Client-scoped docs (usage, UX notes, component docs).
- `aguidojo-server`: Server docs (setup, API reference, tool descriptions).
- `aguidojo-apphost`: AppHost docs.
- `aguidojo-setup`: Developer setup and secrets configuration guides.
- `aguidojo-spec`: Specification documents in `.docs/`.

### `chore`

- `aguidojo`: Cross-cutting maintenance (shared config, tooling).
- `aguidojo-client`: Client maintenance (`.csproj`, package updates, config).
- `aguidojo-server`: Server maintenance.
- `aguidojo-apphost`: AppHost maintenance.
- `aguidojo-tests`: Test infrastructure maintenance.
- `aguidojo-deps`: NuGet or npm dependency bumps.

### `test`

- `aguidojo-tests`: Add or update tests in `AGUIDojoServer.Tests`.
- `aguidojo-server`: Inline test helpers tightly coupled to server production code.
- `aguidojo-shared-state`: Tests for shared state agent or recipe logic.

## Scope Selection Guidelines

1. **Multi-subproject change** → `aguidojo` (umbrella).
2. **Single subproject, no specific feature** → `aguidojo-client` / `aguidojo-server` / `aguidojo-apphost` / `aguidojo-tests`.
3. **Feature area within a subproject** → use the matching feature-area scope (e.g., `aguidojo-chat`, `aguidojo-approvals`).
4. **Demo features** → use `aguidojo-weather` for weather-only changes.
5. **Pure styling** → use `style` type with the closest area scope.
6. **Never mix scopes** — choose the single most specific scope; mention secondary areas in the subject line.
7. **Legacy scopes** → read-only; use only when documenting historical context.

### Decision Tree

```
Change in AGUIClientServer?
└── Touches multiple projects? → aguidojo
    └── One project only?
        ├── AGUIDojoServer.Tests? → aguidojo-tests
        ├── AGUIDojo.AppHost? → aguidojo-apphost
        ├── AGUIDojoServer?
        │   ├── Feature-area change? → aguidojo-<area>
        │   └── General server change → aguidojo-server
        └── AGUIDojoClient?
            ├── Chat components? → aguidojo-chat
            ├── Layout components? → aguidojo-layout
            ├── Approvals? → aguidojo-approvals
            ├── Generative UI? → aguidojo-generative-ui
            ├── Governance? → aguidojo-governance
            ├── Shared state? → aguidojo-shared-state
            ├── Predictive UI? → aguidojo-predictive-ui
            ├── Observability? → aguidojo-observability
            ├── Mobile? → aguidojo-mobile
            ├── Fluxor store? → aguidojo-store
            ├── CSS/theme? → aguidojo-theme
            └── General client change → aguidojo-client
```

## Amendment Process

1. Propose a new scope when a new AGUIDojo module, subproject, or feature area is introduced.
2. Add the scope to all relevant commit types with a clear, non-overlapping definition.
3. Update the Quick Reference table.
4. Record the change in the amendment history with rationale.

## Amendment History

### 2026-02-24 - Amendment #2

**Changes:**
- Added `style` type with scopes: `aguidojo-theme`, `aguidojo-components`, `aguidojo-layout`, `aguidojo-chat`, `aguidojo-css`.
- Added feature-area scopes discovered in git history and structural analysis:
  `aguidojo-chat`, `aguidojo-layout`, `aguidojo-ux`, `aguidojo-components`,
  `aguidojo-approvals`, `aguidojo-generative-ui`, `aguidojo-governance`,
  `aguidojo-shared-state`, `aguidojo-predictive-ui`, `aguidojo-observability`,
  `aguidojo-mobile`, `aguidojo-store`, `aguidojo-theme`, `aguidojo-weather`.
- Added `docs(aguidojo-setup)` and `docs(aguidojo-spec)` sub-scopes.
- Added Scope Quick Reference table and Decision Tree.
- Updated Covered Repository Area section with project inventory table.

**Rationale:**
- The AGUIDojoClient now has nine distinct feature areas (Components vs. one monolithic `aguidojo-client` scope). Granular scopes make it possible to bisect changes by feature when reviewing PRs or investigating regressions.
- `aguidojo-ux`, `aguidojo-weather`, and `aguidojo-components` were already used in commits; formalising them prevents inconsistency.
- The `style` type was missing despite `style(aguidojo-components)` appearing in git history.

**Migration Notes:**
- Existing commits using `feat(aguidojo-client)` for layout or UX work are not retroactively renamed — the constitution governs future commits only.

### 2026-02-10 - Amendment #1

**Changes:**
- Established initial AGUIDojo scope constitution and legacy scope handling.

**Rationale:**
- Aligns scopes with the AGUIDojo client/server/apphost structure while preserving existing historical scopes.
