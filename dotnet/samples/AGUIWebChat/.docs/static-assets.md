# Static Assets & Styling Modules (AGUIWebChat)

## Overview
Static assets and styles are delivered through the ASP.NET Core static asset pipeline and Blazor scoped CSS. This module group covers global styles, component-scoped styles, and static images.

## Module Details

### Global styles
- **Responsibility:** Provide base typography, layout, shared button styles, and global theming.
- **Inputs:** Global CSS definitions.
- **Outputs:** App-wide styling.
- **Dependencies:** ASP.NET Core static files, Blazor static assets.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/wwwroot/app.css](dotnet/samples/AGUIWebChat/Client/wwwroot/app.css)

### Component-scoped styles
- **Responsibility:** Apply scoped styling for chat layout, message bubbles, headers, and spinner animations.
- **Inputs:** Component CSS files.
- **Outputs:** Scoped component styling.
- **Dependencies:** Blazor scoped CSS pipeline.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/Chat.razor.css](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/Chat.razor.css), [dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageItem.razor.css](dotnet/samples/AGUIWebChat/Client/Components/Pages/Chat/ChatMessageItem.razor.css), [dotnet/samples/AGUIWebChat/Client/Components/Layout/LoadingSpinner.razor.css](dotnet/samples/AGUIWebChat/Client/Components/Layout/LoadingSpinner.razor.css)

### Static images
- **Responsibility:** Provide icons and browser tab imagery.
- **Inputs:** Image assets.
- **Outputs:** Static responses for image URLs.
- **Dependencies:** ASP.NET Core static file middleware.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/wwwroot/favicon.png](dotnet/samples/AGUIWebChat/Client/wwwroot/favicon.png)

### Static asset mapping
- **Responsibility:** Map static assets into the request pipeline for the Blazor host.
- **Inputs:** Static file content and component CSS outputs.
- **Outputs:** URL-addressable static assets.
- **Dependencies:** ASP.NET Core static files and Blazor static assets.
- **Primary files:** [dotnet/samples/AGUIWebChat/Client/Program.cs](dotnet/samples/AGUIWebChat/Client/Program.cs)

## References
- Static files in ASP.NET Core: https://learn.microsoft.com/aspnet/core/fundamentals/static-files?view=aspnetcore-8.0
- Blazor scoped CSS: https://learn.microsoft.com/aspnet/core/blazor/components/css-isolation?view=aspnetcore-8.0
- ASP.NET Core static assets for Blazor: https://learn.microsoft.com/aspnet/core/blazor/components/?view=aspnetcore-8.0#static-assets
