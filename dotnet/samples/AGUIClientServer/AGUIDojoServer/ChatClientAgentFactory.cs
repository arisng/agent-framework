// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Text.Json;
using AGUIDojoServer.AgenticUI;
using AGUIDojoServer.BackendToolRendering;
using AGUIDojoServer.HumanInTheLoop;
using AGUIDojoServer.PredictiveStateUpdates;
using AGUIDojoServer.SharedState;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace AGUIDojoServer;

internal static class ChatClientAgentFactory
{
    private static ChatClient chatClient = null!;

    public static void Initialize(IConfiguration configuration)
    {
        // Create the real ChatClientAgent with LLM backend
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
                "Either AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT_NAME, or OPENAI_API_KEY and OPENAI_MODEL must be set. " +
                "Alternatively, set USE_MOCK_AGENT=true to use the mock agent for testing without LLM credentials.");
        }
    }

    public static ChatClientAgent CreateAgenticChat()
    {
        return chatClient.AsIChatClient().AsAIAgent(
            instructions: """
            You are a helpful assistant that can chat with users using LLM capabilities. 
            Format your user-facing text response as in markdown.
            """,
            name: "AgenticChat",
            description: "A simple AI Assistant that can chat with users using LLM capabilities.");
    }

    public static ChatClientAgent CreateBackendToolRendering()
    {
        return chatClient.AsIChatClient().AsAIAgent(
            instructions: """
                You are a helpful assistant that can chat with users and use backend tools to get information.
                When the user asks for information that requires a tool, call the appropriate tool.
                Format your user-facing text response as in markdown.
                """,
            name: "BackendToolRenderer",
            description: "A simple AI Assistant that can chat with users using LLM capabilities. Format your user-facing text response as in markdown.",
            tools: [AIFunctionFactory.Create(
                GetWeather,
                name: "get_weather",
                description: "Get the weather for a given location.",
                AGUIDojoServerSerializerContext.Default.Options)]);
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only
    /// <summary>
    /// Creates a Human-in-the-Loop agent that requires approval for sensitive tool calls.
    /// The agent wraps the send_email tool with ApprovalRequiredAIFunction to demonstrate
    /// the approval workflow in AG-UI.
    /// </summary>
    /// <param name="jsonSerializerOptions">The JSON serializer options for approval data.</param>
    /// <returns>An AIAgent wrapped with ServerFunctionApprovalAgent for approval handling.</returns>
    public static AIAgent CreateHumanInTheLoop(JsonSerializerOptions jsonSerializerOptions)
    {
        // Create the approval-required tool
        AITool approvalTool = new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(
                SendEmail,
                name: "send_email",
                description: "Send an email to a recipient.",
                AGUIDojoServerSerializerContext.Default.Options));

        var baseAgent = chatClient.AsIChatClient().AsAIAgent(new ChatClientAgentOptions
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
        return new ServerFunctionApprovalAgent(baseAgent, jsonSerializerOptions);
    }
#pragma warning restore MEAI001

    public static ChatClientAgent CreateToolBasedGenerativeUI()
    {
        return chatClient.AsIChatClient().AsAIAgent(
            instructions: "Format your user-facing text response as in markdown.",
            name: "ToolBasedGenerativeUIAgent",
            description: "A simple AI Assistant that demonstrates tool-based generative UI patterns.",
            tools: [AIFunctionFactory.Create(
                GetWeather,
                name: "get_weather",
                description: "Get the weather for a given location.",
                AGUIDojoServerSerializerContext.Default.Options)]);
    }

    public static AIAgent CreateAgenticUI(JsonSerializerOptions options)
    {
        var baseAgent = chatClient.AsIChatClient().AsAIAgent(new ChatClientAgentOptions
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

        return new AgenticUIAgent(baseAgent, options);
    }

    public static AIAgent CreateSharedState(JsonSerializerOptions options)
    {
        var baseAgent = chatClient.AsIChatClient().AsAIAgent(
            name: "SharedStateAgent",
            description: "An agent that demonstrates shared state patterns. Format your user-facing text response as in markdown.");

        return new SharedStateAgent(baseAgent, options);
    }

    public static AIAgent CreatePredictiveStateUpdates(JsonSerializerOptions options)
    {
        var baseAgent = chatClient.AsIChatClient().AsAIAgent(new ChatClientAgentOptions
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
                        WriteDocument,
                        name: "write_document",
                        description: "Write a document. Use markdown formatting to format the document.",
                        AGUIDojoServerSerializerContext.Default.Options)
                ]
            }
        });

        return new PredictiveStateUpdatesAgent(baseAgent, options);
    }

    [Description("Get the weather for a given location.")]
    private static async Task<WeatherInfo> GetWeather([Description("The location to get the weather for.")] string location)
    {
        // Add artificial delay to demonstrate tool call appearing before result in UI
        // This allows users to see the tool call in progress before the LLM processes the result
        await Task.Delay(1500);

        return new WeatherInfo
        {
            Temperature = 20,
            Conditions = "sunny",
            Humidity = 50,
            WindSpeed = 10,
            FeelsLike = 25
        };
    }

    [Description("Send an email to a recipient.")]
    private static string SendEmail(
        [Description("The email address of the recipient.")] string to,
        [Description("The subject line of the email.")] string subject,
        [Description("The body content of the email.")] string body)
    {
        // Simulate sending email
        return $"Email sent successfully to {to} with subject '{subject}'.";
    }

    [Description("Write a document in markdown format.")]
    private static string WriteDocument([Description("The document content to write.")] string document)
    {
        // Simply return success - the document is tracked via state updates
        return "Document written successfully";
    }
}
