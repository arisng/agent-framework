using System.Text.Json;
using AGUIDojoServer.AgenticUI;
using AGUIDojoServer.Api;
using AGUIDojoServer.HumanInTheLoop;
using AGUIDojoServer.Multimodal;
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
    private const string UnifiedSystemPrompt = """
        You are a versatile AI assistant that helps users with conversations, data queries,
        document editing, planning, and data visualization. Format responses in markdown.

        ## Tool Usage Guidelines

        Choose tools based on user intent:
        - **Email**: Use `send_email` when asked to send an email. The tool will prompt the user
          for approval automatically — do NOT ask for confirmation in chat.
        - **Documents**: Use `write_document` for writing or editing content. Always write the full
          document in markdown. Keep content concise.
          Do NOT use italic or strike-through formatting in documents.
        - **Planning**: Use `create_plan` to start a new plan, then `update_plan_step` to progress
          each step. Only one plan can be active at a time. Do NOT summarize the plan in chat —
          the UI renders it automatically. Continue updating steps until all are completed.
        - **Visualization**: Use `show_chart` for trends/comparisons/distributions, `show_data_grid`
          for tabular data, `show_form` for user input collection.
        - **Weather**: Use `get_weather` when asked about weather conditions.
        - **General chat**: For questions that don't require tools, respond conversationally.

        ## Rules
        - When planning, use tools only without additional chat messages.
        - After tool execution, provide a brief summary of the result.
        - Do NOT repeat tool output in your text response — the UI renders tool results directly.
        - For every final assistant response, prepend an HTML comment in the form `<!-- confidence:0.00 -->`
          using a self-rated score from 0.00 to 1.00. Keep the comment hidden from the user and do not mention it
          in the visible answer.
        """;

    private readonly ChatClient _chatClient;
    private readonly WeatherTool _weatherTool;
    private readonly EmailTool _emailTool;
    private readonly DocumentTool _documentTool;
    private readonly IFileStorageService _fileStorage;

    /// <summary>
    /// Gets an <see cref="IChatClient"/> wrapper around the underlying <see cref="ChatClient"/>,
    /// suitable for lightweight LLM calls such as session title generation.
    /// </summary>
    /// <returns>An <see cref="IChatClient"/> instance backed by the configured LLM provider.</returns>
    public IChatClient GetChatClient() => _chatClient.AsIChatClient();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatClientAgentFactory"/> class.
    /// </summary>
    /// <param name="configuration">The configuration containing LLM provider settings.</param>
    /// <param name="weatherTool">The weather tool for AI function calls.</param>
    /// <param name="emailTool">The email tool for AI function calls.</param>
    /// <param name="documentTool">The document tool for AI function calls.</param>
    /// <param name="fileStorage">The uploaded file storage used to resolve multimodal attachments.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither Azure OpenAI nor OpenAI credentials are configured.
    /// </exception>
    public ChatClientAgentFactory(
        IConfiguration configuration,
        WeatherTool weatherTool,
        EmailTool emailTool,
        DocumentTool documentTool,
        IFileStorageService fileStorage)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(weatherTool);
        ArgumentNullException.ThrowIfNull(emailTool);
        ArgumentNullException.ThrowIfNull(documentTool);
        ArgumentNullException.ThrowIfNull(fileStorage);

        this._weatherTool = weatherTool;
        this._emailTool = emailTool;
        this._documentTool = documentTool;
        this._fileStorage = fileStorage;

        // Create the real ChatClient with LLM backend
        // Requires OpenAI or Azure OpenAI credentials
        string? endpoint = configuration["AZURE_OPENAI_ENDPOINT"];
        string? deploymentName = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"];
        string? openAiApiKey = configuration["OPENAI_API_KEY"]; // prefer this option over Azure OpenAI
        string? openAiModel = configuration["OPENAI_MODEL"] ?? "gpt-5.4-mini";

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

#pragma warning disable MEAI001 // Type is for evaluation purposes only
    /// <summary>
    /// Creates the unified server-side agent for the consolidated <c>/chat</c> endpoint.
    /// </summary>
    /// <param name="jsonSerializerOptions">The JSON serializer options for wrapper-specific state transformations.</param>
    /// <returns>An <see cref="AIAgent"/> composed from the unified tool registry and wrapper pipeline.</returns>
    public AIAgent CreateUnifiedAgent(JsonSerializerOptions jsonSerializerOptions)
    {
        IChatClient wrappedClient = new ToolResultStreamingChatClient(
            new ContextWindowChatClient(this._chatClient.AsIChatClient(), maxNonSystemMessages: 80));

        var baseAgent = wrappedClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "UnifiedAgent",
            Description = "Unified agentic chat agent with all AG-UI capabilities",
            ChatOptions = new ChatOptions
            {
                Instructions = UnifiedSystemPrompt,
                Tools = CreateUnifiedChatTools(),
            }
        });

        return baseAgent
            .AsBuilder()
            .Use(inner => new MultimodalAttachmentAgent(inner, this._fileStorage))
            .Use(inner => new ServerFunctionApprovalAgent(inner, jsonSerializerOptions))
            .Use(inner => new AgenticUIAgent(inner, jsonSerializerOptions))
            .Use(inner => new PredictiveStateUpdatesAgent(inner, jsonSerializerOptions))
            .Use(inner => new SharedStateAgent(inner, jsonSerializerOptions))
            .UseOpenTelemetry(SourceName)
            .Build();
    }
#pragma warning restore MEAI001

    #pragma warning disable MEAI001 // Type is for evaluation purposes only
    private AITool[] CreateUnifiedChatTools()
    {
        return
        [
            AIFunctionFactory.Create(
                this._weatherTool.GetWeatherAsync,
                name: "get_weather",
                description: "Get the weather for a given location.",
                AGUIDojoServerSerializerContext.Default.Options),
            new ApprovalRequiredAIFunction(
                AIFunctionFactory.Create(
                    this._emailTool.SendEmailAsync,
                    name: "send_email",
                    description: "Send an email to a recipient. Requires user approval before sending.",
                    AGUIDojoServerSerializerContext.Default.Options)),
            AIFunctionFactory.Create(
                this._documentTool.WriteDocumentAsync,
                name: "write_document",
                description: "Write or edit a document. Use markdown formatting. Always write the full document.",
                AGUIDojoServerSerializerContext.Default.Options),
            AIFunctionFactory.Create(
                AgenticPlanningTools.CreatePlan,
                name: "create_plan",
                description: "Create a plan with multiple steps for task execution.",
                AGUIDojoServerSerializerContext.Default.Options),
            AIFunctionFactory.Create(
                AgenticPlanningTools.UpdatePlanStepAsync,
                name: "update_plan_step",
                description: "Update a step in an existing plan with new description or status.",
                AGUIDojoServerSerializerContext.Default.Options),
            AIFunctionFactory.Create(
                ChartTool.ShowChartAsync,
                name: "show_chart",
                description: "Show data as a chart (bar, line, pie, area). Use for trends, comparisons, distributions.",
                AGUIDojoServerSerializerContext.Default.Options),
            AIFunctionFactory.Create(
                DataGridTool.ShowDataGridAsync,
                name: "show_data_grid",
                description: "Show structured data in a rich table view. Use for lists, inventories, tabular data.",
                AGUIDojoServerSerializerContext.Default.Options),
            AIFunctionFactory.Create(
                DynamicFormTool.ShowFormAsync,
                name: "show_form",
                description: "Show a dynamic form for user input. Use for registrations, feedback, orders.",
                AGUIDojoServerSerializerContext.Default.Options)
        ];
    }
#pragma warning restore MEAI001
}
