// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUIDojoServer.AgenticUI;
using AGUIDojoServer.HumanInTheLoop;
using AGUIDojoServer.PredictiveStateUpdates;
using AGUIDojoServer.SharedState;
using AGUIDojoServer.Tools;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace AGUIDojoServer;

/// <summary>
/// Factory for creating AI agents with various capabilities for AG-UI protocol demonstrations.
/// </summary>
/// <remarks>
/// <para>
/// This factory is registered as a Singleton in the DI container. It creates <see cref="ChatClient"/>
/// during construction based on configuration (Azure OpenAI or OpenAI) and provides methods to
/// create pre-configured agents for different AG-UI scenarios.
/// </para>
/// <para>
/// Tools are injected via DI-compatible tool classes (<see cref="WeatherTool"/>, <see cref="EmailTool"/>,
/// <see cref="DocumentTool"/>) which use <see cref="IHttpContextAccessor"/> to resolve scoped services
/// at execution time (per research findings Q1.15-Q1.18).
/// </para>
/// <para>
/// All agents are wrapped with OpenTelemetry instrumentation via <see cref="OpenTelemetryAgentBuilderExtensions.UseOpenTelemetry"/>
/// to provide distributed tracing capabilities (per research findings Q1.26).
/// </para>
/// </remarks>
public sealed class ChatClientAgentFactory
{
    /// <summary>
    /// The source name for OpenTelemetry instrumentation, identifying telemetry from this server.
    /// </summary>
    private const string SourceName = "AGUIDojoServer";

    private readonly ChatClient _chatClient;
    private readonly WeatherTool _weatherTool;
    private readonly EmailTool _emailTool;
    private readonly DocumentTool _documentTool;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatClientAgentFactory"/> class.
    /// </summary>
    /// <param name="configuration">The configuration containing LLM provider settings.</param>
    /// <param name="weatherTool">The weather tool for AI function calls.</param>
    /// <param name="emailTool">The email tool for AI function calls.</param>
    /// <param name="documentTool">The document tool for AI function calls.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither Azure OpenAI nor OpenAI credentials are configured.
    /// </exception>
    public ChatClientAgentFactory(
        IConfiguration configuration,
        WeatherTool weatherTool,
        EmailTool emailTool,
        DocumentTool documentTool)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(weatherTool);
        ArgumentNullException.ThrowIfNull(emailTool);
        ArgumentNullException.ThrowIfNull(documentTool);

        this._weatherTool = weatherTool;
        this._emailTool = emailTool;
        this._documentTool = documentTool;

