# AG-UI Client and Server Samples

This directory contains samples demonstrating how to use the AG-UI (Agentic Generative UI) protocol to enable communication between client applications and remote agent servers. The AG-UI protocol provides a standardized way for clients to interact with AI agents through Server-Sent Events (SSE) streaming.

## Samples Overview

| Sample                                 | Type              | Description                                                                                                    |
| -------------------------------------- | ----------------- | -------------------------------------------------------------------------------------------------------------- |
| **[AGUIServer](./AGUIServer)**         | Minimal API       | ASP.NET Core server exposing an AI agent via the AG-UI protocol                                                |
| **[AGUIClient](./AGUIClient)**         | Console App       | Console client that connects to the AG-UI server and displays streaming updates                                |
| **[AGUIDojoServer](./AGUIDojoServer)** | Unified Backend   | Full-featured server with 7 AG-UI endpoints, business APIs, JWT auth, OpenTelemetry, and shared services       |
| **[AGUIDojoClient](./AGUIDojoClient)** | Blazor Server BFF | Production-ready Blazor web chat UI with YARP proxy, all 7 AG-UI features, Polly resilience, and health checks |

## Architecture Overview

The AGUIDojoClient and AGUIDojoServer implement a **Backend-for-Frontend (BFF) pattern** with YARP reverse proxy integration:

```
┌─────────────────────────────────────────────────────────────────────┐
│                AGUIDojoClient (Blazor Server :6000/6001)            │
│                              [BFF Layer]                            │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────┐  ┌─────────────────────────────────────┐  │
│  │   Agentic Chat UI   │  │     Business API Consumption        │  │
│  │  (Direct HttpClient │  │     (YARP Reverse Proxy)            │  │
│  │   to SSE endpoints) │  │     /api/* → :5100/api/*            │  │
│  └─────────────────────┘  └─────────────────────────────────────┘  │
│                                                                     │
│  Features: OpenTelemetry · Polly Resilience · Health Checks         │
└─────────────────────────────────────────────────────────────────────┘
                     │                              │
                     │ SSE Streaming                 │ YARP Proxied
                     ▼                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                AGUIDojoServer (Minimal API :5100)                    │
│                         [Unified Backend]                            │
├─────────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────┐  ┌───────────────────────────────────┐   │
│  │     AG-UI Layer      │  │       Business API Layer          │   │
│  │    (MapAGUI POST)    │  │       (Minimal API GET/POST)      │   │
│  ├──────────────────────┤  ├───────────────────────────────────┤   │
│  │ /agentic_chat        │  │ GET  /api/weather/{location}      │   │
│  │ /backend_tool_*      │  │ POST /api/email                   │   │
│  │ /human_in_the_loop   │  │ POST /api/auth/token (dev only)   │   │
│  │ /shared_state        │  │ GET  /health                      │   │
│  │ /predictive_state_*  │  └───────────────────────────────────┘   │
│  └──────────────────────┘                                           │
│                     │                       │                       │
│                     ▼                       ▼                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │               Shared Business Services (DI)                  │   │
│  ├──────────────────────────────────────────────────────────────┤   │
│  │  IWeatherService  │  IEmailService  │  IDocumentService      │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                     ▲                       ▲                       │
│  ┌──────────────────────┐  ┌───────────────────────────────────┐   │
│  │      AI Tools        │  │       API Handlers                │   │
│  │  (KeyedSingleton,    │  │    (inject same services)         │   │
│  │   IHttpContextAccessor│ │                                   │   │
│  │   for scoped access) │  │                                   │   │
│  └──────────────────────┘  └───────────────────────────────────┘   │
│                                                                     │
│  Features: JWT Auth · OpenTelemetry · ProblemDetails · Health Checks│
└─────────────────────────────────────────────────────────────────────┘
```

**Key Design Decisions:**
- **YARP Reverse Proxy**: Business APIs (`/api/*`) route through YARP for unified routing; Blazor routes precede YARP (`Order = int.MaxValue`) to avoid conflicts
- **Direct SSE Streaming**: AG-UI endpoints use direct `HttpClient` to `:5100` (bypassing YARP) for optimal streaming without buffering
- **Shared Services**: `IWeatherService`, `IEmailService`, `IDocumentService` registered as Scoped, shared between Minimal API handlers and AI Tools
- **AI Tool DI Pattern**: Tools are `KeyedSingleton` and use `IHttpContextAccessor.HttpContext.RequestServices` to access scoped services
- **No Route Conflicts**: MapAGUI uses POST only; Minimal API endpoints use `/api/` prefix with GET/POST

