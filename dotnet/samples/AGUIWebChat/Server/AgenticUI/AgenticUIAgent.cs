// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AGUIWebChatServer.AgenticUI;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated in Program.cs")]
internal sealed class AgenticUIAgent : DelegatingAIAgent
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public AgenticUIAgent(AIAgent innerAgent, JsonSerializerOptions jsonSerializerOptions)
        : base(innerAgent)
    {
        this._jsonSerializerOptions = jsonSerializerOptions;
    }

    protected override Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentThread? thread = null, AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        return this.RunCoreStreamingAsync(messages, thread, options, cancellationToken).ToAgentResponseAsync(cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var trackedFunctionCalls = new Dictionary<string, FunctionCallContent>();

        await foreach (AgentResponseUpdate update in this.InnerAgent.RunStreamingAsync(messages, thread, options, cancellationToken).ConfigureAwait(false))
        {
            List<AIContent> stateEventsToEmit = [];

            foreach (AIContent content in update.Contents)
            {
                if (content is FunctionCallContent callContent)
                {
                    if (callContent.Name == "create_plan" || callContent.Name == "update_plan_step")
                    {
                        trackedFunctionCalls[callContent.CallId] = callContent;
                        break;
                    }
                }
                else if (content is FunctionResultContent resultContent)
                {
                    if (trackedFunctionCalls.TryGetValue(resultContent.CallId, out FunctionCallContent? matchedCall))
                    {
                        // Handle different result types: JsonElement, Dictionary, List, or any object
                        byte[] bytes = resultContent.Result switch
                        {
                            JsonElement jsonElement => JsonSerializer.SerializeToUtf8Bytes(jsonElement, this._jsonSerializerOptions),
                            string strResult => JsonSerializer.SerializeToUtf8Bytes(JsonDocument.Parse(strResult).RootElement, this._jsonSerializerOptions),
                            _ => JsonSerializer.SerializeToUtf8Bytes(resultContent.Result!, this._jsonSerializerOptions)
                        };

                        if (matchedCall.Name == "create_plan")
                        {
                            stateEventsToEmit.Add(new DataContent(bytes, "application/json"));
                        }
                        else if (matchedCall.Name == "update_plan_step")
                        {
                            stateEventsToEmit.Add(new DataContent(bytes, "application/json-patch+json"));
                        }
                    }
                }
            }

            yield return update;

            yield return new AgentResponseUpdate(
                new ChatResponseUpdate(role: ChatRole.System, stateEventsToEmit)
                {
                    MessageId = "delta_" + Guid.NewGuid().ToString("N"),
                    CreatedAt = update.CreatedAt,
                    ResponseId = update.ResponseId,
                    AuthorName = update.AuthorName,
                    Role = update.Role,
                    ContinuationToken = update.ContinuationToken,
                    AdditionalProperties = update.AdditionalProperties,
                })
            {
                AuthorName = update.AuthorName,
                CreatedAt = update.CreatedAt,
                ResponseId = update.ResponseId,
                Role = update.Role,
                ContinuationToken = update.ContinuationToken,
                AdditionalProperties = update.AdditionalProperties
            };
        }
    }
}