        // Create the real ChatClient with LLM backend
        // Requires OpenAI or Azure OpenAI credentials
        string? endpoint = configuration["AZURE_OPENAI_ENDPOINT"];
        string? deploymentName = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"];
        string? openAiApiKey = configuration["OPENAI_API_KEY"]; // prefer this option over Azure OpenAI
        string? openAiModel = configuration["OPENAI_MODEL"] ?? "gpt-5-mini";

        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(deploymentName))
        {
            // Create the Azure OpenAI client using DefaultAzureCredential.
            AzureOpenAIClient azureOpenAIClient = new(
                new Uri(endpoint),
                new DefaultAzureCredential());

            this._chatClient = azureOpenAIClient.GetChatClient(deploymentName);
        }
        else if (!string.IsNullOrWhiteSpace(openAiApiKey) && !string.IsNullOrWhiteSpace(openAiModel))
        {
            // Create the OpenAI client using an API key.
            OpenAIClient openAIClient = new(openAiApiKey);
            this._chatClient = openAIClient.GetChatClient(openAiModel);
        }
        else
        {
            throw new InvalidOperationException(
                "Either AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT_NAME, or OPENAI_API_KEY and OPENAI_MODEL must be set. " +
                "Alternatively, set USE_MOCK_AGENT=true to use the mock agent for testing without LLM credentials.");
        }
    }

    /// <summary>
    /// Creates an agent for basic agentic chat using LLM capabilities.
    /// </summary>
    /// <returns>An <see cref="AIAgent"/> configured for general conversation, wrapped with OpenTelemetry instrumentation.</returns>
    public AIAgent CreateAgenticChat()
    {
        return this._chatClient.AsIChatClient().AsAIAgent(
            instructions: """
            You are a helpful assistant that can chat with users using LLM capabilities. 
            Format your user-facing text response as in markdown.
            """,
            name: "AgenticChat",
            description: "A simple AI Assistant that can chat with users using LLM capabilities.")
            .AsBuilder()
            .UseOpenTelemetry(SourceName)
            .Build();
    }

    /// <summary>
    /// Creates an agent for backend tool rendering demonstrations.
    /// </summary>
    /// <returns>An <see cref="AIAgent"/> configured with weather tool for backend rendering, wrapped with OpenTelemetry instrumentation.</returns>
    public AIAgent CreateBackendToolRendering()
    {
        return this._chatClient.AsIChatClient().AsAIAgent(
            instructions: """
                You are a helpful assistant that can chat with users and use backend tools to get information.
                When the user asks for information that requires a tool, call the appropriate tool.
                Format your user-facing text response as in markdown.
                """,
            name: "BackendToolRenderer",
            description: "A simple AI Assistant that can chat with users using LLM capabilities. Format your user-facing text response as in markdown.",
            tools: [AIFunctionFactory.Create(
                this._weatherTool.GetWeatherAsync,
                name: "get_weather",
                description: "Get the weather for a given location.",
                AGUIDojoServerSerializerContext.Default.Options)])
            .AsBuilder()
            .UseOpenTelemetry(SourceName)
            .Build();
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only
    /// <summary>
    /// Creates a Human-in-the-Loop agent that requires approval for sensitive tool calls.
    /// The agent wraps the send_email tool with ApprovalRequiredAIFunction to demonstrate
    /// the approval workflow in AG-UI.
    /// </summary>
    /// <param name="jsonSerializerOptions">The JSON serializer options for approval data.</param>
    /// <returns>An AIAgent wrapped with ServerFunctionApprovalAgent and OpenTelemetry instrumentation for approval handling.</returns>
    public AIAgent CreateHumanInTheLoop(JsonSerializerOptions jsonSerializerOptions)
    {
        // Create the approval-required tool using DI-compatible EmailTool
        AITool approvalTool = new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(
                this._emailTool.SendEmailAsync,
                name: "send_email",
                description: "Send an email to a recipient.",
                AGUIDojoServerSerializerContext.Default.Options));

        var baseAgent = this._chatClient.AsIChatClient().AsAIAgent(new ChatClientAgentOptions
        {
            Name = "HumanInTheLoopAgent",
            Description = "An agent that involves human feedback in its decision-making process",
            ChatOptions = new ChatOptions
            {
                Instructions = """
                    You are a helpful assistant that can send emails on behalf of the user.
                    IMPORTANT: When asked to send an email, immediately call the send_email tool.
                    Do NOT ask the user for confirmation in chat - the tool will automatically
                    prompt for approval through the UI. Just call the tool directly.
                    After the user approves or rejects, report the outcome.
                    Format your user-facing text response as in markdown.
                    """,
                Tools = [approvalTool]
            }
        });

        // Wrap with ServerFunctionApprovalAgent to transform approval content to AG-UI format
        // Then wrap with OpenTelemetry for distributed tracing
        return new ServerFunctionApprovalAgent(baseAgent, jsonSerializerOptions)
            .AsBuilder()
            .UseOpenTelemetry(SourceName)
            .Build();
    }
