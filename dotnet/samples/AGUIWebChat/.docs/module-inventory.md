# AGUIWebChat Module Inventory

This document inventories the AGUIWebChat sample modules and maps them to source files.

## Server Modules

| Module                    | Responsibility                                                              | Inputs/Outputs                                                                          | Primary files                                                                                              |
| ------------------------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| Server host bootstrap     | Configure hosting, register AG-UI services, and start the ASP.NET Core app. | Inputs: environment, configuration. Outputs: running server and endpoints.              | Server/Program.cs                                                                                          |
| Azure OpenAI client setup | Create `AzureOpenAIClient` and `ChatClient` used by the agent.              | Inputs: `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT_NAME`. Outputs: `ChatClient`. | Server/Program.cs                                                                                          |
| Agent creation            | Build a `ChatClientAgent` with name, instructions, and tools.               | Inputs: `ChatClient`. Outputs: `ChatClientAgent`.                                       | Server/Program.cs                                                                                          |
| Agentic UI wrapper        | Wrap the base agent to emit state events from tool results.                 | Inputs: `ChatClientAgent`, serializer options. Outputs: `AIAgent` with state events.    | Server/AgenticUI/AgenticUIAgent.cs, Server/Program.cs                                                      |
| Agentic UI tools & models | Provide plan models and tool functions that drive state updates.            | Inputs: tool calls. Outputs: plan snapshots and JSON Patch deltas.                      | Server/AgenticUI/AgenticPlanningTools.cs, Server/AgenticUI/Plan.cs, Server/AgenticUI/JsonPatchOperation.cs |
| AG-UI endpoint mapping    | Expose `/ag-ui` endpoint for agent interactions.                            | Inputs: `AIAgent` (wrapped). Outputs: HTTP endpoint.                                    | Server/Program.cs                                                                                          |
| Server launch profile     | Configure local host/port for the server.                                   | Inputs: launch settings. Outputs: application URL.                                      | Server/Properties/launchSettings.json                                                                      |

## Client Host Modules

| Module                    | Responsibility                                                          | Inputs/Outputs                                                      | Primary files                         |
| ------------------------- | ----------------------------------------------------------------------- | ------------------------------------------------------------------- | ------------------------------------- |
| Client host bootstrap     | Configure Razor components, middleware, static assets, and render mode. | Inputs: environment. Outputs: running Blazor Server app.            | Client/Program.cs                     |
| AG-UI client registration | Configure `HttpClient` and `AGUIChatClient` for the server endpoint.    | Inputs: `SERVER_URL`. Outputs: `IChatClient` service.               | Client/Program.cs                     |
| Client launch profile     | Configure local host/port and server URL env var.                       | Inputs: launch settings. Outputs: application URL and `SERVER_URL`. | Client/Properties/launchSettings.json |

## Client App Shell Modules

| Module    | Responsibility                                           | Inputs/Outputs                                     | Primary files                                                                   |
| --------- | -------------------------------------------------------- | -------------------------------------------------- | ------------------------------------------------------------------------------- |
| App shell | Provide HTML shell, global styles, and script bootstrap. | Inputs: static assets. Outputs: page shell.        | Client/Components/App.razor, Client/wwwroot/app.css, Client/wwwroot/favicon.png |
| Routing   | Configure the Blazor router and layout.                  | Inputs: route data. Outputs: component navigation. | Client/Components/Routes.razor                                                  |
| Layout    | Provide the app layout and error UI.                     | Inputs: body content. Outputs: layout markup.      | Client/Components/Layout/MainLayout.razor                                       |

## Chat Page & UI Modules

| Module                 | Responsibility                                                           | Inputs/Outputs                                                               | Primary files                                                                                             |
| ---------------------- | ------------------------------------------------------------------------ | ---------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Chat page orchestrator | Coordinate chat flow, agent calls, and child components.                 | Inputs: user messages, `IChatClient`. Outputs: streamed responses, UI state. | Client/Components/Pages/Chat/Chat.razor                                                                   |
| Chat header            | Display header and “New chat” action.                                    | Inputs: UI callbacks. Outputs: header UI.                                    | Client/Components/Pages/Chat/ChatHeader.razor                                                             |
| Message list           | Render the scrollable list and auto-scroll behaviors.                    | Inputs: message collection. Outputs: list UI.                                | Client/Components/Pages/Chat/ChatMessageList.razor, Client/Components/Pages/Chat/ChatMessageList.razor.js |
| Message item           | Render user/assistant messages, citations, and Agentic UI state updates. | Inputs: message model, state events. Outputs: message UI.                    | Client/Components/Pages/Chat/ChatMessageItem.razor, Client/Components/Pages/Chat/ChatCitation.razor       |
| Chat input             | Provide input box, keyboard shortcuts, and resizing.                     | Inputs: user text. Outputs: submit events.                                   | Client/Components/Pages/Chat/ChatInput.razor, Client/Components/Pages/Chat/ChatInput.razor.js             |
| Suggestions            | Render follow-up suggestions.                                            | Inputs: suggestion list. Outputs: suggestion UI events.                      | Client/Components/Pages/Chat/ChatSuggestions.razor                                                        |
| Loading indicator      | Show streaming/progress feedback.                                        | Inputs: loading state. Outputs: spinner UI.                                  | Client/Components/Layout/LoadingSpinner.razor                                                             |

## Styling & Static Assets

| Module           | Responsibility                         | Inputs/Outputs                                        | Primary files                                                                  |
| ---------------- | -------------------------------------- | ----------------------------------------------------- | ------------------------------------------------------------------------------ |
| Global styles    | Base theme, layout, and shared styles. | Inputs: CSS. Outputs: visual theme.                   | Client/wwwroot/app.css                                                         |
| Component styles | Component-specific layout and styling. | Inputs: CSS per component. Outputs: scoped UI styles. | Client/Components/Pages/Chat/*.razor.css, Client/Components/Layout/*.razor.css |
| Static assets    | Icons and static files.                | Inputs: assets. Outputs: static file responses.       | Client/wwwroot/favicon.png                                                     |
