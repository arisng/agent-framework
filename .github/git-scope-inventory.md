# Scopes Inventory

**Repository:** agent-framework (AGUIClientServer sample)
**Last Updated:** 2026-02-24
**Source:** Git commit history analysis — all history for `dotnet/samples/AGUIClientServer/**`

## Summary

- Total Commit Types Observed: 5
- Total Unique Scopes (including legacy): 9
- Analysis Period: Full git history

## Scopes by Commit Type

### `feat` (10 commits)

| Scope | Count | Notes |
|-------|-------|-------|
| `aguidojo` | 4 | Cross-cutting UX/Nebula/theme work |
| `aguiclientserver` | 5 | Legacy — early full-sample features |
| `aguiwebclient` | 1 | Legacy — simple web client |
| `aguidojo-client` | 1 | Tabbed canvas + data grid addition |
| `aguidojo-ux` | 1 | Dual-pane layout enhancement |

### `fix` (2 commits)

| Scope | Count | Notes |
|-------|-------|-------|
| `aguiclientserver` | 1 | Legacy — SharedState AG-UI fix |
| `aguidojo-server` | 1 | Tool result streaming fix |

### `refactor` (3 commits)

| Scope | Count | Notes |
|-------|-------|-------|
| `aguidojo` | 2 | General refactor + AppHost rename |
| `aguidojo-weather` | 1 | Weather component restructure |

### `style` (1 commit)

| Scope | Count | Notes |
|-------|-------|-------|
| `aguidojo-components` | 1 | Component styling and interactions |

### `docs` (3 commits)

| Scope | Count | Notes |
|-------|-------|-------|
| `aguidojo` | 3 | Setup guides, feedback cleanup |

## All Observed Scopes (Alphabetical)

- `aguidojo` — 9 commits (feat ×4, refactor ×2, docs ×3)
- `aguidojo-client` — 1 commit (feat ×1)
- `aguidojo-components` — 1 commit (style ×1)
- `aguidojo-server` — 1 commit (fix ×1)
- `aguidojo-ux` — 1 commit (feat ×1)
- `aguidojo-weather` — 1 commit (refactor ×1)
- `aguiclientserver` — 6 commits (feat ×5, fix ×1) **[legacy]**
- `aguiwebclient` — 1 commit (feat ×1) **[legacy]**

## Coverage Gap Analysis

The following approved scopes (from constitution) have **no git history** yet. They are derived from structural analysis of the AGUIDojoClient folder layout and are ready for use:

- `aguidojo-chat` (AGUIDojoClient/Components/Pages/Chat)
- `aguidojo-layout` (AGUIDojoClient/Components/Layout)
- `aguidojo-approvals` (AGUIDojoClient/Components/Approvals + AGUIDojoServer/HumanInTheLoop)
- `aguidojo-generative-ui` (AGUIDojoClient/Components/GenerativeUI + AGUIDojoServer/Tools)
- `aguidojo-governance` (AGUIDojoClient/Components/Governance)
- `aguidojo-shared-state` (AGUIDojoClient/Components/SharedState + AGUIDojoServer/SharedState)
- `aguidojo-predictive-ui` (AGUIDojoClient/Components/PredictiveUI + AGUIDojoServer/PredictiveStateUpdates)
- `aguidojo-observability` (AGUIDojoClient/Components/Observability)
- `aguidojo-mobile` (AGUIDojoClient/Components/Mobile)
- `aguidojo-store` (AGUIDojoClient/Store)
- `aguidojo-theme` (AGUIDojoClient/wwwroot + ThemeService)
- `aguidojo-apphost` (AGUIDojo.AppHost)
- `aguidojo-tests` (AGUIDojoServer.Tests)
- `aguidojo-setup` (documentation only)
- `aguidojo-spec` (documentation only)

## Notes

- Inventory covers all commits touching `dotnet/samples/AGUIClientServer/**`.
- Pull request merge commits and upstream Microsoft commits are excluded from scope counting.
- Inventory reflects historical usage, not approved future scopes (see `scope-constitution.md`).
- Legacy scopes (`aguiclientserver`, `aguiwebclient`) are preserved for historical integrity and must not be used for new commits.
