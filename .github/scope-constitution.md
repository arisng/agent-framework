# Commit Scope Constitution

Last Updated: 2026-02-10

## Purpose

This constitution defines the approved commit scopes for AGUIDojo work in this repository. It keeps commit history consistent and makes it easy to locate changes across the AGUIDojo client, server, and app host.

## Scope Naming Conventions

- Use kebab-case (`aguidojo-client`, not `AGUIDojoClient`).
- Prefer the `aguidojo-` prefix for AGUIDojo subprojects.
- Use the most specific scope that fits the change.
- Legacy scopes remain valid for historical continuity but should not be used for new work unless required for migration.

## Approved Scopes by Commit Type

### `feat`
- `aguidojo`: Cross-cutting AGUIDojo changes spanning multiple subprojects.
- `aguidojo-client`: AGUIDojo Blazor client (BFF) features.
- `aguidojo-server`: AGUIDojo server features (AG-UI endpoints, APIs, tools).
- `aguidojo-apphost`: AGUIDojo Aspire AppHost changes.
- `aguidojo-tests`: AGUIDojo server test additions.
- `aguiclientserver` (legacy): Historical AGUIClientServer scope; prefer `aguidojo` or a subproject scope.
- `aguiwebclient` (legacy): Historical web client scope; prefer `aguidojo-client`.

### `fix`
- `aguidojo`: Cross-cutting fixes across client/server/apphost.
- `aguidojo-client`: Client-side bug fixes.
- `aguidojo-server`: Server-side bug fixes.
- `aguidojo-apphost`: AppHost fixes.
- `aguidojo-tests`: Test fixes.
- `aguiclientserver` (legacy): Historical scope; avoid for new fixes unless migrating.
- `aguiwebclient` (legacy): Historical scope; avoid for new fixes unless migrating.

### `refactor`
- `aguidojo`: Cross-cutting refactors across multiple subprojects.
- `aguidojo-client`: Client refactors.
- `aguidojo-server`: Server refactors.
- `aguidojo-apphost`: AppHost refactors.
- `aguidojo-tests`: Test refactors.
- `aguiclientserver` (legacy): Historical scope; avoid for new refactors unless migrating.
- `aguiwebclient` (legacy): Historical scope; avoid for new refactors unless migrating.

### `docs`
- `aguidojo`: Documentation covering multiple AGUIDojo subprojects.
- `aguidojo-client`: Client documentation (README, usage, UX notes).
- `aguidojo-server`: Server documentation (setup, APIs, tools).
- `aguidojo-apphost`: AppHost documentation.

### `chore`
- `aguidojo`: Cross-cutting maintenance (config, tooling) for AGUIDojo.
- `aguidojo-client`: Client maintenance/config changes.
- `aguidojo-server`: Server maintenance/config changes.
- `aguidojo-apphost`: AppHost maintenance/config changes.
- `aguidojo-tests`: Test maintenance.

### `test`
- `aguidojo-tests`: Add or update AGUIDojo tests.
- `aguidojo-server`: Inline test or fixture changes tightly coupled to server code.

## Scope Selection Guidelines

- If a change touches both client and server (or app host), use `aguidojo`.
- If changes are contained within one subproject, use the matching subproject scope.
- Use `aguidojo-tests` when work is limited to test projects.
- Only use legacy scopes to maintain continuity when updating old commits or documenting historical context.

## Amendment Process

1. Propose a new scope when a new AGUIDojo module or subproject is introduced.
2. Add the scope under the appropriate commit types with a clear definition.
3. Record changes in the amendment history with rationale.

## Amendment History

### 2026-02-10 - Amendment #1

**Changes:**
- Established initial AGUIDojo scope constitution and legacy scope handling.

**Rationale:**
- Aligns scopes with the AGUIDojo client/server/apphost structure while preserving existing historical scopes.
