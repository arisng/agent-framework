# AGUIDojoClient - AG-UI Protocol Demonstration Web Client

A Blazor Server web application that demonstrates all 7 AG-UI protocol features by connecting to the AGUIDojoServer. This application provides a visual, interactive way to explore the full capabilities of the AG-UI (Agentic Generative UI) protocol.

## Overview

AGUIDojoClient is a single-page Blazor application that connects to AGUIDojoServer via HTTP POST with Server-Sent Events (SSE) streaming. It allows users to switch between 7 different AG-UI endpoints from a dropdown menu, each demonstrating a specific protocol feature.

## Prerequisites

### Required
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- AGUIDojoServer running locally (see [AGUIDojoServer setup](#running-the-server))

### Environment Variables

Configure one of the following AI model providers:

**Option 1: Azure OpenAI (Recommended)**
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4.1-mini"
```

> **Note:** Uses `DefaultAzureCredential` for authentication. Ensure you're authenticated with Azure (e.g., via `az login`).

**Option 2: OpenAI API**
```bash
export OPENAI_API_KEY="sk-your-api-key"
export OPENAI_MODEL_NAME="gpt-4.1-mini"  # Optional, defaults to gpt-4.1-mini
```

## Running the Sample

### Step 1: Start the AGUIDojoServer

In a terminal window:
```bash
cd dotnet/samples/AGUIClientServer/AGUIDojoServer
dotnet build
dotnet run
```

The server starts at `http://localhost:5100` and exposes 7 AG-UI endpoints.

### Step 2: Start the AGUIDojoClient

In a separate terminal window:
```bash
cd dotnet/samples/AGUIClientServer/AGUIDojoClient
dotnet build
dotnet run
```

The client starts at `http://localhost:5000` (or `https://localhost:5001`).

### Step 3: Interact with the Application

1. Open your browser and navigate to `http://localhost:5000`
2. Use the endpoint dropdown in the header to select an AG-UI feature
3. Type a message in the chat input and press Enter
4. Observe the streaming response and feature-specific UI components

## The 7 AG-UI Protocol Features

### Feature 1: Agentic Chat
**Endpoint:** `/agentic_chat`

Basic streaming chat with automatic tool calling. This is the foundational AG-UI feature.

**Demo:** Type any question like "What is the capital of France?" and observe streaming text responses.

---

### Feature 2: Backend Tool Rendering
**Endpoint:** `/backend_tool_rendering`

Tools execute server-side and results stream to the client with specialized rendering.

**Demo:** Ask "What's the weather in Seattle?" to trigger the `get_weather` tool. The response displays in a formatted WeatherCard component showing temperature, conditions, humidity, and wind speed.

---

### Feature 3: Human-in-the-Loop
**Endpoint:** `/human_in_the_loop`

Approval workflows for sensitive tool calls. The agent pauses and requests user consent before executing certain actions.

**Demo:** Ask "Book a flight to Paris" - an approval dialog appears. Click "Approve" or "Reject" to control whether the action proceeds.

---

### Feature 4: Agentic Generative UI
**Endpoint:** `/agentic_generative_ui`

Async tools with progress updates for long-running operations. Displays a Plan with Steps that update in real-time.

**Demo:** Ask "Plan a trip to Tokyo" - a PlanProgress component appears showing steps like "Research destinations", "Book flights", etc. Watch each step transition from "Pending" to "In Progress" to "Completed".

---

### Feature 5: Tool-Based UI Rendering
**Endpoint:** `/tool_based_generative_ui`

Custom Blazor components render based on tool definitions. The client uses a ToolComponentRegistry to map tool names to UI components.

**Demo:** Ask "What's the weather in New York?" - you'll see the tool call displayed with its arguments (location). The assistant then provides weather information in its text response.

> **Note on WeatherDisplay Component:** The WeatherDisplay component infrastructure is fully implemented and registered in the ToolComponentRegistry. However, due to the AG-UI protocol's tool execution model, the component currently does not render. When tools execute server-side via the `FunctionInvokingChatClient`, tool results are consumed internally by the LLM to generate text responses rather than being streamed as separate `FunctionResultContent` events. The component would render correctly if `FunctionResultContent` were streamed separately. See [Known Limitations](#known-limitations) for more details.

---

### Feature 6: Shared State
**Endpoint:** `/shared_state`

Bidirectional state synchronization between the agent and client. A RecipeEditor appears where you can edit recipe data that's shared with the server.

**Demo:** 
1. Edit the recipe name, ingredients, or instructions in the RecipeEditor panel
2. Ask "Generate a recipe for chocolate cake"
3. The agent receives your current state and generates a recipe, which updates the RecipeEditor automatically

---

### Feature 7: Predictive State Updates
**Endpoint:** `/predictive_state_updates`

Stream tool arguments as optimistic updates before execution completes. Shows document content progressively as it's being generated.

**Demo:** Ask "Write a document about AI" - a DocumentPreview component appears with a "Streaming..." indicator, showing partial content that grows in real-time. Once complete, it transitions to "Finalized" state.

---

## Project Structure

```
AGUIDojoClient/
├── Program.cs                      # Application entry point, DI configuration
├── AGUIDojoClient.csproj           # Project file
├── Components/
│   ├── App.razor                   # Application root
│   ├── Routes.razor                # Routing configuration
│   ├── _Imports.razor              # Global using directives
│   ├── Layout/                     # Main layout components
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── Pages/
│   │   └── Chat/                   # Main chat UI components
│   │       ├── Chat.razor          # Primary chat page with endpoint switching
│   │       ├── ChatHeader.razor    # Header with endpoint dropdown
│   │       ├── ChatInput.razor     # Message input component
│   │       ├── ChatMessageItem.razor # Message rendering (text, tools, errors)
│   │       ├── ChatMessageList.razor
│   │       └── ChatSuggestions.razor
│   ├── Approvals/                  # Human-in-the-Loop components
│   │   └── ApprovalDialog.razor
│   ├── GenerativeUI/               # Agentic Generative UI components
│   │   └── PlanProgress.razor
│   ├── PredictiveUI/               # Predictive State Updates components
│   │   └── DocumentPreview.razor
│   ├── SharedState/                # Shared State components
│   │   └── RecipeEditor.razor
│   └── ToolResults/                # Tool-specific rendering components
│       └── WeatherDisplay.razor
├── Models/                         # Data models (mirrored from server)
│   ├── DocumentState.cs
│   ├── Ingredient.cs
│   ├── JsonPatchOperation.cs
│   ├── Plan.cs
│   ├── Recipe.cs
│   ├── Step.cs
│   ├── StepStatus.cs
│   └── WeatherInfo.cs
├── Services/                       # Business logic services
│   ├── AGUIChatClientFactory.cs    # Creates clients for different endpoints
│   ├── ApprovalHandler.cs          # Handles approval workflows
│   ├── JsonPatchApplier.cs         # Applies JSON Patch for state updates
│   ├── SseEventParser.cs           # Parses raw SSE for STATE_DELTA events
│   ├── StateManager.cs             # Manages bidirectional shared state
│   └── ToolComponentRegistry.cs    # Maps tool names to Blazor components
└── wwwroot/                        # Static assets (CSS, JS)
```

## Configuration

The server URL can be configured via the `SERVER_URL` setting:

**appsettings.json:**
```json
{
  "SERVER_URL": "http://localhost:5100"
}
```

**Environment variable:**
```bash
export SERVER_URL="http://localhost:5100"
```

## Architecture

### Client-Server Communication

1. **HTTP POST** - Client sends messages to the selected endpoint
2. **SSE Streaming** - Server responds with Server-Sent Events containing:
   - `TEXT_MESSAGE_CONTENT` - Streamed text chunks
   - `TOOL_CALL_START/ARGS/END` - Tool execution events
   - `STATE_SNAPSHOT` - Full state updates
   - `STATE_DELTA` - Incremental state changes (JSON Patch)

### Key Services

| Service | Purpose |
|---------|---------|
| `AGUIChatClientFactory` | Creates `IChatClient` instances for any of the 7 endpoints |
| `ApprovalHandler` | Extracts approval requests from tool calls, creates responses |
| `JsonPatchApplier` | Applies RFC 6902 JSON Patch operations to Plan models |
| `StateManager` | Manages Recipe state for bidirectional sync |
| `SseEventParser` | Parses raw SSE to capture STATE_DELTA events |
| `ToolComponentRegistry` | Maps tool names to Blazor component types |

## Troubleshooting

### Server Connection Issues
- Ensure AGUIDojoServer is running on `http://localhost:5100`
- Check that CORS is properly configured on the server
- Verify environment variables for AI model access

### Feature Not Working
- Use browser DevTools to inspect network requests
- Check console output on both client and server
- Ensure you've selected the correct endpoint for the feature

### Build Errors
- Run `dotnet restore` to ensure all packages are installed
- Verify .NET 10.0 SDK is installed: `dotnet --version`

## Related Samples

- [AGUIDojoServer](../AGUIDojoServer) - The AG-UI server implementation
- [AGUIClient](../AGUIClient) - Console client for AG-UI protocol testing
- [AGUIServer](../AGUIServer) - Basic AG-UI server implementation
- [AGUIWebChat](../../AGUIWebChat) - Original template this client was based on

## Known Limitations

### Tool Result Component Rendering (WeatherDisplay)

**Limitation:** Custom components for tool results (like `WeatherDisplay`) do not render even though the infrastructure (ToolComponentRegistry, DynamicComponent, parsing logic) is fully implemented.

**Cause:** The AG-UI protocol supports streaming `FunctionResultContent` via `ToolCallResultEvent`. However, when using `ChatClientAgent` with the `FunctionInvokingChatClient` from `Microsoft.Extensions.AI`, tool execution happens internally:

1. LLM requests a tool call
2. `FunctionInvokingChatClient` intercepts and executes the tool
3. Tool result is fed back to the LLM (not streamed to client)
4. LLM generates a text response incorporating the result

The `FunctionCallContent` (tool invocation) IS streamed, which is why you see tool calls in the UI. But `FunctionResultContent` (tool output) is NOT separately streamed—it's consumed internally.

**Impact:**
- ✅ Tool calls display correctly (name, arguments)
- ✅ Weather information appears in assistant's text response
- ❌ WeatherDisplay card component does not render

**Workarounds:**
1. **Use DataContent:** For custom visualizations, emit state via `DataContent` events (as demonstrated in Shared State and Predictive State Updates features)
2. **Parse TextContent:** Extract structured data from the assistant's natural language response
3. **Future Enhancement:** The framework could add an option to stream `FunctionResultContent` separately before passing to the LLM

**Infrastructure Ready:** The client-side code correctly handles `FunctionResultContent` and would render `WeatherDisplay` if such events were received. The integration tests in `ToolCallingTests.cs` verify this capability.