#pragma warning restore MEAI001

    /// <summary>
    /// Creates an agent for tool-based generative UI demonstrations.
    /// </summary>
    /// <returns>An <see cref="AIAgent"/> configured with weather tool for UI generation, wrapped with OpenTelemetry instrumentation.</returns>
    public AIAgent CreateToolBasedGenerativeUI()
    {
        return this._chatClient.AsIChatClient().AsAIAgent(
            instructions: "Format your user-facing text response as in markdown.",
            name: "ToolBasedGenerativeUIAgent",
            description: "A simple AI Assistant that demonstrates tool-based generative UI patterns.",
            tools: [AIFunctionFactory.Create(
                this._weatherTool.GetWeatherAsync,
                name: "get_weather",
                description: "Get the weather for a given location.",
                AGUIDojoServerSerializerContext.Default.Options)])
            .AsBuilder()
            .UseOpenTelemetry(SourceName)
            .Build();
    }

    /// <summary>
    /// Creates an agent for agentic UI demonstrations with planning capabilities.
    /// </summary>
    /// <param name="options">The JSON serializer options for UI state.</param>
    /// <returns>An <see cref="AIAgent"/> configured for agentic planning UI, wrapped with OpenTelemetry instrumentation.</returns>
    public AIAgent CreateAgenticUI(JsonSerializerOptions options)
    {
        var baseAgent = this._chatClient.AsIChatClient().AsAIAgent(new ChatClientAgentOptions
        {
            Name = "AgenticUIAgent",
            Description = "An agent that generates agentic user interfaces using Azure OpenAI",
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
                Tools = [
                    AIFunctionFactory.Create(
                        AgenticPlanningTools.CreatePlan,
                        name: "create_plan",
                        description: "Create a plan with multiple steps.",
                        AGUIDojoServerSerializerContext.Default.Options),
                    AIFunctionFactory.Create(
                        AgenticPlanningTools.UpdatePlanStepAsync,
                        name: "update_plan_step",
                        description: "Update a step in the plan with new description or status.",
                        AGUIDojoServerSerializerContext.Default.Options)
                ],
                AllowMultipleToolCalls = false
            }
        });

        // Wrap with AgenticUIAgent for agentic UI state management, then with OpenTelemetry
        return new AgenticUIAgent(baseAgent, options)
            .AsBuilder()
            .UseOpenTelemetry(SourceName)
            .Build();
    }

    /// <summary>
    /// Creates an agent for shared state demonstrations.
    /// </summary>
    /// <param name="options">The JSON serializer options for state management.</param>
    /// <returns>An <see cref="AIAgent"/> configured for shared state patterns, wrapped with OpenTelemetry instrumentation.</returns>
    public AIAgent CreateSharedState(JsonSerializerOptions options)
    {
        var baseAgent = this._chatClient.AsIChatClient().AsAIAgent(
            instructions: """
                You are a recipe assistant that helps users create and manage recipes.
                When users provide recipe information (ingredients, cooking instructions, preferences),
                update the recipe state accordingly.
                
                If users ask about non-recipe topics, politely guide them back to recipe creation.
                For example, if they mention a favorite color, you might suggest creating a recipe
                that uses ingredients of that color (e.g., blueberry recipes for blue, tomato recipes for red).
                
                Format your user-facing text response as in markdown.
                """,
            name: "SharedStateAgent",
            description: "An agent that demonstrates shared state patterns for recipe management.");

        // Wrap with SharedStateAgent for state management, then with OpenTelemetry
        return new SharedStateAgent(baseAgent, options)
            .AsBuilder()
            .UseOpenTelemetry(SourceName)
            .Build();
    }

    /// <summary>
    /// Creates an agent for predictive state updates demonstrations.
    /// </summary>
    /// <param name="options">The JSON serializer options for state updates.</param>
    /// <returns>An <see cref="AIAgent"/> configured for predictive state update patterns, wrapped with OpenTelemetry instrumentation.</returns>
    public AIAgent CreatePredictiveStateUpdates(JsonSerializerOptions options)
    {
        var baseAgent = this._chatClient.AsIChatClient().AsAIAgent(new ChatClientAgentOptions
        {
            Name = "PredictiveStateUpdatesAgent",
            Description = "An agent that demonstrates predictive state updates.",
            ChatOptions = new ChatOptions
            {
                Instructions = """
                    You are a document editor assistant. When asked to write or edit content:
                    
                    IMPORTANT:
                    - Use the `write_document` tool with the full document text in Markdown format
                    - Format the document extensively so it's easy to read
                    - You can use all kinds of markdown (headings, lists, bold, etc.)
                    - However, do NOT use italic or strike-through formatting
                    - You MUST write the full document, even when changing only a few words
                    - When making edits to the document, try to make them minimal - do not change every word
                    - Keep stories SHORT!
                    - After you are done writing the document you MUST call a confirm_changes tool after you call write_document
                    
                    After the user confirms the changes, provide a brief summary of what you wrote.
                    """,
                Tools = [
                    AIFunctionFactory.Create(
                        this._documentTool.WriteDocumentAsync,
                        name: "write_document",
                        description: "Write a document. Use markdown formatting to format the document.",
                        AGUIDojoServerSerializerContext.Default.Options)
                ]
            }
        });

        // Wrap with PredictiveStateUpdatesAgent for state updates, then with OpenTelemetry
        return new PredictiveStateUpdatesAgent(baseAgent, options)
            .AsBuilder()
            .UseOpenTelemetry(SourceName)
            .Build();
    }
}
