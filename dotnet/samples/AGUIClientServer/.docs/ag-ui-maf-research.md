# AG-UI and MAF (dotnet) consolidated research summary

## Overview
AG-UI provides a transport and event protocol (HTTP POST + Server-Sent Events) for running agents with structured messages, tool calls, and state deltas. MAF (Microsoft Agent Framework) defines the core agent abstractions, execution APIs, and response models that AG-UI adapts for streaming and interoperability in dotnet.

## AG-UI (dotnet)

### Data models
| Area         | Key types                                      | Purpose                                                                                                                  |
| ------------ | ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Run envelope | `RunAgentInput`                                | Captures the run request payload with thread/run identifiers, state, messages, tools, context, and forwarded properties. |
| Messages     | `AGUIMessage` + role-specific derived types    | Represents user/assistant/system/developer/tool messages with role-specific fields and optional tool calls.              |
| Tooling      | `AGUITool`, `AGUIToolCall`, `AGUIFunctionCall` | Describes tool metadata, calls, and serialized arguments.                                                                |
| Context      | `AGUIContextItem`                              | Name/value context items with descriptions.                                                                              |
| Events       | `BaseEvent` + run/text/tool/state events       | Defines the SSE event model for run lifecycle, streaming text, tool calls, and state snapshot/delta.                     |

### API surface
- Client-side: `AGUIChatClient` adapts `ChatMessage` input to `RunAgentInput`, posts via `AGUIHttpService`, and converts AG-UI events to `ChatResponseUpdate`.
- Server-side: `AGUIEndpointRouteBuilderExtensions.MapAGUI` exposes a POST route that accepts `RunAgentInput` and streams `BaseEvent` instances using `AGUIServerSentEventsResult`.
- Conversion utilities: `AGUIChatMessageExtensions`, `AIToolExtensions`, and `ChatResponseUpdateAGUIExtensions` handle mapping between AG-UI structures and Microsoft.Extensions.AI exchange types.

## MAF (Microsoft Agent Framework, dotnet)

### Data models
| Area        | Key types                                                  | Purpose                                                                               |
| ----------- | ---------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| Agent core  | `AIAgent`, `AgentThread`                                   | Defines agent identity, thread lifecycle, service discovery, and execution contracts. |
| Run options | `AgentRunOptions`                                          | Run-time options like continuation tokens and background responses.                   |
| Responses   | `AgentResponse`, `AgentResponseUpdate`, `AgentResponse<T>` | Completed and streaming response models with message/content, metadata, and usage.    |
| Metadata    | `AIAgentMetadata`                                          | Provider-level metadata for telemetry and reporting.                                  |

### API surface
- Execution: `AIAgent.RunAsync(...)` and `AIAgent.RunStreamingAsync(...)` overloads support string and message inputs for completed or streaming responses.
- Composition: `AIAgentBuilder` and `AIAgentExtensions` enable pipeline composition and wrapping agents as tools (`AsAIFunction`).
- Exchange types: MAF relies on Microsoft.Extensions.AI types such as `ChatMessage`, `ChatResponseUpdate`, `AIContent`, and `UsageDetails` for interchange and streaming.

## Relationship between AG-UI and MAF (dotnet)
AG-UI serves as an HTTP/SSE transport for MAF agent execution. On the server, `MapAGUI` translates a run request into `AIAgent` execution and streams `BaseEvent` payloads. On the client, `AGUIChatClient` converts MAF chat inputs into AG-UI run requests and converts AG-UI event streams back into `ChatResponseUpdate` via `ChatResponseUpdateAGUIExtensions`.

## AGUIDojoServer (dotnet) findings

### AG-UI endpoints and routing
The AGUIDojoServer sample configures multiple AG-UI endpoints via `MapAGUI`, each hosting a `RunAgentInput` POST route that streams AG-UI events over SSE.
Routes include `/agentic_chat`, `/backend_tool_rendering`, `/human_in_the_loop`, `/tool_based_generative_ui`, `/agentic_generative_ui`, `/shared_state`, and `/predictive_state_updates`.

### Data models used by the sample
| Area                | Key types                                          | Purpose                                                |
| ------------------- | -------------------------------------------------- | ------------------------------------------------------ |
| Backend tools       | `WeatherInfo`                                      | `get_weather` tool response payload.                   |
| Agentic UI planning | `Plan`, `Step`, `StepStatus`, `JsonPatchOperation` | Plan snapshots and JSON Patch deltas for plan updates. |
| Shared state        | `Ingredient`, `Recipe`, `RecipeResponse`           | Structured recipe state for shared state snapshots.    |
| Predictive updates  | `DocumentState`                                    | Markdown document content streamed as state snapshots. |
| Serialization       | `AGUIDojoServerSerializerContext`                  | Source-generated JSON serialization for sample models. |

