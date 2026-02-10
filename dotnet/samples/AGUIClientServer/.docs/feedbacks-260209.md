# Humand Feedbacks

## General Feedbacks
- Double check if the Claude theme is correctly applied across all components and pages. Almost all components seem to have default styling instead of Claude theme styling.
- Redesign the black theme color scheme to make it more visually appealing and consistent with the Claude theme.

## Chat Header Related
- Need to completely redesign "chat-header-container main-background-gradient" to make it more visually appealing. Given that the endpoint is for prototyping only, soon we are going to remove it since we will support all the AGUI features in the main AGUI app. In practice, users don't need to choose a specific AG-UI feature since all features will be available in the main AGUI app.
- The "panic-button-container" is not well positioned. Let's completely redesign its UXUI and position.

## Dual-Pane Layout Related
- Regarding the "dual-pane-root" layout, we need to explicitly predefine the width of the left and right panes to avoid layout issues across different screen sizes and resolutions.
- In dual-pane layout, the context-pane is for user chat activities, and the canvas-pane is for shared artifacts collaboration between human and AI. Currently, the canvas-pane is display something else and they are not shared artifacts. Be mindful that we need to handle overflow vertical scrolling properly in both panes to ensure that content is accessible without breaking the layout.
- The dual-pane layout should not always display canvas-pane by default. Instead, it should only show the canvas-pane when there is relevant content to display, such as shared artifacts or collaborative elements. If there is no content for the canvas-pane, it should be hidden to maximize the space for the context-pane (main chat area).

## Canvas-Pane Related
- In context-pane -> message-list, the assistant-thought is now always collapsed after the final "assistant-message" is displayed. The toggle button does not work anymore. During chat responses streaming, the assistant-thought is indeed expanded by default, but as soon as the final response is rendered, it collapses and the toggle button becomes non-functional. We need to fix this behavior so that users can expand or collapse the assistant-thought at any time, regardless of whether the final message has been displayed.
- We need to document clearly when the canvas-pane is visible, and for which specific use cases or content types it is intended to display. 
- For example:
  - the weather card is not an interactive shared artifact, so it should not be shown in the canvas-pane. 
  - the PlanProgress component is not an interactive shared artifact, so it should not be shown in the canvas-pane.
  - The recipe editor is an interactive shared artifact, so it should be displayed in the canvas-pane. 
  - The document editor (using monaco editor) is also an interactive shared artifact, so it should be displayed in the canvas-pane.

## Context-Pane (Message List) Related
- I'm sending message "What is the weather in Paris" and there are weather card displayed in message-list anymore. This is used to work befor the dual-pane architecture.
- I'm sending message "Show me the progress of my plan" and there are PlanProgress component displayed anymore. This is used to work befor the dual-pane architecture. We should ensure to display it in message-list.
- In a assistant-thought, we should not include visual components like weather card or PlanProgress. Instead, these visual components should be rendered directly in the message-list as separate messages from the assistant. This way, user can always see the visual components without needing to expand the assistant-thought.

## Other components
- Thoroughly redesign and reallocate the Observability components (MemoryInspector, ToolExecutionInspector, etc.) to enhance their usability and visual appeal. Currently these components seems to be raw components without any CSS effects applied to them. And be mindful that they should not be displayed in the canvas-pane.