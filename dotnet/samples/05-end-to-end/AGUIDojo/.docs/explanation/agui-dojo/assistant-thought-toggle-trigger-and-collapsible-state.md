# Assistant thought toggle trigger and collapsible state

## Summary

AGUIDojo’s assistant-thought disclosure stopped behaving like a real collapsible. The summary row still looked clickable, but clicking it no longer changed the open/closed state of the thought content.

The fix keeps the same collapsed/expanded state model, but replaces the trigger markup with an explicit `BbCollapsibleTrigger` button instead of relying on custom child markup that no longer reliably received trigger behavior.

## What the defect looked like

Before the fix, each assistant thought rendered:

- a summary row that visually looked interactive
- a chevron that suggested expand/collapse behavior
- a `BbCollapsible` container with `Open` / `OpenChanged`

But clicking the summary did not collapse the nested tool/error content. The component state model existed, yet the user-facing disclosure interaction was effectively disconnected.

## Root cause

The bug lived at the boundary between AGUIDojo’s custom markup and the BlazorBlueprint collapsible trigger contract.

`AssistantThought` used this pattern:

- `BbCollapsibleTrigger`
- wrapping a plain `<div class="thought-toggle"> ... </div>`

That depended on implicit trigger-child behavior. Once that assumption stopped lining up with the current Blueprint trigger expectations, the child content still rendered, but the actual toggle semantics were no longer guaranteed to be wired to the rendered element.

In practice, AGUIDojo had:

- a local `IsExpanded` state
- an `OnToggleChanged` callback
- auto-collapse behavior after streaming completes

but no reliable interactive trigger element driving those state changes.

## Why state logic was not the main problem

The collapse state itself was still coherent:

- `IsExpanded` tracked open/closed UI state
- `_userToggled` prevented stream-completion auto-collapse from overriding a manual choice
- `OnParametersSet` still handled the `InProgress -> false` transition

So the failure was not primarily about rerender resets. The broken piece was the trigger hookup.

## Trigger model introduced by the fix

The summary row now uses an explicit rendered trigger button:

- `BbCollapsibleTrigger AsChild="false"`
- the `thought-toggle` class applied directly to the trigger
- the summary icons/text rendered inside that trigger button

This does two important things:

1. it restores a concrete interactive element that Blueprint can fully control
2. it lets Blueprint own the disclosure semantics instead of relying on AGUIDojo’s child `<div>` to behave like a trigger

The visual styling stays the same, but the interaction contract is now explicit instead of incidental.

## Resulting behavior

With the fix in place:

- the thought summary is rendered as a real button
- clicking it changes the collapsible state again
- the existing `IsExpanded` / `_userToggled` lifecycle continues to work
- completed thoughts can collapse and re-expand without losing their content model

The design lesson is that disclosure widgets should use the UI library’s explicit trigger element, not a visually styled stand-in that only looks interactive.
