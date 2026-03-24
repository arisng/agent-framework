---
title: Enable multi-select mode in session list with bulk deletion confirmation
type: feature-plan
status: proposed
author: Copilot
created_at: 2026-03-24
labels:
  - ux
  - session-management
  - bulk-actions

---

## Summary

Implement the ability for users to multi-select sessions in the session list so they can quickly choose multiple sessions for bulk operations. Provide a toggle button to enable/disable multi-select mode. When a bulk deletion is initiated, require explicit user confirmation before deleting.

## Motivation

Users frequently need to operate on several sessions at once (archive, delete, export, etc.). Current UI requires one-by-one selection, which is inefficient and error-prone. Multi-select mode with explicit confirmation increases efficiency and minimizes accidental data loss.

## Scope

- UI: Add a `Multi-select` toggle button inside session list toolbar.
- UI: Display checkboxes in session list rows only when multi-select mode is active.
- UI: Add bulk action controls (Delete, e.g. `Delete selected`) when any items are selected.
- Confirmation: On bulk delete, show modal confirmation with a clear action and cancel path.
- UX: On toggling multi-select OFF, clear current session selection.

## Requirements

1. Add a `Multi-select mode` toggle (switch/button) + state variable in session list component.
2. In multi-select mode, show per-row checkboxes and allow selecting multiple sessions.
3. Show selection count and enable bulk action buttons when >0 sessions selected.
4. `Delete selected` triggers a modal: "Are you sure? This will permanently delete X sessions." Confirm and cancel.
5. Backend call for bulk deletion should run only _after_ confirmation.
6. Safeguard: If no sessions selected, disable bulk action triggers.

## Acceptance Criteria

- [ ] Multi-select toggle exists and toggles UI row checkboxes.
- [ ] Selecting multiple rows updates selection count display.
- [ ] Bulk delete action appears only in multi-select mode and with selected rows.
- [ ] Confirm dialog is required to finalize deletion.
- [ ] Cancelling the dialog aborts deletion and leaves selected sessions intact.
- [ ] Multi-select OFF clears selection state.

## Notes

- Avoid auto-deleting when toggle mode is OFF.
- Keep keyboard navigation/accessibility consistent with existing table interactions.
- Consider adding a dedicated `Select All` checkbox when in multi-select mode if session count is moderate.