## AG-UI Protocol Features

The AGUIDojoServer and AGUIDojoClient samples demonstrate all 7 standardized AG-UI protocol features:

| #   | Feature                      | Endpoint                    | Server Component     | Client Component                                     | Description                                                                                 |
| --- | ---------------------------- | --------------------------- | -------------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| 1   | **Agentic Chat**             | `/agentic_chat`             | `ChatClientAgent`    | `Chat.razor`, `ChatMessageItem.razor`                | Streaming chat with automatic tool calling, collapsible thought blocks                      |
| 2   | **Backend Tool Rendering**   | `/backend_tool_rendering`   | `WeatherTool`        | `WeatherDisplay.razor`                               | Server-side tool execution with typed results rendered in custom Blazor components          |
| 3   | **Human-in-the-Loop**        | `/human_in_the_loop`        | `ApprovalTool`       | `ApprovalDialog.razor`                               | Approval workflows with distinct styling for approved (green ✓) vs rejected (red ✗) results |
| 4   | **Agentic Generative UI**    | `/agentic_generative_ui`    | Async tools          | `PlanProgress.razor`                                 | Long-running operations with real-time progress step updates                                |
| 5   | **Tool-Based UI Rendering**  | `/tool_based_generative_ui` | `WeatherTool`        | `WeatherDisplay.razor` (via `ToolComponentRegistry`) | `DynamicComponent` rendering based on tool definitions                                      |
| 6   | **Shared State**             | `/shared_state`             | `STATE_DELTA` events | `RecipeEditor.razor`                                 | Bidirectional state sync between agent and client via JSON Patch                            |
| 7   | **Predictive State Updates** | `/predictive_state_updates` | `DocumentTool`       | `DocumentPreview.razor`                              | Optimistic UI updates by streaming tool arguments as they arrive                            |

### Client UX Features

The Blazor client includes production-quality UX patterns:

- **Collapsible Thought Blocks** (`AssistantThought.razor`): Non-text content (tool calls, tool results, data) wrapped in collapsible "🤔 Thinking (N steps)" blocks that auto-collapse on completion
- **Sorted Message Display**: Contents sorted by type priority (tool-call → tool-result → data → text → error) for logical reading order
- **HITL Rejection Styling**: Rejected tool calls display with red/orange theme and ✗ icon; approved calls show green ✓
- **Markdown Rendering**: Agent responses rendered as formatted Markdown via `MarkdownService`

### Business APIs (via Minimal API)

| Method | Endpoint                  | Auth Required | Description                                           |
| ------ | ------------------------- | ------------- | ----------------------------------------------------- |
| GET    | `/api/weather/{location}` | Yes           | Get weather for a location (shared `IWeatherService`) |
| POST   | `/api/email`              | Yes           | Send email via shared `IEmailService`                 |
| POST   | `/api/auth/token`         | No (dev only) | Generate JWT token for development testing            |
| GET    | `/health`                 | No            | Health check endpoint                                 |

## Project Structure

### AGUIDojoServer

```
AGUIDojoServer/
├── Program.cs                     # Entry point: DI, JWT auth, OpenTelemetry, MapAGUI + Minimal API
├── ChatClientAgentFactory.cs      # DI-based agent factory with OpenTelemetry wrapping
├── AGUIDojoServerSerializerContext.cs  # JSON source gen context
├── Services/                      # Shared business services (Scoped)
│   ├── IWeatherService.cs / WeatherService.cs
│   ├── IEmailService.cs / EmailService.cs
│   └── IDocumentService.cs / DocumentService.cs
├── Tools/                         # DI-compatible AI tools (KeyedSingleton)
│   ├── WeatherTool.cs             # Uses IHttpContextAccessor for scoped service access
│   ├── EmailTool.cs
│   └── DocumentTool.cs
├── Api/                           # Minimal API endpoint groups
│   ├── WeatherEndpoints.cs        # RequireAuthorization
│   ├── EmailEndpoints.cs          # RequireAuthorization
│   └── AuthEndpoints.cs           # Dev-only JWT token generation
├── AgenticUI/                     # Feature-specific agent configurations
├── BackendToolRendering/
├── HumanInTheLoop/
├── SharedState/
├── PredictiveStateUpdates/
└── appsettings.{Environment}.json # Multi-environment configuration
```

