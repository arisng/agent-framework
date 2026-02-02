# GitHub Copilot Instructions

This repository contains the `AGUIWebChat` sample project.
All relevant code for this sample resides under `dotnet/samples/AGUIWebChat/`.

- **Server-side**: [dotnet/samples/AGUIWebChat/Server/](dotnet/samples/AGUIWebChat/Server/) - ASP.NET Core AG-UI host.
- **Client-side**: [dotnet/samples/AGUIWebChat/Client/](dotnet/samples/AGUIWebChat/Client/) - Blazor Server web UI.

## AGUIWebChat Guidelines

- The top of all `*.cs` files should have a copyright notice: `// Copyright (c) Microsoft. All rights reserved.`
- Configuration settings should be read from environment variables (e.g., `AZURE_OPENAI_ENDPOINT`, `OPENAI_API_KEY`).
- Use the solution file [dotnet/samples/AGUIWebChat/AGUIWebChat.slnx](dotnet/samples/AGUIWebChat/AGUIWebChat.slnx) for this sample.
- **NEVER** build the root Solution file (`dotnet/agent-framework-dotnet.slnx`).
- When making changes, ensure the server and client remain compatible via the AG-UI protocol.
- Follow the structure and patterns established in [dotnet/samples/AGUIWebChat/Server/Program.cs](dotnet/samples/AGUIWebChat/Server/Program.cs) and [dotnet/samples/AGUIWebChat/Client/Program.cs](dotnet/samples/AGUIWebChat/Client/Program.cs).

### Development Workflow

1.  **Modify Server/Client**: Edit files within [dotnet/samples/AGUIWebChat/](dotnet/samples/AGUIWebChat/).
2.  **Build Sample Solution**: Run `dotnet build dotnet/samples/AGUIWebChat/AGUIWebChat.slnx`.
3.  **Run Server**: In a terminal, run `dotnet run --project dotnet/samples/AGUIWebChat/Server/Server.csproj`.
4.  **Run Client**: In a separate terminal, run `dotnet run --project dotnet/samples/AGUIWebChat/Client/Client.csproj`.
5.  **Format Code**: Run `dotnet format dotnet/samples/AGUIWebChat/AGUIWebChat.slnx`.

### Coding Standards

- Prefer defining variables using types rather than `var`.
- Use the `Async` suffix for all async methods returning `Task` or `ValueTask`.
- All private classes that are not subclassed should be `sealed`.
- Maintain the Agentic UI state handling (snapshots and JSON Patch deltas) logic within the server.
