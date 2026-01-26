# UI Component Modules (AGUIWebChat)

## Overview
The UI is composed of Blazor components under the chat page. The components orchestrate message flow, render message content, and expose user actions. Modules below include responsibilities, inputs/outputs, and key dependencies.

## Component Modules

### Chat page orchestrator
- **Responsibility:** Manage conversation state, issue streaming chat requests, and coordinate child components.
- **Inputs:** User messages, `IChatClient` instance, chat options.
- **Outputs:** Streamed response updates, updated message list, suggestion refresh.
- **Dependencies:** Blazor component lifecycle, `IChatClient` streaming API.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/Chat.razor](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/Chat.razor)

### Chat header
- **Responsibility:** Display title and provide a “New chat” action.
- **Inputs:** `OnNewChat` callback.
- **Outputs:** UI action event.
- **Dependencies:** Blazor event binding.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatHeader.razor](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatHeader.razor)

### Message list
- **Responsibility:** Render a scrollable list of messages and streaming indicators.
- **Inputs:** `Messages`, `InProgressMessage`, empty-state content.
- **Outputs:** Message list UI and loading indicator.
- **Dependencies:** Blazor rendering, JS interop for auto-scroll.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageList.razor](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageList.razor)

### Message item renderer
- **Responsibility:** Render user and assistant message content, including assistant function-call visualizations and Agentic UI state updates (`DataContent`).
- **Inputs:** `ChatMessage` contents, state payloads.
- **Outputs:** Message markup with role-based styling and state blocks.
- **Dependencies:** Blazor conditional rendering, JSON formatting.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageItem.razor](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageItem.razor)

### Citation renderer
- **Responsibility:** Render citations for file-backed content (e.g., PDF snippets) with viewer links.
- **Inputs:** `File`, `PageNumber`, `Quote`.
- **Outputs:** Anchored citation UI.
- **Dependencies:** Blazor parameters, URL encoding.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatCitation.razor](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatCitation.razor)

### Chat input
- **Responsibility:** Collect user text, manage submit flow, and focus behavior.
- **Inputs:** User text, `OnSend` callback.
- **Outputs:** `ChatMessage` events.
- **Dependencies:** Blazor forms and validation, JS interop for autosize/enter-to-send.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatInput.razor](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatInput.razor)

### Suggestions panel
- **Responsibility:** Render and trigger follow-up prompts derived from the chat history.
- **Inputs:** Suggestions from `IChatClient`.
- **Outputs:** Click events that add a new user message.
- **Dependencies:** Blazor state updates and async handling.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatSuggestions.razor](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatSuggestions.razor)

### Loading indicator
- **Responsibility:** Display a spinner while streaming responses.
- **Inputs:** Loading state from message list.
- **Outputs:** Animated spinner.
- **Dependencies:** Blazor rendering, scoped CSS.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Layout/LoadingSpinner.razor](dotnet/samples/AGUIWebChat/Client/Components/Layout/LoadingSpinner.razor)

### App shell and layout
- **Responsibility:** Define the HTML shell, base styles, and error UI.
- **Inputs:** Root component tree.
- **Outputs:** App layout and error surface.
- **Dependencies:** Blazor routing and layout.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/App.razor](dotnet/samples/AGUIWebChat/Client/Components/App.razor), [dotnet/samples/AGUIWebChat/Client/Components/Layout/MainLayout.razor](dotnet/samples/AGUIWebChat/Client/Components/Layout/MainLayout.razor)

## References
- Blazor component model: https://learn.microsoft.com/aspnet/core/blazor/components/?view=aspnetcore-8.0
- Blazor routing: https://learn.microsoft.com/aspnet/core/blazor/routing?view=aspnetcore-8.0
- Blazor forms and validation: https://learn.microsoft.com/aspnet/core/blazor/forms-validation?view=aspnetcore-8.0
