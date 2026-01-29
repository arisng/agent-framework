// Copyright (c) Microsoft. All rights reserved.

using AGUIWebChatClient.Components;
using AGUIWebChatClient.Components.Pages.Chat;
using AGUIWebChat.Client.Services;
using Microsoft.Agents.AI.AGUI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

string serverUrl = builder.Configuration["SERVER_URL"] ?? "http://localhost:5100";

builder.Services.AddHttpClient("aguiserver", httpClient => httpClient.BaseAddress = new Uri(serverUrl));

builder.Services.AddChatClient(sp => new AGUIChatClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("aguiserver"), "ag-ui"));

builder.Services.AddScoped<AGUIProtocolService>();
builder.Services.AddScoped<AGUIWebChat.Client.ViewModels.ChatViewModel>();
builder.Services.AddSingleton<IComponentRegistry>(sp =>
{
    ComponentRegistry componentRegistry = new();
    componentRegistry.Register("application/vnd.microsoft.agui.plan+json", typeof(PlanComponent));
    componentRegistry.Register(ToolContentMediaTypes.ToolCall, typeof(ToolCallComponent));
    componentRegistry.Register(ToolContentMediaTypes.ToolResult, typeof(ToolResultComponent));
    componentRegistry.Register(ToolContentMediaTypes.WeatherCall, typeof(WeatherToolComponent));
    componentRegistry.Register(ToolContentMediaTypes.WeatherResult, typeof(WeatherToolComponent));
    return componentRegistry;
});

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
