// Copyright (c) Microsoft. All rights reserved.

// This sample demonstrates a basic AG-UI server hosting a chat agent for the Blazor web client.

using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Services;
using AGUIWebChatServer.AgenticUI;
using AGUIWebChatServer.Tools;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient().AddLogging();

// Add controllers for REST API endpoints
builder.Services.AddControllers();

// Register QuizDbContext with SQLite
string? connectionString = builder.Configuration.GetConnectionString("QuizDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("QuizDatabase connection string is not configured.");
}

builder.Services.AddDbContext<QuizDbContext>(options =>
    options.UseSqlite(connectionString));

// Register MockQuizService
builder.Services.AddScoped<IMockQuizService, MockQuizService>();

// Register QuizEvaluationService
builder.Services.AddScoped<IQuizEvaluationService, QuizEvaluationService>();

// Register QuizAnalyticsService
builder.Services.AddScoped<IQuizAnalyticsService, QuizAnalyticsService>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Add(AgenticUISerializerContext.Default));
builder.Services.AddAGUI();

// Register JsonSerializerOptions for DI (after ConfigureHttpJsonOptions)
builder.Services.AddSingleton(serviceProvider =>
{
    Microsoft.AspNetCore.Http.Json.JsonOptions jsonOptions = serviceProvider
        .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value;
    return jsonOptions.SerializerOptions;
});

// Register QuizTool as transient since it will be resolved within a scope
builder.Services.AddTransient<QuizTool>();

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

// Register QuizGeneratorTool in DI container
ILogger<QuizGeneratorTool> quizToolLogger = app.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger<QuizGeneratorTool>();
IQuizGeneratorTool quizGeneratorTool = new QuizGeneratorTool(chatClient, jsonOptions.SerializerOptions, quizToolLogger);

// Create factory functions for QuizTool that resolve scoped dependencies per invocation
IServiceProvider serviceProvider = app.Services;

async Task<string> ListQuizzesFactoryAsync(CancellationToken cancellationToken)
{
    using IServiceScope scope = serviceProvider.CreateScope();
    QuizTool quizTool = scope.ServiceProvider.GetRequiredService<QuizTool>();
    return await quizTool.ListQuizzesAsync(cancellationToken);
}

async Task<string> GetQuizFactoryAsync(QuizRequest request, CancellationToken cancellationToken)
{
    using IServiceScope scope = serviceProvider.CreateScope();
    QuizTool quizTool = scope.ServiceProvider.GetRequiredService<QuizTool>();
    return await quizTool.GetQuizAsync(request, cancellationToken);
}

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
        serializerOptions: jsonOptions.SerializerOptions),
    AIFunctionFactory.Create(
        quizGeneratorTool.GenerateQuizAsync,
        name: "generate_quiz",
        description: """
            Generate an interactive quiz on a specified topic with configurable difficulty and question types.
            Returns a JSON string containing the complete quiz structure with questions, answer options, and correct answers.
            """,
        serializerOptions: jsonOptions.SerializerOptions),
    AIFunctionFactory.Create(
        ListQuizzesFactoryAsync,
        name: "list_quizzes",
        description: "List all available quizzes with summary information. Use when user asks to 'list quizzes' or 'show available quizzes'.",
        serializerOptions: jsonOptions.SerializerOptions),
    AIFunctionFactory.Create(
        GetQuizFactoryAsync,
        name: "get_quiz",
        description: """
            Retrieve a quiz by topic or ID from the mock quiz database.
            Use when user asks to 'show me a quiz', 'get quiz about [topic]', or 'show quiz [id]'.
            Returns quiz JSON with media type application/vnd.quiz+json.
            """,
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

            When the user requests a quiz or test on a topic:

            FOR GENERATING NEW QUIZZES:
            - Use the `generate_quiz` tool with appropriate parameters
            - Topic: Clear and specific subject matter (e.g., "Python Programming Basics")
            - Difficulty: "easy", "medium", or "hard" based on context or user preference
            - NumberOfQuestions: Default to 5 unless user specifies otherwise (range: 1-20)
            - QuestionTypes: Use ["mixed"] for variety, or ["single-select"] or ["multi-select"] if user specifies
            - The tool returns quiz JSON; present it directly to the user without additional commentary
            - Do NOT attempt to create or modify quiz JSON manually

            FOR RETRIEVING EXISTING QUIZZES:
            - Use `list_quizzes` tool when user asks to "list quizzes" or "show available quizzes"
            - Use `get_quiz` tool to retrieve a quiz by topic or ID:
              * Specify topic (e.g., {"topic": "programming"}) to search by topic
              * Specify quizId (e.g., {"quizId": "quiz-123"}) to get a specific quiz
              * Leave both empty ({}) to get a random quiz
            - The tool returns quiz JSON from the mock database; present it directly
            """,
        Tools = tools,
        AllowMultipleToolCalls = false
    }
});

AIAgent agent = new AgenticUIAgent(baseAgent, jsonOptions.SerializerOptions);

// Map controllers for REST API endpoints
app.MapControllers();

// Map the AG-UI agent endpoint
app.MapAGUI("/ag-ui", agent);

// Apply database migrations and seed data on startup
using (IServiceScope scope = app.Services.CreateScope())
{
    QuizDbContext dbContext = scope.ServiceProvider.GetRequiredService<QuizDbContext>();
    await dbContext.Database.MigrateAsync();

    // Seed mock quiz data if database is empty
    await QuizDataSeeder.SeedAsync(dbContext);
}

await app.RunAsync();
