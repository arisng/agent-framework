# AGUIDojoServer (dotnet) Analysis

## Scope
This document analyzes the AGUIDojoServer sample under dotnet/samples/AGUIClientServer/AGUIDojoServer, focusing on data models, AG-UI endpoints, and runtime flow.

## Entry Point and AG-UI Endpoints
AGUIDojoServer configures ASP.NET Core with AG-UI endpoints via `MapAGUI`, each exposing a run endpoint that accepts AG-UI `RunAgentInput` and streams AG-UI events via SSE.

- `/agentic_chat`: Simple chat agent.
- `/backend_tool_rendering`: Agent with a `get_weather` tool.
- `/human_in_the_loop`: Agent configured for human-in-the-loop scenarios.
- `/tool_based_generative_ui`: Agent for tool-based UI.
- `/agentic_generative_ui`: Agent that emits plan state updates.
- `/shared_state`: Agent that consumes and updates shared state.
- `/predictive_state_updates`: Agent that emits predictive document state updates.

References:
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/Program.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/Program.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/ChatClientAgentFactory.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/ChatClientAgentFactory.cs)

## Data Models
### Backend Tool Rendering
- `WeatherInfo`: Weather response for `get_weather` tool calls.
  - Fields: `temperature`, `conditions`, `humidity`, `wind_speed`, `feelsLike`.

Reference: [dotnet/samples/AGUIClientServer/AGUIDojoServer/BackendToolRendering/WeatherInfo.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/BackendToolRendering/WeatherInfo.cs)

### Agentic UI Planning
- `Plan`: Collection of `steps`.
- `Step`: `description`, `status`.
- `StepStatus`: `Pending`, `Completed` (serialized as lowercase by patching logic).
- `JsonPatchOperation`: Patch object with `op`, `path`, `value`, `from`.

References:
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/Plan.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/Plan.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/Step.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/Step.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/StepStatus.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/StepStatus.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/JsonPatchOperation.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/JsonPatchOperation.cs)

### Shared State
- `Ingredient`: `icon`, `name`, `amount`.
- `Recipe`: `title`, `skill_level`, `cooking_time`, `special_preferences`, `ingredients`, `instructions`.
- `RecipeResponse`: Wrapper containing `recipe`.

References:
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/Ingredient.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/Ingredient.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/Recipe.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/Recipe.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/RecipeResponse.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/RecipeResponse.cs)

### Predictive State Updates
- `DocumentState`: `document` markdown content for streamed state updates.

Reference: [dotnet/samples/AGUIClientServer/AGUIDojoServer/PredictiveStateUpdates/DocumentState.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/PredictiveStateUpdates/DocumentState.cs)

### Serializer Context
`AGUIDojoServerSerializerContext` registers all model types for source-generated JSON serialization.

Reference: [dotnet/samples/AGUIClientServer/AGUIDojoServer/AGUIDojoServerSerializerContext.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AGUIDojoServerSerializerContext.cs)

## Runtime Flow Highlights
### Common Setup
- Uses `AzureOpenAIClient` and Azure AD credentials to create `ChatClient` instances.
- Requires environment variables `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_DEPLOYMENT_NAME`.
- Converts `ChatClient` to `ChatClientAgent` via `AsIChatClient().AsAIAgent(...)`.

Reference: [dotnet/samples/AGUIClientServer/AGUIDojoServer/ChatClientAgentFactory.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/ChatClientAgentFactory.cs)

### Agentic UI (`/agentic_generative_ui`)
- Uses tools `create_plan` and `update_plan_step`.
- `create_plan` returns a `Plan` with `StepStatus.Pending`.
- `update_plan_step` returns JSON Patch operations (with lowercase `status` values to match AG-UI expectations).
- `AgenticUIAgent` intercepts tool results and emits state events:
  - `application/json` for plan snapshots.
  - `application/json-patch+json` for incremental updates.

References:
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/AgenticPlanningTools.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/AgenticPlanningTools.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/AgenticUIAgent.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/AgenticUIAgent.cs)

### Shared State (`/shared_state`)
- Reads incoming AG-UI state from run options: `AdditionalProperties["ag_ui_state"]`.
- Runs the agent with JSON schema response format for `RecipeResponse` to produce a state snapshot.
- Emits a `DataContent` state snapshot as `application/json`, then requests a concise textual summary.

Reference: [dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/SharedStateAgent.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/SharedStateAgent.cs)

### Predictive State Updates (`/predictive_state_updates`)
- Watches for `write_document` tool calls.
- Extracts `document` content from tool call arguments and emits progressive `DocumentState` snapshots in chunks.
- Emits additional `DataContent` updates with `application/json` to simulate streaming document edits.

Reference: [dotnet/samples/AGUIClientServer/AGUIDojoServer/PredictiveStateUpdates/PredictiveStateUpdatesAgent.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/PredictiveStateUpdates/PredictiveStateUpdatesAgent.cs)
