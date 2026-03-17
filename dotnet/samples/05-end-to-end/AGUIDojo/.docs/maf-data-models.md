# MAF (Microsoft Agent Framework) data models and API surface (dotnet)

## Data models

### `AIAgent`
`AIAgent` is the core abstraction that defines agent identity (`Id`, `Name`, `Description`), service discovery (`GetService`), thread lifecycle (`GetNewThreadAsync`, `DeserializeThreadAsync`), and the primary execution APIs (`RunAsync` and `RunStreamingAsync` overloads). It is the base contract for all agent implementations.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgent.cs#L14-L380

### `AgentThread`
`AgentThread` models conversation state and behavior boundaries for a specific agent session. It provides `Serialize` for persistence and a `GetService` hook for retrieving thread-scoped services like history providers.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentThread.cs#L9-L92

### `AgentRunOptions`
`AgentRunOptions` captures invocation-level options. It supports `ContinuationToken` for resuming background or streaming runs, `AllowBackgroundResponses`, and `AdditionalProperties` for provider-specific metadata.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentRunOptions.cs#L9-L93

### `AgentResponse`
`AgentResponse` represents a completed run. It carries `Messages` (list of `ChatMessage`), `Text` convenience access, `AgentId`, `ResponseId`, `ContinuationToken`, `CreatedAt`, `Usage`, `RawRepresentation`, and `AdditionalProperties`. It can also be converted back to streaming updates via `ToAgentResponseUpdates()`.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponse.cs#L22-L260

### `AgentResponseUpdate`
`AgentResponseUpdate` is the streaming counterpart to `AgentResponse`. It includes incremental `Contents` (list of `AIContent`), author metadata (`Role`, `AuthorName`), identifiers (`AgentId`, `ResponseId`, `MessageId`), timestamps, and `ContinuationToken` for stream resumption.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponseUpdate.cs#L14-L176

### `AgentResponse<T>`
`AgentResponse<T>` is a typed response wrapper that derives from `AgentResponse` and exposes a strongly-typed `Result` property for structured output.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponse%7BT%7D.cs#L7-L29

### `AIAgentMetadata`
`AIAgentMetadata` holds provider-level metadata for an agent, such as `ProviderName`, aligned with OpenTelemetry semantic conventions when available.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgentMetadata.cs#L7-L39

## API surface (dotnet)

### Agent execution APIs
- `AIAgent.RunAsync(...)` overloads support no input, string input, single `ChatMessage`, or a collection of `ChatMessage`, all returning `AgentResponse`.
- `AIAgent.RunStreamingAsync(...)` overloads mirror `RunAsync` but return `IAsyncEnumerable<AgentResponseUpdate>` for incremental streaming.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgent.cs#L144-L380

### Agent pipeline composition
`AIAgentBuilder` lets you create pipeline agents by wrapping an inner agent. It exposes `Use(...)` overloads to attach middleware-like agents or custom delegates for `RunAsync` and `RunStreamingAsync`, and `Build(...)` to produce the composed agent.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI/AIAgentBuilder.cs#L13-L172

### Agent utility extensions
`AIAgentExtensions` provides:
- `AsBuilder()` to start a pipeline from an existing agent.
- `AsAIFunction()` to wrap an agent as an invocable `AIFunction` tool, with optional `AgentThread` and metadata.

Source: https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI/AgentExtensions.cs#L13-L114

## Notes on dependent exchange types
MAF uses Microsoft.Extensions.AI exchange types (`ChatMessage`, `ChatRole`, `AIContent`, `ChatResponse`, `ChatResponseUpdate`, `UsageDetails`, `AdditionalPropertiesDictionary`) as the message and content model across the abstractions above.

Sources:
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgent.cs#L3-L10
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponse.cs#L7-L19
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponseUpdate.cs#L3-L10

## References
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgent.cs#L14-L380
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentThread.cs#L9-L92
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentRunOptions.cs#L9-L93
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponse.cs#L22-L260
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponseUpdate.cs#L14-L176
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AgentResponse%7BT%7D.cs#L7-L29
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Abstractions/AIAgentMetadata.cs#L7-L39
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI/AIAgentBuilder.cs#L13-L172
- https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI/AgentExtensions.cs#L13-L114
