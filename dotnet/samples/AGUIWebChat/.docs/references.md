# AGUIWebChat Authoritative References

This document lists authoritative sources that ground the AGUIWebChat module documentation. The references focus on ASP.NET Core hosting, Blazor components and JS interop, HTTP client patterns, and Azure OpenAI usage in .NET.

## ASP.NET Core Hosting & Configuration
- ASP.NET Core hosting model (generic host): https://learn.microsoft.com/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-8.0
- Minimal APIs and endpoint mapping: https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-8.0
- Dependency injection fundamentals: https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0
- Static file middleware: https://learn.microsoft.com/aspnet/core/fundamentals/static-files?view=aspnetcore-8.0
- Launch settings (local dev profiles): https://learn.microsoft.com/aspnet/core/fundamentals/environments?view=aspnetcore-8.0#launch-settings

## Blazor Components & Routing
- Blazor overview and hosting models: https://learn.microsoft.com/aspnet/core/blazor/?view=aspnetcore-8.0
- Blazor components and component lifecycle: https://learn.microsoft.com/aspnet/core/blazor/components/?view=aspnetcore-8.0
- Blazor routing and navigation: https://learn.microsoft.com/aspnet/core/blazor/fundamentals/routing?view=aspnetcore-8.0

## Blazor JavaScript Interop
- JavaScript interop (JSRuntime, JSImport/JSInvokable): https://learn.microsoft.com/aspnet/core/blazor/javascript-interoperability?view=aspnetcore-8.0

## HTTP Clients & Service Registration
- HTTP requests and `IHttpClientFactory`: https://learn.microsoft.com/aspnet/core/fundamentals/http-requests?view=aspnetcore-8.0
- `HttpClient` usage and lifetime guidance: https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines

## Azure OpenAI & Azure Identity
- Azure OpenAI service overview: https://learn.microsoft.com/azure/ai-services/openai/overview
- Azure OpenAI .NET quickstart: https://learn.microsoft.com/azure/ai-services/openai/quickstart?tabs=command-line&pivots=programming-language-csharp
- Azure Identity client library overview (DefaultAzureCredential): https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential
