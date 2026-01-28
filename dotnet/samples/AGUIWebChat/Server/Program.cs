// Copyright (c) Microsoft. All rights reserved.

// This sample demonstrates a basic AG-UI server hosting a chat agent for the Blazor web client.

using AGUIWebChatServer.AgenticUI;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(AgenticUISerializerContext.Default));
builder.Services.AddAGUI();

WebApplication app = builder.Build();

string? endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"];
string? deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"];
string? openAiApiKey = builder.Configuration["OPENAI_API_KEY"];
string? openAiModel = builder.Configuration["OPENAI_MODEL"] ?? "gpt-5-mini";

ChatClient chatClient;
if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(deploymentName))
{
    // Create the Azure OpenAI client using DefaultAzureCredential.
    AzureOpenAIClient azureOpenAIClient = new(
        new Uri(endpoint),
        new DefaultAzureCredential());

    chatClient = azureOpenAIClient.GetChatClient(deploymentName);
}
else if (!string.IsNullOrWhiteSpace(openAiApiKey) && !string.IsNullOrWhiteSpace(openAiModel))
{
    // Create the OpenAI client using an API key.
    OpenAIClient openAIClient = new(openAiApiKey);
    chatClient = openAIClient.GetChatClient(openAiModel);
}
else
{
    throw new InvalidOperationException(
        "Either AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT_NAME, or OPENAI_API_KEY and OPENAI_MODEL must be set.");
}

Microsoft.AspNetCore.Http.Json.JsonOptions jsonOptions = app.Services
    .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value;

AITool[] tools =
[
    AIFunctionFactory.Create(
        AgenticPlanningTools.CreatePlan,
        name: "create_plan",
        description: "Create a plan with multiple steps.",
        serializerOptions: jsonOptions.SerializerOptions),
    AIFunctionFactory.Create(
        AgenticPlanningTools.UpdatePlanStepAsync,
        name: "update_plan_step",
        description: "Update a step in the plan with new description or status.",
        serializerOptions: jsonOptions.SerializerOptions),
    AIFunctionFactory.Create(
        WeatherTool.GetWeather,
        name: "get_weather",
        description: "Get the weather for a given location.",
        serializerOptions: jsonOptions.SerializerOptions)
];

ChatClientAgent baseAgent = chatClient.AsIChatClient().AsAIAgent(new ChatClientAgentOptions
{
    Name = "AgenticUIAssistant",
    Description = "An agent that demonstrates Agentic UI planning updates.",
    ChatOptions = new ChatOptions
    {
        Instructions = """
            When planning use tools only, without any other messages.
            IMPORTANT:
            - Use the `create_plan` tool to set the initial state of the steps
            - Use the `update_plan_step` tool to update the status of each step
            - Do NOT repeat the plan or summarise it in a message
            - Do NOT confirm the creation or updates in a message
            - Do NOT ask the user for additional information or next steps
            - Do NOT leave a plan hanging, always complete the plan via `update_plan_step` if one is ongoing.
            - Continue calling update_plan_step until all steps are marked as completed.

            Only one plan can be active at a time, so do not call the `create_plan` tool
            again until all the steps in current plan are completed.
            """,
        Tools = tools,
        AllowMultipleToolCalls = true
    }
});

AIAgent agent = new AgenticUIAgent(baseAgent, jsonOptions.SerializerOptions);

// Map the AG-UI agent endpoint
app.MapAGUI("/ag-ui", agent);

await app.RunAsync();