### AGUIDojoClient

```
AGUIDojoClient/
├── Program.cs                     # Entry point: YARP, OpenTelemetry, Polly, health checks
├── Components/
│   ├── Pages/Chat/                # Core chat UI
│   │   ├── Chat.razor             # Main chat page with feature dropdown
│   │   ├── ChatHeader.razor       # Header with endpoint selector
│   │   ├── ChatMessageItem.razor  # Individual message rendering
│   │   ├── ChatMessageList.razor  # Scrollable message container
│   │   ├── ChatInput.razor        # User input with send button
│   │   ├── ChatSuggestions.razor  # Suggested prompts per feature
│   │   ├── ChatCitation.razor     # Citation rendering
│   │   └── AssistantThought.razor # Collapsible thought blocks
│   ├── Approvals/
│   │   └── ApprovalDialog.razor   # HITL approve/reject dialog
│   ├── GenerativeUI/
│   │   └── PlanProgress.razor     # Async operation progress display
│   ├── ToolResults/
│   │   └── WeatherDisplay.razor   # Weather card with temperature, conditions
│   ├── SharedState/
│   │   └── RecipeEditor.razor     # Bidirectional state sync editor
│   └── PredictiveUI/
│       └── DocumentPreview.razor  # Optimistic document preview
├── Services/
│   ├── IAGUIChatClientFactory.cs / AGUIChatClientFactory.cs  # SSE client factory
│   ├── ApprovalHandler.cs         # HITL approval state management
│   ├── IMarkdownService.cs / MarkdownService.cs              # Markdown → HTML rendering
│   ├── IStateManager.cs / StateManager.cs                    # Shared state management
│   ├── IToolComponentRegistry.cs / ToolComponentRegistry.cs  # DynamicComponent registration
│   ├── IWeatherApiClient.cs / WeatherApiClient.cs            # Typed HTTP client (YARP-proxied)
│   ├── JsonPatchApplier.cs        # JSON Patch operations for STATE_DELTA
│   └── EndpointInfo.cs            # AG-UI endpoint metadata
├── Models/
│   ├── WeatherInfo.cs, Plan.cs, Step.cs, StepStatus.cs
│   ├── Recipe.cs, Ingredient.cs
│   ├── DocumentState.cs, JsonPatchOperation.cs
└── appsettings.{Environment}.json # YARP configuration
```

## Cross-Cutting Concerns

### Authentication (JWT)

The server supports JWT Bearer authentication:
- **Development**: Use `/api/auth/token` to generate tokens for testing
- **Production**: Configure `Jwt__SigningKey` environment variable (256-bit key)
- **YARP Forwarding**: Authorization headers are automatically forwarded through the YARP proxy
- Business API endpoints require authentication (`RequireAuthorization`)

### Observability (OpenTelemetry)

Both projects are instrumented with OpenTelemetry:
- **W3C Trace Context**: Automatic trace propagation through YARP (client → proxy → backend)
- **ASP.NET Core Instrumentation**: HTTP request/response metrics and traces
- **OTLP Exporter**: Configured via `OTEL_EXPORTER_OTLP_ENDPOINT` for tools like Aspire Dashboard, Jaeger, etc.
- **Health Checks**: Both services expose `/health` endpoints

### Resilience (Polly)

The client uses Polly for resilience:
- **Circuit Breaker**: Protects against cascading failures when the backend is down
- **Configured via `Microsoft.Extensions.Http.Resilience`** on named `HttpClient` instances

### Error Handling

- **REST APIs**: Return `ProblemDetails` (RFC 9457) for structured error responses
- **SSE Streams**: Return `RunErrorEvent` within the AG-UI event stream for in-band error reporting

## Environment Variables

### AGUIDojoServer (Backend)

Configure ONE of the following LLM providers:

