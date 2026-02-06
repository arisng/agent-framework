// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer.SharedState;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by ChatClientAgentFactory.CreateSharedState")]
internal sealed class SharedStateAgent : DelegatingAIAgent
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public SharedStateAgent(AIAgent innerAgent, JsonSerializerOptions jsonSerializerOptions)
        : base(innerAgent)
    {
        this._jsonSerializerOptions = jsonSerializerOptions;
    }

    protected override Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this.RunCoreStreamingAsync(messages, session, options, cancellationToken).ToAgentResponseAsync(cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options is not ChatClientAgentRunOptions { ChatOptions.AdditionalProperties: { } properties } chatRunOptions ||
            !properties.TryGetValue("ag_ui_state", out JsonElement state))
        {
            await foreach (var update in this.InnerAgent.RunStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
            yield break;
        }

        // Try to parse incoming state to determine if it's a Recipe or generic state
        bool isRecipeState = IsRecipeState(state);

        var firstRunOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = chatRunOptions.ChatOptions.Clone(),
            AllowBackgroundResponses = chatRunOptions.AllowBackgroundResponses,
            ContinuationToken = chatRunOptions.ContinuationToken,
            ChatClientFactory = chatRunOptions.ChatClientFactory,
        };

        // Only configure RecipeResponse JSON schema if current state is recipe-like
        // For other state types, use the generic JSON format
        if (isRecipeState)
        {
            firstRunOptions.ChatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema<RecipeResponse>(
                schemaName: "RecipeResponse",
                schemaDescription: "A response containing a recipe with title, skill level, cooking time, preferences, ingredients, and instructions");
        }
        else
        {
            // For non-recipe state, use generic JSON object format
            firstRunOptions.ChatOptions.ResponseFormat = ChatResponseFormat.Json;
        }

        ChatMessage stateUpdateMessage = new(
            ChatRole.System,
            [
                new TextContent("Here is the current state in JSON format:"),
                    new TextContent(state.GetRawText()),
                    new TextContent("The new state is:")
            ]);

        var firstRunMessages = messages.Append(stateUpdateMessage);

        var allUpdates = new List<AgentResponseUpdate>();

        // Run first agent invocation to get updated state
        await foreach (var update in this.InnerAgent.RunStreamingAsync(firstRunMessages, session, firstRunOptions, cancellationToken).ConfigureAwait(false))
        {
            allUpdates.Add(update);

            // Yield all non-text updates (tool calls, etc.)
            bool hasNonTextContent = update.Contents.Any(c => c is not TextContent);
            if (hasNonTextContent)
            {
                yield return update;
            }
        }

        var response = allUpdates.ToAgentResponse();

        // Try to deserialize and serialize state snapshot
        if (response.TryDeserialize(this._jsonSerializerOptions, out JsonElement stateSnapshot))
        {
            byte[] stateBytes = JsonSerializer.SerializeToUtf8Bytes(
                stateSnapshot,
                this._jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)));
            yield return new AgentResponseUpdate
            {
                Contents = [new DataContent(stateBytes, "application/json")]
            };
        }
        else
        {
            // Deserialization failed - provide feedback but continue
            System.Diagnostics.Debug.WriteLine("SharedStateAgent: Failed to deserialize state snapshot");
            yield return new AgentResponseUpdate
            {
                Contents = [new TextContent("Unable to process state update. Please provide recipe information (ingredients, instructions, etc.) to update the recipe.")]
            };
            yield break;
        }

        // Run second agent invocation to get summary
        var secondRunMessages = messages.Concat(response.Messages).Append(
            new ChatMessage(
                ChatRole.System,
                [new TextContent("Please provide a concise summary of the state changes in at most two sentences.")]));

        await foreach (var update in this.InnerAgent.RunStreamingAsync(secondRunMessages, session, options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Determines if the given state JSON represents a Recipe object.
    /// Returns true if the state has recipe-specific properties like "ingredients", "title", etc.
    /// </summary>
    private static bool IsRecipeState(JsonElement state)
    {
        try
        {
            // Check for recipe-specific properties
            if (state.ValueKind == JsonValueKind.Object)
            {
                return state.TryGetProperty("recipe", out JsonElement recipeProp) ||
                       (state.TryGetProperty("ingredients", out _) && state.TryGetProperty("title", out _));
            }
        }
        catch
        {
            // If we can't parse it, assume it's not a recipe
        }

        return false;
    }
}
