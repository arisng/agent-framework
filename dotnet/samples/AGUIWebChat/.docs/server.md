# Server Modules (AGUIWebChat)

## Overview
The server project hosts an ASP.NET Core app that exposes an AG-UI endpoint backed by Azure OpenAI and emits Agentic UI state updates. The modules below cover configuration, service registration, agent creation, Agentic UI tooling, and endpoint mapping.

## Module Details

### Server host bootstrap
- **Responsibility:** Build the ASP.NET Core host, register services, and run the request pipeline.
- **Inputs:** Command-line args, environment/configuration.
- **Outputs:** Running web host configured for AG-UI.
- **Dependencies:** ASP.NET Core minimal hosting, dependency injection.
- **Primary files:** [dotnet/samples/AGUIWebChat/Server/Program.cs](dotnet/samples/AGUIWebChat/Server/Program.cs)

### AG-UI service registration
- **Responsibility:** Register AG-UI services required to map the protocol endpoint.
- **Inputs:** Service collection.
- **Outputs:** AG-UI service registrations.
- **Dependencies:** AG-UI hosting integration.
- **Primary files:** [dotnet/samples/AGUIWebChat/Server/Program.cs](dotnet/samples/AGUIWebChat/Server/Program.cs)

### Azure OpenAI client setup
- **Responsibility:** Read `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_DEPLOYMENT_NAME`, then construct `AzureOpenAIClient` and `ChatClient`.
- **Inputs:** Environment variables, Azure credentials.
- **Outputs:** `ChatClient` instance used by the agent.
- **Dependencies:** Azure OpenAI SDK, `DefaultAzureCredential`.
- **Primary files:** [dotnet/samples/AGUIWebChat/Server/Program.cs](dotnet/samples/AGUIWebChat/Server/Program.cs)

### Agent creation
- **Responsibility:** Build a `ChatClientAgent` with instructions, tools, and identity.
- **Inputs:** `ChatClient`, instruction text, tool definitions.
- **Outputs:** Configured `ChatClientAgent`.
- **Dependencies:** Microsoft.Agents.AI abstractions.
- **Primary files:** [dotnet/samples/AGUIWebChat/Server/Program.cs](dotnet/samples/AGUIWebChat/Server/Program.cs)

### Agentic UI wrapper
- **Responsibility:** Wrap the base agent to emit Agentic UI state events from plan tool results.
- **Inputs:** `ChatClientAgent`, JSON serializer options.
- **Outputs:** `AIAgent` that emits `DataContent` snapshots and JSON Patch deltas.
- **Dependencies:** Delegating agent pattern, JSON serialization.
- **Primary files:** [dotnet/samples/AGUIWebChat/Server/AgenticUI/AgenticUIAgent.cs](dotnet/samples/AGUIWebChat/Server/AgenticUI/AgenticUIAgent.cs)

### Agentic UI tools and models
- **Responsibility:** Provide plan creation and update tools that drive state updates.
- **Inputs:** Tool arguments (`steps`, `index`, `description`, `status`).
- **Outputs:** Plan snapshots (`application/json`) and JSON Patch deltas (`application/json-patch+json`).
- **Dependencies:** Tool binding, JSON serialization.
- **Primary files:** [dotnet/samples/AGUIWebChat/Server/AgenticUI/AgenticPlanningTools.cs](dotnet/samples/AGUIWebChat/Server/AgenticUI/AgenticPlanningTools.cs), [dotnet/samples/AGUIWebChat/Server/AgenticUI/Plan.cs](dotnet/samples/AGUIWebChat/Server/AgenticUI/Plan.cs), [dotnet/samples/AGUIWebChat/Server/AgenticUI/JsonPatchOperation.cs](dotnet/samples/AGUIWebChat/Server/AgenticUI/JsonPatchOperation.cs)

### AG-UI endpoint mapping
- **Responsibility:** Map `/ag-ui` to the wrapped agent using ASP.NET Core endpoint routing.
- **Inputs:** `AIAgent` (Agentic UI wrapper).
- **Outputs:** HTTP endpoint that serves the AG-UI protocol and state events.
- **Dependencies:** Minimal API endpoint mapping.
- **Primary files:** [dotnet/samples/AGUIWebChat/Server/Program.cs](dotnet/samples/AGUIWebChat/Server/Program.cs)

### Local launch configuration
- **Responsibility:** Configure local URLs and environment for development.
- **Inputs:** launch settings.
- **Outputs:** Host URL and environment variables.
- **Dependencies:** ASP.NET Core launch settings.
- **Primary files:** [dotnet/samples/AGUIWebChat/Server/Properties/launchSettings.json](dotnet/samples/AGUIWebChat/Server/Properties/launchSettings.json)

## References
- ASP.NET Core hosting model: https://learn.microsoft.com/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-8.0
- Minimal APIs and endpoint mapping: https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-8.0
- Dependency injection in ASP.NET Core: https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0
- Azure OpenAI overview: https://learn.microsoft.com/azure/ai-services/openai/overview
- Azure OpenAI .NET quickstart: https://learn.microsoft.com/azure/ai-services/openai/quickstart?tabs=command-line&pivots=programming-language-csharp
- `DefaultAzureCredential`: https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential
