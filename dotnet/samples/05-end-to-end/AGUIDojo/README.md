# AGUIDojo Sample

AGUIDojo is a full end-to-end sample that now uses a **single AG-UI route** on the server: `POST /chat`.
The client keeps per-session UI state locally and renders plans, approvals, recipe state,
document previews, charts, forms, and data grids from the unified event stream.

## Projects

| Project | Description |
| --- | --- |
| **[AGUIDojoServer](./AGUIDojoServer)** | Minimal API backend exposing `POST /chat`, business APIs, auth, health checks, and OpenTelemetry. |
| **[AGUIDojoClient](./AGUIDojoClient)** | Blazor Server BFF UI with session sidebar, streaming chat, notifications, canvas artifacts, and YARP for `/api/*`. |
| **[AGUIDojo.AppHost](./AGUIDojo.AppHost)** | Aspire host for running the client and server together. |
| **[AGUIDojoServer.Tests](./AGUIDojoServer.Tests)** | Focused server tests. |

## Unified architecture

```text
Browser
  │
  ▼
AGUIDojoClient (Blazor Server)
  ├─ Direct AG-UI streaming to AGUIDojoServer /chat
  ├─ Session-scoped state store for chat, approvals, plans, artifacts, and notifications
  └─ YARP proxy for /api/* business endpoints
        │
        ▼
AGUIDojoServer (Minimal API)
  ├─ POST /chat          unified agent route
  ├─ /api/weather/*      shared business API
  ├─ /api/email          shared business API
  ├─ /api/auth/dev/token development auth helper
  └─ /health             readiness/liveness
```

### What `/chat` does

The server composes one agent pipeline that layers:

- tool result streaming
- human-in-the-loop approval handling
- plan state updates
- recipe/shared-state updates
- predictive document streaming

The agent can call tools such as:

- `get_weather`
- `send_email`
- `write_document`
- `create_plan`
- `update_plan_step`
- `show_chart`
- `show_data_grid`
- `show_form`

## Client model

The client no longer switches between separate AG-UI endpoints. Instead it maintains **session-keyed UI state**:

- each session has its own conversation, artifacts, status, unread count, and approval state
- the sidebar lets you switch between sessions while background sessions continue streaming
- the canvas pane renders artifacts for the active session only
- notifications surface background completions, errors, and approval requests

Artifacts appear on demand from the unified stream:

- **Plan** → plan sheet + diff preview
- **Recipe state** → recipe editor
- **Document updates** → preview / Monaco diff
- **Data grid** → tabbed table artifact
- **Charts / forms / weather** → message or canvas rendering via `ToolComponentRegistry`

## Running locally

### Prerequisites

- .NET 10 SDK
- One configured model provider for `AGUIDojoServer`

Example server environment:

```bash
export OPENAI_API_KEY="sk-your-api-key"
# optional
export OPENAI_MODEL="gpt-5.4-mini"
```

Optional:

```bash
export SERVER_URL="http://localhost:5100"
export ASPNETCORE_ENVIRONMENT="Development"
export Jwt__SigningKey="your-256-bit-key"
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"
```

### Start the sample

```bash
# terminal 1
cd AGUIDojoServer
dotnet run

# terminal 2
cd AGUIDojoClient
dotnet run
```

Open the client URL shown in the console, typically `http://localhost:6001`.

## Useful checks

```bash
curl http://localhost:5100/health
curl http://localhost:5100/api/weather/Seattle
curl http://localhost:6001/api/weather/Seattle
```

## Example prompts

Try these in the chat UI:

- “What’s the weather in Seattle?”
- “Create a three-step launch plan for a developer meetup.”
- “Draft a short project update email to the team.”
- “Write a short markdown announcement about AG-UI.”
- “Show a data grid of sample cloud regions and latency.”
- “Show a chart comparing quarterly revenue.”
- “Build a recipe for a spicy vegetarian pasta.”

## Notes

- Business APIs still flow through YARP on the client.
- AG-UI streaming still goes directly from client to server to avoid proxy buffering.
- Session metadata may retain a route hint for existing worktree state, but all new traffic is sent to `/chat`.
