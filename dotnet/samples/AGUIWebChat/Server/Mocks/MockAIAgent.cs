// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AGUIWebChat.Server.Mocks.ToolSets;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AGUIWebChat.Server.Mocks;

/// <summary>
/// A mock AI agent that simulates LLM responses for testing AG-UI features without actual LLM dependencies.
/// </summary>
/// <remarks>
/// <para>
/// This agent provides realistic simulation of streaming text responses and tool calls,
/// enabling development and testing of AG-UI features without requiring OpenAI/Azure OpenAI credentials.
/// </para>
/// <para>
/// Scenarios can be configured via <see cref="MockAgentOptions"/> to trigger different behaviors
/// based on keywords in user input.
/// </para>
/// </remarks>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated conditionally in Program.cs when USE_MOCK_AGENT is true")]
internal sealed class MockAIAgent : AIAgent
{
    private readonly MockAgentOptions _options;
    private readonly ToolSetRegistry? _registry;
    private readonly ILogger<MockAIAgent> _logger;
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockAIAgent"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the mock agent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public MockAIAgent(MockAgentOptions options)
        : this(options, registry: null, logger: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockAIAgent"/> class with a tool set registry.
    /// </summary>
    /// <param name="options">Configuration options for the mock agent.</param>
    /// <param name="registry">
    /// Optional tool set registry for handling tool sequences. When provided, the agent will
    /// attempt to find a matching tool set before falling back to scenario-based responses.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public MockAIAgent(MockAgentOptions options, ToolSetRegistry? registry)
        : this(options, registry, logger: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockAIAgent"/> class with a tool set registry and logger.
    /// </summary>
    /// <param name="options">Configuration options for the mock agent.</param>
    /// <param name="registry">
    /// Optional tool set registry for handling tool sequences. When provided, the agent will
    /// attempt to find a matching tool set before falling back to scenario-based responses.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured logging of scenario detection, tool set selection, and response generation.
    /// If not provided, a null logger is used.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public MockAIAgent(MockAgentOptions options, ToolSetRegistry? registry, ILogger<MockAIAgent>? logger)
        : this(options, registry, logger, loggerFactory: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockAIAgent"/> class with a tool set registry, logger, and logger factory.
    /// </summary>
    /// <param name="options">Configuration options for the mock agent.</param>
    /// <param name="registry">
    /// Optional tool set registry for handling tool sequences. When provided, the agent will
    /// attempt to find a matching tool set before falling back to scenario-based responses.
    /// </param>
    /// <param name="logger">
    /// Optional logger for structured logging of scenario detection, tool set selection, and response generation.
    /// If not provided, a null logger is used.
    /// </param>
    /// <param name="loggerFactory">
    /// Optional logger factory for creating scoped loggers in tool sets via MockSequenceContext.
    /// If not provided, tool sets will not have logging enabled.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public MockAIAgent(MockAgentOptions options, ToolSetRegistry? registry, ILogger<MockAIAgent>? logger, ILoggerFactory? loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        this._options = options;
        this._registry = registry;
        this._logger = logger ?? NullLogger<MockAIAgent>.Instance;
        this._loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public override string? Name => this._options.Name;

    /// <inheritdoc/>
    public override string? Description => this._options.Description;

    /// <inheritdoc/>
    public override ValueTask<AgentThread> GetNewThreadAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<AgentThread>(new MockAgentThread());
    }

    /// <inheritdoc/>
    public override ValueTask<AgentThread> DeserializeThreadAsync(
        JsonElement serializedThread,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<AgentThread>(new MockAgentThread(serializedThread, jsonSerializerOptions));
    }

    /// <inheritdoc/>
    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Delegate to streaming and collect all updates into a single response
        List<ChatMessage> responseMessages = [];
        string? responseId = null;
        DateTimeOffset? createdAt = null;

        await foreach (AgentResponseUpdate update in this.RunCoreStreamingAsync(messages, thread, options, cancellationToken).ConfigureAwait(false))
        {
            responseId ??= update.ResponseId;
            createdAt ??= update.CreatedAt;

            if (update.Contents.Count > 0)
            {
                ChatMessage responseMessage = new(update.Role ?? ChatRole.Assistant, update.Contents)
                {
                    MessageId = update.MessageId,
                    AuthorName = update.AuthorName,
                };
                responseMessages.Add(responseMessage);
            }
        }

        return new AgentResponse(responseMessages)
        {
            ResponseId = responseId,
            CreatedAt = createdAt,
            AgentId = this.Id,
        };
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string responseId = "mock_resp_" + Guid.NewGuid().ToString("N");
        string messageId = "mock_msg_" + Guid.NewGuid().ToString("N");
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        // Extract user input text from the last user message
        string? userMessage = messages
            .LastOrDefault(m => m.Role == ChatRole.User)?
            .Text;

        // Try to find a matching tool set via the registry (Phase 2 pattern)
        if (this._registry != null && !string.IsNullOrEmpty(userMessage))
        {
            IToolSet? toolSet = this._registry.FindByKeywords(userMessage);

            if (toolSet != null)
            {
                this._logger.LogInformation(
                    "Tool set selected: {ToolSetName} for user message: {UserMessagePreview}",
                    toolSet.Name,
                    userMessage.Length > 50 ? userMessage[..50] + "..." : userMessage);

                // Create execution context for the tool set with logger factory for tool set logging
                MockSequenceContext context = new(
                    responseId,
                    createdAt,
                    userMessage,
                    this._options.StreamingDelayMs,
                    this.Name,
                    this._loggerFactory);

                // Delegate to the tool set's sequence execution
                await foreach (AgentResponseUpdate update in toolSet.ExecuteSequenceAsync(context, cancellationToken).ConfigureAwait(false))
                {
                    yield return update;
                }

                this._logger.LogDebug(
                    "Response completed via tool set {ToolSetName} with response ID: {ResponseId}",
                    toolSet.Name,
                    responseId);

                yield break;
            }
        }

        // Fall back to scenario-based logic (Phase 1 pattern for backward compatibility)
        this._logger.LogDebug(
            "No tool set matched, falling back to scenario-based logic for user message: {UserMessagePreview}",
            userMessage?.Length > 50 ? userMessage[..50] + "..." : userMessage ?? "<no message>");

        MockScenario scenario = this.FindMatchingScenario(messages);

        // Process based on scenario type
        switch (scenario.Type)
        {
            case MockScenarioType.TextResponse:
                this._logger.LogDebug(
                    "Streaming text response with {WordCount} words",
                    (scenario.TextContent ?? "Hello! I'm the mock agent.").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
                await foreach (AgentResponseUpdate update in this.StreamTextResponseAsync(
                    scenario.TextContent ?? "Hello! I'm the mock agent.",
                    responseId,
                    messageId,
                    createdAt,
                    cancellationToken).ConfigureAwait(false))
                {
                    yield return update;
                }
                this._logger.LogDebug(
                    "Response completed with text, response ID: {ResponseId}",
                    responseId);
                break;

            case MockScenarioType.ToolCall:
                this._logger.LogInformation(
                    "Emitting tool call: {ToolName}",
                    scenario.ToolCallName ?? "mock_tool");
                yield return this.CreateToolCallUpdate(
                    scenario.ToolCallName ?? "mock_tool",
                    scenario.ToolCallArguments ?? "{}",
                    responseId,
                    messageId,
                    createdAt);
                this._logger.LogDebug(
                    "Response completed with tool call, response ID: {ResponseId}",
                    responseId);
                break;

            case MockScenarioType.Mixed:
                this._logger.LogDebug(
                    "Processing mixed scenario with text and tool call: {ToolName}",
                    scenario.ToolCallName ?? "<none>");
                // First stream the text content
                if (!string.IsNullOrEmpty(scenario.TextContent))
                {
                    await foreach (AgentResponseUpdate update in this.StreamTextResponseAsync(
                        scenario.TextContent,
                        responseId,
                        messageId,
                        createdAt,
                        cancellationToken).ConfigureAwait(false))
                    {
                        yield return update;
                    }
                }

                // Then emit the tool call
                if (!string.IsNullOrEmpty(scenario.ToolCallName))
                {
                    this._logger.LogInformation(
                        "Emitting tool call in mixed scenario: {ToolName}",
                        scenario.ToolCallName);
                    string toolMessageId = "mock_msg_" + Guid.NewGuid().ToString("N");
                    yield return this.CreateToolCallUpdate(
                        scenario.ToolCallName,
                        scenario.ToolCallArguments ?? "{}",
                        responseId,
                        toolMessageId,
                        createdAt);
                }
                this._logger.LogDebug(
                    "Response completed with mixed content, response ID: {ResponseId}",
                    responseId);
                break;
        }
    }

    /// <summary>
    /// Finds a matching scenario based on keywords in the user messages.
    /// </summary>
    private MockScenario FindMatchingScenario(IEnumerable<ChatMessage> messages)
    {
        // Extract user input text from the last user message
        string? userInput = messages
            .LastOrDefault(m => m.Role == ChatRole.User)?
            .Text;

        if (!string.IsNullOrEmpty(userInput))
        {
            // Check for keyword matches
            foreach (KeyValuePair<string, MockScenario> kvp in this._options.ScenariosByTrigger)
            {
                if (userInput.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    this._logger.LogInformation(
                        "Scenario detected: matched keyword '{Keyword}' with scenario type {ScenarioType}",
                        kvp.Key,
                        kvp.Value.Type);
                    return kvp.Value;
                }
            }
        }

        // Fall back to default scenario or a simple text response
        return this._options.DefaultScenario ?? new MockScenario
        {
            Type = MockScenarioType.TextResponse,
            TextContent = "I'm the mock agent. I don't have a specific scenario configured for your request."
        };
    }

    /// <summary>
    /// Streams a text response word-by-word with configurable delay.
    /// </summary>
    private async IAsyncEnumerable<AgentResponseUpdate> StreamTextResponseAsync(
        string text,
        string responseId,
        string messageId,
        DateTimeOffset createdAt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Add space before words (except the first one)
            string token = i > 0 ? " " + words[i] : words[i];

            yield return new AgentResponseUpdate(ChatRole.Assistant, token)
            {
                ResponseId = responseId,
                MessageId = messageId,
                CreatedAt = createdAt,
                AuthorName = this.Name,
            };

            // Delay between tokens to simulate realistic streaming
            if (this._options.StreamingDelayMs > 0 && i < words.Length - 1)
            {
                await Task.Delay(this._options.StreamingDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Creates an update containing a tool call (FunctionCallContent).
    /// </summary>
    private AgentResponseUpdate CreateToolCallUpdate(
        string toolName,
        string toolArguments,
        string responseId,
        string messageId,
        DateTimeOffset createdAt)
    {
        string callId = "mock_call_" + Guid.NewGuid().ToString("N");

        // Parse JSON arguments into a dictionary
        Dictionary<string, object?>? arguments = null;
        if (!string.IsNullOrEmpty(toolArguments) && toolArguments != "{}")
        {
            arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolArguments);
        }

        FunctionCallContent functionCall = new(
            callId,
            toolName,
            arguments);

        return new AgentResponseUpdate(ChatRole.Assistant, [functionCall])
        {
            ResponseId = responseId,
            MessageId = messageId,
            CreatedAt = createdAt,
            AuthorName = this.Name,
        };
    }

    /// <summary>
    /// A private sealed thread implementation for the mock agent using in-memory storage.
    /// </summary>
    private sealed class MockAgentThread : InMemoryAgentThread
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MockAgentThread"/> class.
        /// </summary>
        public MockAgentThread()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockAgentThread"/> class from serialized state.
        /// </summary>
        /// <param name="serializedThreadState">The serialized thread state.</param>
        /// <param name="jsonSerializerOptions">Optional JSON serializer options.</param>
        public MockAgentThread(JsonElement serializedThreadState, JsonSerializerOptions? jsonSerializerOptions = null)
            : base(serializedThreadState, jsonSerializerOptions)
        {
        }
    }
}