**Option A: OpenAI (quickest setup)**
```bash
export OPENAI_API_KEY="sk-your-api-key"
export OPENAI_MODEL="gpt-4.1-mini"  # Optional, defaults to gpt-5-mini
```

**Option B: Azure OpenAI with API Key**
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_OPENAI_API_KEY="your-api-key"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
```

**Option C: Azure OpenAI with Managed Identity (recommended for production)**
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
# No API key needed - uses DefaultAzureCredential
```

**Optional Variables:**
```bash
export ASPNETCORE_ENVIRONMENT="Development"  # Development, Staging, Production
export ASPNETCORE_URLS="http://localhost:5100"
export Jwt__SigningKey="your-256-bit-key"  # Required when auth is enabled
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"  # OpenTelemetry collector
```

### AGUIDojoClient (BFF)

```bash
export SERVER_URL="http://localhost:5100"  # Backend server URL
export ASPNETCORE_ENVIRONMENT="Development"
```

**YARP Backend Override (production):**
```bash
export ReverseProxy__Clusters__backend__Destinations__primary__Address="https://api.production.example.com/"
```

## Quick Start: Full Demo

### Development Setup

```bash
# Terminal 1: Start the backend server
cd AGUIDojoServer
dotnet build
dotnet run

# Terminal 2: Start the Blazor BFF client
cd AGUIDojoClient
dotnet build
dotnet run
```

Open http://localhost:6001 and use the dropdown to switch between the 7 AG-UI features.

### Production Setup

```bash
# Set production environment
export ASPNETCORE_ENVIRONMENT=Production

# Configure production secrets (example for Azure OpenAI)
export AZURE_OPENAI_ENDPOINT="https://prod-resource.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
export Jwt__SigningKey="your-production-256-bit-key"

# Configure YARP to point to production backend
export ReverseProxy__Clusters__backend__Destinations__primary__Address="https://api.yourdomain.com/"

# Start server (uses appsettings.Production.json)
cd AGUIDojoServer && dotnet run

# Start client BFF
cd AGUIDojoClient && dotnet run
```

### Verify Setup

```bash
# Check server health
curl http://localhost:5100/health

# Check client health (verifies backend connectivity)
curl http://localhost:6001/health

# Test business API directly
curl http://localhost:5100/api/weather/Seattle

# Test via YARP proxy (from client)
curl http://localhost:6001/api/weather/Seattle
```

---

## Basic Sample: AGUIServer + AGUIClient

The basic demonstration has two components:

1. **AGUIServer** - An ASP.NET Core web server that hosts an AI agent and exposes it via the AG-UI protocol
2. **AGUIClient** - A console application that connects to the AG-UI server and displays streaming updates

> **Warning**
> The AG-UI protocol is still under development and changing.
> We will try to keep these samples updated as the protocol evolves.

## Configuring Environment Variables

Configure the required Azure OpenAI environment variables:

```powershell
$env:AZURE_OPENAI_ENDPOINT="<<your-model-endpoint>>"
$env:AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4.1-mini"
```

> **Note:** This sample uses `DefaultAzureCredential` for authentication. Make sure you're authenticated with Azure (e.g., via `az login`, Visual Studio, or environment variables).

## Running the Sample

### Step 1: Start the AG-UI Server

```bash
cd AGUIServer
dotnet build
dotnet run --urls "http://localhost:5100"
```

The server will start and listen on `http://localhost:5100`.

### Step 2: Testing with the REST Client (Optional)

Before running the client, you can test the server using the included `.http` file:

1. Open [./AGUIServer/AGUIServer.http](./AGUIServer/AGUIServer.http) in Visual Studio or VS Code with the REST Client extension
2. Send a test request to verify the server is working
3. Observe the server-sent events stream in the response

Sample request:
```http
POST http://localhost:5100/
Content-Type: application/json

{
  "threadId": "thread_123",
  "runId": "run_456",
  "messages": [
    {
      "role": "user",
      "content": "What is the capital of France?"
    }
  ],
  "context": {}
}
```

### Step 3: Run the AG-UI Client

In a new terminal window:

```bash
cd AGUIClient
dotnet run
```

Optionally, configure a different server URL:

```powershell
$env:AGUI_SERVER_URL="http://localhost:5100"
```

### Step 4: Interact with the Agent

