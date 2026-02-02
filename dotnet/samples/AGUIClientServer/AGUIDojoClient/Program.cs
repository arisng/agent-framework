// Copyright (c) Microsoft. All rights reserved.

// This is the AGUIDojoClient - a Blazor Server web chat application that demonstrates
// all 7 AG-UI protocol features by connecting to the AGUIDojoServer.
// It supports multiple endpoints: /agentic_chat, /backend_tool_rendering, /human_in_the_loop,
// /agentic_generative_ui, /tool_based_generative_ui, /shared_state, and /predictive_state_updates.

using AGUIDojoClient.Components;
using AGUIDojoClient.Services;
using Microsoft.Agents.AI.AGUI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Get server URL from configuration (default to AGUIDojoServer at localhost:5100)
string serverUrl = builder.Configuration["SERVER_URL"] ?? "http://localhost:5100";

// Register HttpClient for AGUIDojoServer
builder.Services.AddHttpClient("aguiserver", httpClient => httpClient.BaseAddress = new Uri(serverUrl));

// Register AGUIChatClientFactory for dynamic endpoint selection
// The factory allows creating IChatClient instances for any of the 7 AG-UI endpoints
builder.Services.AddSingleton<IAGUIChatClientFactory, AGUIChatClientFactory>();

// Register ApprovalHandler for Human-in-the-Loop feature
// This service handles approval requests and responses for tool calls requiring user consent
builder.Services.AddScoped<IApprovalHandler, ApprovalHandler>();

// Register JsonPatchApplier for Agentic Generative UI feature
// This service applies JSON Patch operations to Plan models for incremental state updates
builder.Services.AddSingleton<IJsonPatchApplier, JsonPatchApplier>();

// Register ToolComponentRegistry for Tool-Based UI Rendering feature
// This service maps tool names to Blazor component types for dynamic rendering
builder.Services.AddSingleton<IToolComponentRegistry, ToolComponentRegistry>();

// Register StateManager for Shared State feature
// This service manages bidirectional state sync with the server for Recipe data
builder.Services.AddScoped<IStateManager, StateManager>();

// Register SseEventParser for Agentic Generative UI feature
// This service parses raw SSE events to handle STATE_DELTA events that the AGUIChatClient library doesn't recognize
builder.Services.AddSingleton<ISseEventParser, SseEventParser>();

// Register a default AGUIChatClient for backward compatibility
// Components can also use IAGUIChatClientFactory to create clients for specific endpoints
builder.Services.AddChatClient(sp => new AGUIChatClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver"), "agentic_chat"));

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
