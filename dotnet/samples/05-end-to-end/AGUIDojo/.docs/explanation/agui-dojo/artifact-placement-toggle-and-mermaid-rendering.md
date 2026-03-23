# Artifact placement toggles and Mermaid rendering

AGUIDojo’s artifact rendering bug was not one broken component. It was a gap in the artifact model.

Before this fix, tool results were split across two unrelated paths:

- read-only visual results such as charts and weather rendered inline from the message itself
- canvas-oriented artifacts such as data grids were promoted into session state and shown in `CanvasPane`

That split meant the UI had no durable notion of “this tool result is an artifact that can move between chat context and workspace.” As a result, AGUIDojo could not support placement toggling, and there was no natural place to add Mermaid diagrams that should default to the canvas.

## What changed

The fix introduced a lightweight artifact-placement model instead of adding one-off conditions to each renderer.

- `ToolMetadata` now describes default placement and whether an artifact can be moved back and forth or must stay canvas-only.
- `SessionState.ToolArtifacts` stores promoted visual artifacts in a dedicated canvas workspace collection instead of a single ad hoc slot.
- `ChatMessageItem` renders all visual tool results from shared metadata. Inline artifacts can be moved to the canvas, and promoted artifacts render as lightweight placeholders in the chat with “focus canvas” or “show inline” actions.
- `CanvasPane` now exposes an `Artifacts` workspace tab with artifact pills so multiple promoted artifacts can remain available while the chat continues.
- `AgentStreamingService` auto-promotes visual tools whose default placement is the canvas.
- Mermaid diagrams are now first-class visual artifacts via `MermaidDisplay`, with Mermaid-specific parsing in `ToolResultParser`.

## Why this fixes the user experience

The important change is that placement is now stateful instead of implicit.

An artifact is no longer “whatever the current renderer decided to do.” It is either:

- inline in the current message flow, or
- promoted into the canvas workspace collection

Because the same parsed tool result can exist in either location, AGUIDojo can let the user move it without regenerating the response or disturbing the rest of the transcript. That directly closes the “keep the artifact for reference without losing chat focus” gap from manual testing.

## Mermaid-specific design choice

Mermaid diagrams were registered as visual artifacts with canvas-first, canvas-only placement.

That choice matters because Mermaid diagrams usually need more width than the message column provides. Treating Mermaid as a normal inline visual tool would technically render something, but it would create a cramped and unreliable experience. By making Mermaid canvas-only, the system keeps the placement rule explicit in metadata rather than scattering Mermaid exceptions through the chat renderer.

The renderer also falls back to showing raw Mermaid source if the browser cannot initialize the Mermaid runtime, so the artifact remains inspectable even when interactive rendering is unavailable.

## Design insight

The key lesson is that artifact placement belongs in session state and tool metadata, not in individual components.

If placement rules live only in renderers, every new artifact type reopens the same problem. By moving the rule into metadata plus workspace state, AGUIDojo now has one consistent extension point for:

- default inline artifacts
- default canvas artifacts
- canvas-only artifacts
- future artifact types that need different workspace behavior
