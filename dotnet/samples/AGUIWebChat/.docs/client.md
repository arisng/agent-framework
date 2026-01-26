# Client Host & Service Modules (AGUIWebChat)

## Overview
The client project is a Blazor Server application that renders the chat UI and connects to the AG-UI server via `IHttpClientFactory`. It also renders Agentic UI state updates emitted as `DataContent` payloads. The modules below capture startup, service registration, and environment configuration.

## Module Details

### Client host bootstrap
- **Responsibility:** Configure Razor components, middleware, and render mode for the Blazor Server app.
- **Inputs:** Environment, configuration.
- **Outputs:** Running Blazor Server host.
- **Dependencies:** Blazor hosting, ASP.NET Core middleware.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Program.cs](dotnet/samples/AGUIWebChat/Client/Program.cs)

### Static asset mapping
- **Responsibility:** Enable static asset delivery for the app shell and scoped CSS.
- **Inputs:** Static files under wwwroot and component CSS.
- **Outputs:** Static asset endpoints.
- **Dependencies:** ASP.NET Core static files / static assets pipeline.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Program.cs](dotnet/samples/AGUIWebChat/Client/Program.cs)

### AG-UI client registration
- **Responsibility:** Register a named `HttpClient` for the server and construct `AGUIChatClient` via `IHttpClientFactory`.
- **Inputs:** `SERVER_URL` configuration value.
- **Outputs:** `IChatClient` service for UI components.
- **Dependencies:** `IHttpClientFactory`, Microsoft.Agents.AI.AGUI client.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Program.cs](dotnet/samples/AGUIWebChat/Client/Program.cs)

### App shell and routing
- **Responsibility:** Define the HTML shell, global styles, and root router.
- **Inputs:** Component tree and static assets.
- **Outputs:** Rendered UI shell.
- **Dependencies:** Blazor components and routing.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/App.razor](dotnet/samples/AGUIWebChat/Client/Components/App.razor), [dotnet/samples/AGUIWebChat/Client/Components/Routes.razor](dotnet/samples/AGUIWebChat/Client/Components/Routes.razor)

### Local launch configuration
- **Responsibility:** Configure local URLs and environment variables, including `SERVER_URL`.
- **Inputs:** launch settings.
- **Outputs:** Host URL and config defaults.
- **Dependencies:** ASP.NET Core launch settings.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Properties/launchSettings.json](dotnet/samples/AGUIWebChat/Client/Properties/launchSettings.json)

## References
- Blazor hosting overview: https://learn.microsoft.com/aspnet/core/blazor/?view=aspnetcore-8.0
- Blazor render modes: https://learn.microsoft.com/aspnet/core/blazor/components/render-modes?view=aspnetcore-8.0
- `IHttpClientFactory` guidance: https://learn.microsoft.com/aspnet/core/fundamentals/http-requests?view=aspnetcore-8.0
- Dependency injection in ASP.NET Core: https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0
- Static files in ASP.NET Core: https://learn.microsoft.com/aspnet/core/fundamentals/static-files?view=aspnetcore-8.0