### Runtime flow highlights
- Uses `AzureOpenAIClient` + Azure AD credentials to build `ChatClient` instances and create `ChatClientAgent` via `AsIChatClient().AsAIAgent(...)`.
- `/agentic_generative_ui` emits plan snapshots (`application/json`) and JSON Patch deltas (`application/json-patch+json`) by intercepting tool results.
- `/shared_state` reads `ag_ui_state` from `AgentRunOptions.AdditionalProperties`, emits a JSON snapshot, then requests a concise text summary.
- `/predictive_state_updates` emits chunked `DocumentState` snapshots while tools generate document content.

## References

### AG-UI (dotnet)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/RunAgentInput.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/RunAgentInput.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIMessage.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIMessage.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIUserMessage.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIUserMessage.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIAssistantMessage.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIAssistantMessage.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUISystemMessage.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUISystemMessage.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIToolMessage.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIToolMessage.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUITool.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUITool.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIToolCall.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIToolCall.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIFunctionCall.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIFunctionCall.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIContextItem.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/AGUIContextItem.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/BaseEvent.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/BaseEvent.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/AGUIChatClient.cs](dotnet/src/Microsoft.Agents.AI.AGUI/AGUIChatClient.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/AGUIHttpService.cs](dotnet/src/Microsoft.Agents.AI.AGUI/AGUIHttpService.cs)
- [dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs](dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIEndpointRouteBuilderExtensions.cs)
- [dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIServerSentEventsResult.cs](dotnet/src/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/AGUIServerSentEventsResult.cs)
- [dotnet/src/Microsoft.Agents.AI.AGUI/Shared/ChatResponseUpdateAGUIExtensions.cs](dotnet/src/Microsoft.Agents.AI.AGUI/Shared/ChatResponseUpdateAGUIExtensions.cs)

### MAF (dotnet)
- [dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgent.cs](dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgent.cs)
- [dotnet/src/Microsoft.Agents.AI.Abstractions/AgentThread.cs](dotnet/src/Microsoft.Agents.AI.Abstractions/AgentThread.cs)
- [dotnet/src/Microsoft.Agents.AI.Abstractions/AgentRunOptions.cs](dotnet/src/Microsoft.Agents.AI.Abstractions/AgentRunOptions.cs)
- [dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponse.cs](dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponse.cs)
- [dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponseUpdate.cs](dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponseUpdate.cs)
- [dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponse{T}.cs](dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponse%7BT%7D.cs)
- [dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgentMetadata.cs](dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgentMetadata.cs)
- [dotnet/src/Microsoft.Agents.AI/AIAgentBuilder.cs](dotnet/src/Microsoft.Agents.AI/AIAgentBuilder.cs)
- [dotnet/src/Microsoft.Agents.AI/AgentExtensions.cs](dotnet/src/Microsoft.Agents.AI/AgentExtensions.cs)

### AGUIDojoServer (dotnet)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/Program.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/Program.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/ChatClientAgentFactory.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/ChatClientAgentFactory.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AGUIDojoServerSerializerContext.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AGUIDojoServerSerializerContext.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/BackendToolRendering/WeatherInfo.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/BackendToolRendering/WeatherInfo.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/Plan.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/Plan.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/Step.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/Step.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/StepStatus.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/StepStatus.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/JsonPatchOperation.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/JsonPatchOperation.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/AgenticPlanningTools.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/AgenticPlanningTools.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/AgenticUIAgent.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/AgenticUI/AgenticUIAgent.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/Ingredient.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/Ingredient.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/Recipe.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/Recipe.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/RecipeResponse.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/RecipeResponse.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/SharedStateAgent.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/SharedState/SharedStateAgent.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/PredictiveStateUpdates/DocumentState.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/PredictiveStateUpdates/DocumentState.cs)
- [dotnet/samples/AGUIClientServer/AGUIDojoServer/PredictiveStateUpdates/PredictiveStateUpdatesAgent.cs](dotnet/samples/AGUIClientServer/AGUIDojoServer/PredictiveStateUpdates/PredictiveStateUpdatesAgent.cs)