1. The client will connect to the AG-UI server
2. Enter your message at the prompt
3. Observe the streaming updates with color-coded output:
   - **Yellow**: Run started notification showing thread and run IDs
   - **Cyan**: Agent's text response (streamed character by character)
   - **Green**: Run finished notification
   - **Red**: Error messages (if any occur)
4. Type `:q` or `quit` to exit

## Sample Output

```
AGUIClient> dotnet run
info: AGUIClient[0]
      Connecting to AG-UI server at: http://localhost:5100

User (:q or quit to exit): What is the capital of France?

[Run Started - Thread: thread_abc123, Run: run_xyz789]
The capital of France is Paris. It is known for its rich history, culture, and iconic landmarks such as the Eiffel Tower and the Louvre Museum.
[Run Finished - Thread: thread_abc123, Run: run_xyz789]

User (:q or quit to exit): Tell me a fun fact about space

[Run Started - Thread: thread_abc123, Run: run_def456]
Here's a fun fact: A day on Venus is longer than its year! Venus takes about 243 Earth days to rotate once on its axis, but only about 225 Earth days to orbit the Sun.
[Run Finished - Thread: thread_abc123, Run: run_def456]

User (:q or quit to exit): :q
```

## How It Works

### Server Side

The `AGUIServer` uses the `MapAGUI` extension method to expose an agent through the AG-UI protocol:

```csharp
AIAgent agent = new OpenAIClient(apiKey)
    .GetChatClient(model)
    .AsAIAgent(
        instructions: "You are a helpful assistant.",
        name: "AGUIAssistant");

app.MapAGUI("/", agent);
```

This automatically handles:
- HTTP POST requests with message payloads
- Converting agent responses to AG-UI event streams
- Server-sent events (SSE) formatting
- Thread and run management

### Client Side

The `AGUIClient` uses the `AGUIChatClient` to connect to the remote server:

```csharp
using HttpClient httpClient = new();
var chatClient = new AGUIChatClient(
    httpClient,
    endpoint: serverUrl,
    modelId: "agui-client",
    jsonSerializerOptions: null);

AIAgent agent = chatClient.AsAIAgent(
    instructions: null,
    name: "agui-client",
    description: "AG-UI Client Agent",
    tools: []);

bool isFirstUpdate = true;
AgentResponseUpdate? currentUpdate = null;

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(messages, thread))
{
    // First update indicates run started
    if (isFirstUpdate)
    {
        Console.WriteLine($"[Run Started - Thread: {update.ConversationId}, Run: {update.ResponseId}]");
        isFirstUpdate = false;
    }
    
    currentUpdate = update;
    
    foreach (AIContent content in update.Contents)
    {
        switch (content)
        {
            case TextContent textContent:
                // Display streaming text
                Console.Write(textContent.Text);
                break;
            case ErrorContent errorContent:
                // Display error notification
                Console.WriteLine($"[Error: {errorContent.Message}]");
                break;
        }
    }
}

// Last update indicates run finished
if (currentUpdate != null)
{
    Console.WriteLine($"\n[Run Finished - Thread: {currentUpdate.ConversationId}, Run: {currentUpdate.ResponseId}]");
}
```

The `RunStreamingAsync` method:
1. Sends messages to the server via HTTP POST
2. Receives server-sent events (SSE) stream
3. Parses events into `AgentResponseUpdate` objects
4. Yields updates as they arrive for real-time display

## Key Concepts

- **Thread**: Represents a conversation context that persists across multiple runs (accessed via `ConversationId` property)
- **Run**: A single execution of the agent for a given set of messages (identified by `ResponseId` property)
- **AgentResponseUpdate**: Contains the response data with:
  - `ResponseId`: The unique run identifier
  - `ConversationId`: The thread/conversation identifier
  - `Contents`: Collection of content items (TextContent, ErrorContent, etc.)
- **Run Lifecycle**: 
  - The **first** `AgentResponseUpdate` in a run indicates the run has started
  - Subsequent updates contain streaming content as the agent processes
  - The **last** `AgentResponseUpdate` in a run indicates the run has finished
  - If an error occurs, the update will contain `ErrorContent`

---

## Troubleshooting

### Common Issues

#### Server Won't Start - "LLM provider not configured"

**Symptom**: Server starts but logs warning about missing LLM configuration.

