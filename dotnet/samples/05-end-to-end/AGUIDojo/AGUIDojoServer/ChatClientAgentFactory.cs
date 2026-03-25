using System.Text.Json;
using System.Linq;
using AGUIDojoServer.AgenticUI;
using AGUIDojoServer.Api;
using AGUIDojoServer.ChatSessions;
using AGUIDojoServer.Data;
using AGUIDojoServer.HumanInTheLoop;
using AGUIDojoServer.Multimodal;
using AGUIDojoServer.Models;
using AGUIDojoServer.PredictiveStateUpdates;
using AGUIDojoServer.SharedState;
using AGUIDojoServer.Tools;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

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
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IModelRegistry _modelRegistry;

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
    /// <param name="httpContextAccessor">Provides access to the current request for persistence integration.</param>
    /// <param name="modelRegistry">The model registry used to resolve routing and compaction context windows.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither Azure OpenAI nor OpenAI credentials are configured.
    /// </exception>
    public ChatClientAgentFactory(
        IConfiguration configuration,
        WeatherTool weatherTool,
        EmailTool emailTool,
        DocumentTool documentTool,
        IFileStorageService fileStorage,
        IHttpContextAccessor httpContextAccessor,
        IModelRegistry modelRegistry)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(weatherTool);
        ArgumentNullException.ThrowIfNull(emailTool);
        ArgumentNullException.ThrowIfNull(documentTool);
        ArgumentNullException.ThrowIfNull(fileStorage);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(modelRegistry);

        this._weatherTool = weatherTool;
        this._emailTool = emailTool;
        this._documentTool = documentTool;
        this._fileStorage = fileStorage;
        this._httpContextAccessor = httpContextAccessor;
        this._modelRegistry = modelRegistry;

        // Create the real ChatClient with LLM backend
        // Requires OpenAI or Azure OpenAI credentials
        string? endpoint = configuration["AZURE_OPENAI_ENDPOINT"];
        string? deploymentName = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"];
        string? openAiApiKey = configuration["OPENAI_API_KEY"]; // prefer this option over Azure OpenAI
        string? openAiModel = configuration["OPENAI_MODEL"] ?? "gpt-5.4-nano";

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
        ToolResultReplayStore replayStore = new();
        IChatClient wrappedClient = new ToolResultUnwrappingChatClient(
            new FunctionInvokingChatClient(
                new ToolResultStreamingChatClient(this._chatClient.AsIChatClient(), replayStore),
                null,
                null),
            replayStore);

        var baseAgent = wrappedClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "UnifiedAgent",
            Description = "Unified agentic chat agent with all AG-UI capabilities",
            ChatOptions = new ChatOptions
            {
                Instructions = UnifiedSystemPrompt,
                Tools = CreateUnifiedChatTools(),
                ModelId = this._modelRegistry.ActiveModelId,
            }
        });

        return baseAgent
            .AsBuilder()
            .Use(inner => new MultimodalAttachmentAgent(inner, this._fileStorage))
            .Use(inner => new ServerFunctionApprovalAgent(inner, jsonSerializerOptions))
            .Use(inner => new AgenticUIAgent(inner, jsonSerializerOptions))
            .Use(inner => new PredictiveStateUpdatesAgent(inner, jsonSerializerOptions))
            .Use(inner => new SharedStateAgent(inner, jsonSerializerOptions))
            .Use(inner => new ModelRoutingAgent(inner, this._modelRegistry, this._httpContextAccessor))
            .Use(inner => new ConversationPersistenceAgent(inner, this._httpContextAccessor))
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

    private sealed class ModelRoutingAgent : DelegatingAIAgent
    {
        private readonly IModelRegistry _modelRegistry;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ModelRoutingAgent(AIAgent innerAgent, IModelRegistry modelRegistry, IHttpContextAccessor httpContextAccessor)
            : base(innerAgent)
        {
            this._modelRegistry = modelRegistry;
            this._httpContextAccessor = httpContextAccessor;
        }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<AIChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return this.RunCoreStreamingAsync(messages, session, options, cancellationToken)
                .ToAgentResponseAsync(cancellationToken);
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<AIChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            List<AIChatMessage> inputMessages = messages.ToList();
            ChatClientAgentRunOptions routedOptions = PrepareRunOptions(options, out string preferredModelId, out string effectiveModelId, out string routingReason);
            string recordedPreferredModelId = string.IsNullOrWhiteSpace(preferredModelId) ? effectiveModelId : preferredModelId;
            IEnumerable<AIChatMessage> compactedMessages = await CompactMessagesAsync(effectiveModelId, inputMessages, cancellationToken).ConfigureAwait(false);
            List<AIChatMessage> compactedMessageList = compactedMessages.ToList();

            await PersistRoutingAuditAsync(recordedPreferredModelId, effectiveModelId, routingReason, inputMessages.Count, compactedMessageList.Count, cancellationToken).ConfigureAwait(false);

            await foreach (AgentResponseUpdate update in this.InnerAgent.RunStreamingAsync(compactedMessageList, session, routedOptions, cancellationToken).ConfigureAwait(false))
            {
                update.AdditionalProperties ??= [];
                update.AdditionalProperties["preferredModelId"] = recordedPreferredModelId;
                update.AdditionalProperties["effectiveModelId"] = effectiveModelId;
                update.AdditionalProperties["modelRoutingReason"] = routingReason;
                update.AdditionalProperties["inputMessageCount"] = inputMessages.Count;
                update.AdditionalProperties["outputMessageCount"] = compactedMessageList.Count;
                update.AdditionalProperties["wasCompacted"] = compactedMessageList.Count < inputMessages.Count;
                yield return update;
            }
        }

        private ChatClientAgentRunOptions PrepareRunOptions(
            AgentRunOptions? options,
            out string preferredModelId,
            out string effectiveModelId,
            out string routingReason)
        {
            ChatClientAgentRunOptions routedOptions = options as ChatClientAgentRunOptions ?? new ChatClientAgentRunOptions();
            routedOptions = (ChatClientAgentRunOptions)routedOptions.Clone();

            preferredModelId = ExtractPreferredModelId(routedOptions);
            if (string.IsNullOrWhiteSpace(preferredModelId))
            {
                effectiveModelId = this._modelRegistry.ActiveModelId;
                routingReason = $"No preferred model was supplied; using active model {effectiveModelId}.";
            }
            else if (this._modelRegistry.GetModel(preferredModelId) is null)
            {
                effectiveModelId = this._modelRegistry.ActiveModelId;
                routingReason = $"Requested model {preferredModelId} is not registered; using active model {effectiveModelId}.";
            }
            else
            {
                effectiveModelId = preferredModelId;
                routingReason = string.Equals(preferredModelId, effectiveModelId, StringComparison.Ordinal)
                    ? $"Routing to preferred model {effectiveModelId}."
                    : $"Routing preference {preferredModelId} resolved to {effectiveModelId}.";
            }

            routedOptions.ChatOptions ??= new ChatOptions();
            routedOptions.ChatOptions.ModelId = effectiveModelId;
            routedOptions.ChatOptions.AdditionalProperties ??= [];
            routedOptions.ChatOptions.AdditionalProperties["preferredModelId"] = string.IsNullOrWhiteSpace(preferredModelId) ? effectiveModelId : preferredModelId;
            routedOptions.ChatOptions.AdditionalProperties["effectiveModelId"] = effectiveModelId;
            routedOptions.ChatOptions.AdditionalProperties["modelRoutingReason"] = routingReason;

            return routedOptions;
        }

        private async Task<IEnumerable<AIChatMessage>> CompactMessagesAsync(
            string effectiveModelId,
            List<AIChatMessage> messages,
            CancellationToken cancellationToken)
        {
            ModelInfo model = this._modelRegistry.GetModel(effectiveModelId) ?? new ModelInfo(effectiveModelId, effectiveModelId, ContextWindowTokens: 128_000);
            CompactionStrategy strategy = BuildCompactionStrategy(model);
            return await CompactionProvider.CompactAsync(strategy, messages, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private async Task PersistRoutingAuditAsync(
            string preferredModelId,
            string effectiveModelId,
            string routingReason,
            int inputMessageCount,
            int outputMessageCount,
            CancellationToken cancellationToken)
        {
            HttpContext? httpContext = this._httpContextAccessor.HttpContext;
            if (httpContext is null ||
                !ChatSessionHttpContextItems.TryGetRoutingContext(httpContext, out ChatSessionRoutingContext? routingContext))
            {
                return;
            }

            string sessionId = routingContext.SessionId;
            ChatSessionAuditService? auditService = httpContext.RequestServices.GetService<ChatSessionAuditService>();
            if (auditService is null)
            {
                return;
            }

            await auditService.AppendAsync(
                sessionId,
                ChatAuditEventType.ModelRouting,
                $"Model routed to {effectiveModelId}",
                routingReason,
                new
                {
                    preferredModelId,
                    effectiveModelId,
                    routingReason,
                },
                correlationId: sessionId,
                ct: cancellationToken).ConfigureAwait(false);

            bool wasCompacted = outputMessageCount < inputMessageCount;
            await auditService.AppendAsync(
                sessionId,
                ChatAuditEventType.CompactionCheckpoint,
                wasCompacted ? "Compaction reduced invocation context" : "Compaction checkpoint evaluated without reducing context",
                $"Input messages: {inputMessageCount}; compacted messages: {outputMessageCount}.",
                new
                {
                    preferredModelId,
                    effectiveModelId,
                    inputMessageCount,
                    outputMessageCount,
                    wasCompacted,
                    routingReason,
                },
                correlationId: sessionId,
                ct: cancellationToken).ConfigureAwait(false);
        }

        private static string ExtractPreferredModelId(ChatClientAgentRunOptions routedOptions)
        {
            if (routedOptions.ChatOptions?.AdditionalProperties is null ||
                !routedOptions.ChatOptions.AdditionalProperties.TryGetValue("ag_ui_forwarded_properties", out object? rawForwardedProps) ||
                rawForwardedProps is not JsonElement forwardedProps ||
                forwardedProps.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            if (forwardedProps.TryGetProperty("preferredModelId", out JsonElement preferredModel) &&
                preferredModel.ValueKind == JsonValueKind.String)
            {
                return preferredModel.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private static PipelineCompactionStrategy BuildCompactionStrategy(ModelInfo model)
        {
            int contextWindow = Math.Max(16_000, model.ContextWindowTokens);
            int toolTrigger = Math.Max(8_000, contextWindow - Math.Min(64_000, contextWindow / 5));
            int turnTrigger = Math.Max(4, contextWindow / 128_000);
            int hardTrigger = Math.Max(toolTrigger + 2_000, contextWindow - Math.Min(32_000, contextWindow / 10));

            CompactionTrigger toolTriggerPredicate = CompactionTriggers.All(
                CompactionTriggers.HasToolCalls(),
                CompactionTriggers.TokensExceed(toolTrigger));

            return new PipelineCompactionStrategy(
                new ToolResultCompactionStrategy(toolTriggerPredicate, minimumPreservedGroups: 12),
                new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(turnTrigger), minimumPreservedTurns: 4),
                new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(hardTrigger), minimumPreservedGroups: 24));
        }
    }
}
