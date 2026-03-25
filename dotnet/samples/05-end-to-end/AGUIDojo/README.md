# AGUIDojo Sample

<!-- MY CUSTOMIZATION POINT: align sample doc links with the current AGUIDojo session research set -->

AGUIDojo is a full end-to-end sample that now uses a **single AG-UI route** on the server: `POST /chat`.
The client keeps per-session UI state locally as a cache/draft convenience, while the server owns
the durable session catalog, canonical branching conversation graph, and workspace projections for
plans, approvals, audit history, documents, charts/forms, and data grids.

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

## Model and context-window behavior

The sample now exposes a **per-session model picker** on the unified `/chat` route:

- the client loads a server model catalog from `/api/models` and renders the current session's preferred model in the chat sidebar
- model selection is stored with session metadata, round-tripped through browser persistence, and restored during hydration
- each chat turn forwards the selected model through AG-UI request metadata so the server can route the effective model per session
- the server records both the preferred and effective model IDs and applies model-aware compaction before invoking the provider
- the unified route remains `POST /chat`; model choice is still request metadata, not endpoint topology

The current implementation keeps browser storage as session cache/import support only. Durable
re-entry, inspection, and model-awareness now come from the server-owned session APIs plus the
unified `/chat` route.

## Inspection and portability surfaces

For local debugging and support-style inspection, the sample exposes:

- `GET /api/chat-sessions` for the session catalog
- `GET /api/chat-sessions/{id}` for thin session detail
- `GET /api/chat-sessions/{id}/conversation` for the canonical branching graph
- `GET /api/chat-sessions/{id}/workspace` for approvals, audit, plan/artifact state, and file references

The sample keeps this foundation **SQL-first**: SQLite is the local default, while SQL Server or
PostgreSQL remain the intended modular-monolith portability targets.

Related docs:

- [Documentation index](./.docs/index.md)
- [Roadmap issue: AGUIDojo implementation plan](./.issues/260323_aguidojo-implementation-plan.md)
- [Issue index](./.issues/index.md)
- [Explanation: AGUIDojo system design](./.docs/explanation/agui-dojo/system-design.md)
- [Explanation: AGUIDojo LLM picker architecture and MAF alignment](./.docs/explanation/agui-dojo/aguidojo-llm-picker-architecture-and-maf-alignment.md)
- [Explanation: Server-side primary persistence for AGUIDojo chat sessions](./.docs/explanation/agui-dojo/server-side-persistence-for-chat-session.md)
- [Explanation: Copilot CLI patterns relevant to AGUIDojo](./.docs/explanation/copilot/copilot-cli-session-context-and-instruction-patterns.md)
- [Explanation: Copilot CLI public repo grounding for AGUIDojo](./.docs/explanation/copilot/copilot-cli-public-repo-grounding.md)
- [Reference: Copilot CLI session state schema reference](./.docs/reference/copilot/copilot-cli-session-state-schema.md)
- [Explanation: Copilot CLI session topology and orchestration layer](./.docs/explanation/copilot/copilot-cli-session-topology.md)

The Copilot CLI research links ground durable-session topology and inspectable session surfaces. They are
reference material, not a directive to mirror Copilot CLI's `~/.copilot/` filesystem or exact session
schema inside AGUIDojo.

## Running locally

### Prerequisites

- .NET 10 SDK
- One configured model provider for `AGUIDojoServer` (the current sample still uses one process-wide default model)

Example server environment:

```bash
export OPENAI_API_KEY="sk-your-api-key"
# optional
export OPENAI_MODEL="gpt-5.4-nano"
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
- The per-session model picker described in `.docs/explanation/agui-dojo/system-design.md` and `.issues/260323_aguidojo-implementation-plan.md` is now implemented and persists through browser/server hydration.
- The context-window direction is full-history submission plus server-side compaction, with model-aware thresholds driven by the selected session model.
