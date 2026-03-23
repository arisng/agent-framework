---
title: "AGUIDojo phase 0/1 testing feedback backlog"
type: task
status: open
date: 2026-03-23
area: AGUIDojo
priority: high
source: manual testing feedback
---

# AGUIDojo phase 0/1 testing feedback backlog

## Purpose

Capture the post-phase-0/1 feedback from AGUIDojo testing as grouped follow-up work. This issue intentionally categorizes the observations instead of proposing fixes.

Reference context: [implementation-plan.md](../.docs/implementation-plan.md)

## Group 1: Session integrity and lifecycle

Priority: critical

- Refreshing the app after sending a message can create two new chat sessions in the sidebar.
- This appears to be the most severe issue because it suggests duplicate session creation or rehydration behavior.
- Related area: [ChatHeader.razor](../AGUIDojoClient/Components/Pages/Chat/ChatHeader.razor)

## Group 2: Chat input and composer behavior

Priority: high

- When the user sends a message by pressing Enter or clicking Send, the input box should clear immediately.
- Current behavior leaves the typed text visible in the composer even though the message has already appeared in the message list.

## Group 3: Message presentation and layout

Priority: high

- Assistant markdown rendering in the message body needs a readability pass.
- The current typography and spacing feel too dense for rendered HTML content.
- The user message edit button overlaps the message text, which hurts usability.
- The user message wrapper leaves an extra gap below the message, making the layout feel unbalanced.

## Group 4: Interactive controls and local UI state

Priority: high

- The thought toggle is no longer working as a collapsible control.
- It should restore the ability to show and hide nested content on click.
- The assistant confidence text appears to reset to 70% after refresh.
- That value should remain consistent with the current session state instead of appearing as a post-refresh default.

## Group 5: Artifact rendering and workspace UX

Priority: medium

- AGUIDojo should be able to render Mermaid diagrams as artifacts.
- The artifact should be displayable either in the current message list/context pane or in the canvas pane.
- The UI should allow toggling an artifact between ContextPane and CanvasPane without disrupting chat focus.
- This is a core AG-UI/agentic-workspace experience gap rather than a plain chat-message bug.
- For example: A chart or plan or data grid generated as an artifact in a message should be viewable in the context pane but also movable to the canvas for persistent reference without losing the user's place in the chat. In addition, we can also enhance the management of these artifacts visualization by adding new property to enforce the place where the artifact should be rendered by default (context pane vs canvas) and add a toggle button to move the artifact between these two places. And also a new property to enforce the artifact must only be rendered in the canvas and never in the context pane, this is for the cases like the mermaid diagrams that require more space to be visualized.

## Group 6: Repository hygiene and ownership

Priority: medium

- AGUIDojo-owned code should not retain the Microsoft copyright header.
- The sample is user-owned, so those headers should be removed wherever they were copied forward from Microsoft code.

## Suggested backlog order

1. Fix session duplication on refresh.
2. Fix composer clearing after send.
3. Restore thought-toggle behavior.
4. Clean up message layout and markdown readability.
5. Stabilize confidence/session UI state after refresh.
6. Add artifact rendering and toggle support.
7. Remove leftover Microsoft copyright headers from AGUIDojo-owned code.
