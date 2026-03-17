# AGUIDojo Sample

This directory contains the standalone AGUIDojo sample, preserving the current branch and working tree state for the AGUIDojo app host, client, server, tests, and supporting docs.

## Project Overview

| Project | Type | Description |
| --- | --- | --- |
| **[AGUIDojoServer](./AGUIDojoServer)** | Unified Backend | Full-featured server with AG-UI endpoints, business APIs, JWT auth, OpenTelemetry, and shared services |
| **[AGUIDojoClient](./AGUIDojoClient)** | Blazor Server BFF | Production-ready Blazor web chat UI with YARP proxy, AG-UI feature demos, Polly resilience, and health checks |
| **[AGUIDojo.AppHost](./AGUIDojo.AppHost)** | Aspire App Host | Aspire app host for running the AGUIDojo server and client together with health monitoring |
| **[AGUIDojoServer.Tests](./AGUIDojoServer.Tests)** | xUnit Tests | Lightweight tests covering AGUIDojo server behavior |

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
├── AGUIDojoChatClientAgentFactory.cs # DI-based agent factory with OpenTelemetry wrapping
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

For local development, set secrets via `dotnet user-secrets` in the AGUIDojoServer project:

```bash
cd AGUIDojoServer
dotnet user-secrets set "OPENAI_API_KEY" "sk-your-api-key"
```

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
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-5-mini"
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