**Solution**: Set the required environment variables for your LLM provider:
```bash
# For OpenAI
export OPENAI_API_KEY="sk-your-key"

# OR for Azure OpenAI
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
```

#### YARP Proxy Returns 502/504

**Symptom**: Requests to `/api/*` through the client return 502 Bad Gateway or 504 Gateway Timeout.

**Causes & Solutions**:
1. **Backend not running**: Ensure AGUIDojoServer is running on port 5100
   ```bash
   curl http://localhost:5100/health  # Should return healthy
   ```
2. **Timeout on long requests**: The YARP ActivityTimeout is set to 10 minutes. For longer operations, increase it in `appsettings.json`:
   ```json
   "HttpRequest": { "ActivityTimeout": "00:30:00" }
   ```

#### Blazor SignalR Connection Fails

**Symptom**: Blazor pages load but interactivity doesn't work; browser console shows SignalR connection errors.

**Causes & Solutions**:
1. **YARP routing conflict**: Blazor routes MUST be registered BEFORE YARP in `Program.cs`. YARP uses `Order = int.MaxValue` to ensure it acts as a catch-all:
   ```csharp
   app.MapRazorComponents<App>()...;  // FIRST
   app.MapReverseProxy()...;          // AFTER (Order = int.MaxValue)
   ```
2. **YARP matching `/_blazor`**: Check YARP route patterns don't match Blazor paths. Use specific patterns like `/api/{**remainder}`.

#### SSE Streaming Appears Buffered

**Symptom**: Agent responses arrive in large batches instead of character-by-character.

**Causes & Solutions**:
1. **Response buffering enabled**: YARP's `AllowResponseBuffering` is `false` by default — do NOT set it to `true`
2. **Intermediate proxy**: If behind Nginx/CDN, add `X-Accel-Buffering: no` header
3. **AG-UI endpoints bypass YARP**: The client uses direct `HttpClient` to `:5100` for SSE endpoints, not the YARP proxy

#### Authentication Token Not Forwarded

**Symptom**: Backend returns 401 Unauthorized even with valid token.

**Solution**: YARP preserves the Authorization header by default. Verify no transform is stripping it:
```csharp
builder.Services.AddReverseProxy()
    .AddTransforms(ctx => {
        ctx.AddRequestTransform(async transformContext => {
            var authHeader = transformContext.HttpContext.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader)) {
                transformContext.ProxyRequest.Headers.Authorization = 
                    System.Net.Http.Headers.AuthenticationHeaderValue.Parse(authHeader);
            }
        });
    });
```

#### HITL Rejection Causes Errors

**Symptom**: HTTP 400 error after rejecting an approval in Human-in-the-Loop feature.

**Solution**: This was a known issue where malformed conversation history (missing tool result messages after rejection) caused LLM errors. The fix resets conversation state (`ConversationId = null`) after rejection so the next message starts a fresh conversation.

### Health Check Endpoints

Both services expose health check endpoints for monitoring:

| Service        | Endpoint                       | Purpose                                  |
| -------------- | ------------------------------ | ---------------------------------------- |
| AGUIDojoServer | `http://localhost:5100/health` | Verifies server running + LLM config     |
| AGUIDojoClient | `http://localhost:6001/health` | Verifies BFF running + backend reachable |

### Viewing Logs

**Development mode** enables detailed logging:
```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

**YARP-specific logs** can be enabled in `appsettings.json`:
```json
"Logging": {
  "LogLevel": {
    "Yarp.ReverseProxy": "Debug"
  }
}
```

**OpenTelemetry traces** can be viewed by pointing the OTLP exporter to a collector:
```bash
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"
```

---

## My Customization on top of Microsoft's Codebase

As a convention, I'm using the following comment to flag my custom code changes overriding Microsoft's codebase.
This repo is a forked repo from Microsoft. I want to keep this repo's main branch aligned with Microsoft releases.
This convention will help me streamline the management of my custom code changes to make merge decisions and avoid future conflicts.
The rule is that for all codebase not written by me in this forked repo, if there are any custom changes on top of Microsoft's codebase, then this comment must be added.

This is the conventional comment:
```txt
// MY CUSTOMIZATION POINT: This is to flag my custom changes on top of Microsoft codebase.
```
