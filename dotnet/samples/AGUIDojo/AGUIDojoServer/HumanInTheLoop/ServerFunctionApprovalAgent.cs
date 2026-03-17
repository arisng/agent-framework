// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer.HumanInTheLoop;

/// <summary>
/// A delegating agent that handles function approval requests on the server side.
/// Transforms FunctionApprovalRequestContent to the AG-UI request_approval tool call pattern,
/// and transforms approval responses back to FunctionApprovalResponseContent.
/// </summary>
internal sealed class ServerFunctionApprovalAgent : DelegatingAIAgent
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerFunctionApprovalAgent"/> class.
    /// </summary>
    /// <param name="innerAgent">The inner agent to delegate to.</param>
    /// <param name="jsonSerializerOptions">The JSON serializer options for approval data.</param>
    public ServerFunctionApprovalAgent(AIAgent innerAgent, JsonSerializerOptions jsonSerializerOptions)
        : base(innerAgent)
    {
        this._jsonSerializerOptions = jsonSerializerOptions;
    }

    /// <inheritdoc/>
    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return this.RunCoreStreamingAsync(messages, session, options, cancellationToken)
            .ToAgentResponseAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Process and transform incoming approval responses from client
        var processedMessages = ProcessIncomingFunctionApprovals(messages.ToList(), this._jsonSerializerOptions);

        // Run the inner agent and intercept any approval requests
        await foreach (var update in this.InnerAgent.RunStreamingAsync(
            processedMessages, session, options, cancellationToken).ConfigureAwait(false))
        {
            yield return ProcessOutgoingApprovalRequests(update, this._jsonSerializerOptions);
        }
    }

#pragma warning disable MEAI001 // Type is for evaluation purposes only
    private static FunctionApprovalRequestContent ConvertToolCallToApprovalRequest(FunctionCallContent toolCall, JsonSerializerOptions jsonSerializerOptions)
    {
        if (toolCall.Name != "request_approval" || toolCall.Arguments == null)
        {
            throw new InvalidOperationException("Invalid request_approval tool call");
        }

        var request = toolCall.Arguments.TryGetValue("request", out var reqObj) &&
            reqObj is JsonElement argsElement &&
            argsElement.Deserialize<ApprovalRequest>(jsonSerializerOptions) is ApprovalRequest approvalRequest
            ? approvalRequest
            : null;

        if (request == null)
        {
            throw new InvalidOperationException("Failed to deserialize approval request from tool call");
        }

        var functionArgs = request.FunctionArguments is { } args
            ? args.Deserialize<Dictionary<string, object?>>(jsonSerializerOptions)
            : null;

        return new FunctionApprovalRequestContent(
            id: request.ApprovalId,
            new FunctionCallContent(
                callId: request.ApprovalId,
                name: request.FunctionName,
                arguments: functionArgs));
    }

    private static FunctionApprovalResponseContent ConvertToolResultToApprovalResponse(FunctionResultContent result, FunctionApprovalRequestContent approval, JsonSerializerOptions jsonSerializerOptions)
    {
        var approvalResponse = result.Result is JsonElement je
            ? je.Deserialize<ApprovalResponse>(jsonSerializerOptions)
            : result.Result is string str
                ? JsonSerializer.Deserialize<ApprovalResponse>(str, jsonSerializerOptions)
                : result.Result as ApprovalResponse;

        if (approvalResponse == null)
        {
            throw new InvalidOperationException("Failed to deserialize approval response from tool result");
        }

        return approval.CreateResponse(approvalResponse.Approved);
    }
#pragma warning restore MEAI001

    private static List<ChatMessage> CopyMessagesUpToIndex(List<ChatMessage> messages, int index)
    {
        var result = new List<ChatMessage>(index);
        for (int i = 0; i < index; i++)
        {
            result.Add(messages[i]);
        }
        return result;
    }

    private static List<AIContent> CopyContentsUpToIndex(IList<AIContent> contents, int index)
    {
        var result = new List<AIContent>(index);
        for (int i = 0; i < index; i++)
        {
            result.Add(contents[i]);
        }
        return result;
    }

    private static List<ChatMessage> ProcessIncomingFunctionApprovals(
        List<ChatMessage> messages,
        JsonSerializerOptions jsonSerializerOptions)
    {
        List<ChatMessage>? result = null;

#pragma warning disable MEAI001 // Type is for evaluation purposes only
        Dictionary<string, FunctionApprovalRequestContent> trackedRequestApprovalToolCalls = new();
        for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
        {
            var message = messages[messageIndex];
            List<AIContent>? transformedContents = null;
            for (int j = 0; j < message.Contents.Count; j++)
            {
                var content = message.Contents[j];
                if (content is FunctionCallContent { Name: "request_approval" } toolCall)
                {
                    result ??= CopyMessagesUpToIndex(messages, messageIndex);
                    transformedContents ??= CopyContentsUpToIndex(message.Contents, j);
                    var approvalRequest = ConvertToolCallToApprovalRequest(toolCall, jsonSerializerOptions);
                    transformedContents.Add(approvalRequest);
                    trackedRequestApprovalToolCalls[toolCall.CallId] = approvalRequest;
                }
                else if (content is FunctionResultContent toolResult &&
                    trackedRequestApprovalToolCalls.TryGetValue(toolResult.CallId, out var approval))
                {
                    result ??= CopyMessagesUpToIndex(messages, messageIndex);
                    transformedContents ??= CopyContentsUpToIndex(message.Contents, j);
                    var approvalResponse = ConvertToolResultToApprovalResponse(toolResult, approval, jsonSerializerOptions);
                    transformedContents.Add(approvalResponse);
                }
                else if (transformedContents != null)
                {
                    transformedContents.Add(content);
                }
            }

            if (transformedContents != null)
            {
                result!.Add(new ChatMessage(message.Role, transformedContents)
                {
                    AuthorName = message.AuthorName,
                    MessageId = message.MessageId,
                    CreatedAt = message.CreatedAt,
                    RawRepresentation = message.RawRepresentation,
                    AdditionalProperties = message.AdditionalProperties
                });
            }
            else if (result != null)
            {
                result.Add(message);
            }
        }
#pragma warning restore MEAI001

        return result ?? messages;
    }

    private static AgentResponseUpdate ProcessOutgoingApprovalRequests(
        AgentResponseUpdate update,
        JsonSerializerOptions jsonSerializerOptions)
    {
        IList<AIContent>? updatedContents = null;
        for (var i = 0; i < update.Contents.Count; i++)
        {
            var content = update.Contents[i];
#pragma warning disable MEAI001 // Type is for evaluation purposes only
            if (content is FunctionApprovalRequestContent request)
            {
                updatedContents ??= [.. update.Contents];
                var functionCall = request.FunctionCall;
                var approvalId = request.Id;

                var approvalData = new ApprovalRequest
                {
                    ApprovalId = approvalId,
                    FunctionName = functionCall.Name,
                    FunctionArguments = functionCall.Arguments != null
                        ? JsonSerializer.SerializeToElement(functionCall.Arguments, jsonSerializerOptions)
                        : null,
                    Message = $"Approve execution of '{functionCall.Name}'?"
                };

                updatedContents[i] = new FunctionCallContent(
                    callId: approvalId,
                    name: "request_approval",
                    arguments: new Dictionary<string, object?> { ["request"] = approvalData });
            }
#pragma warning restore MEAI001
        }

        if (updatedContents is not null)
        {
            var chatUpdate = update.AsChatResponseUpdate();
            return new AgentResponseUpdate(new ChatResponseUpdate()
            {
                Role = chatUpdate.Role,
                Contents = updatedContents,
                MessageId = chatUpdate.MessageId,
                AuthorName = chatUpdate.AuthorName,
                CreatedAt = chatUpdate.CreatedAt,
                RawRepresentation = chatUpdate.RawRepresentation,
                ResponseId = chatUpdate.ResponseId,
                AdditionalProperties = chatUpdate.AdditionalProperties
            })
            {
                AgentId = update.AgentId,
                ContinuationToken = update.ContinuationToken
            };
        }

        return update;
    }
}

/// <summary>
/// Represents an approval request from the server to the client.
/// </summary>
public sealed class ApprovalRequest
{
    /// <summary>
    /// Gets or sets the unique identifier for this approval request.
    /// </summary>
    [JsonPropertyName("approval_id")]
    public required string ApprovalId { get; init; }

    /// <summary>
    /// Gets or sets the name of the function requiring approval.
    /// </summary>
    [JsonPropertyName("function_name")]
    public required string FunctionName { get; init; }

    /// <summary>
    /// Gets or sets the arguments passed to the function.
    /// </summary>
    [JsonPropertyName("function_arguments")]
    public JsonElement? FunctionArguments { get; init; }

    /// <summary>
    /// Gets or sets the message to display to the user.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
/// Represents an approval response from the client to the server.
/// </summary>
public sealed class ApprovalResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for this approval request.
    /// </summary>
    [JsonPropertyName("approval_id")]
    public required string ApprovalId { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the function call was approved.
    /// </summary>
    [JsonPropertyName("approved")]
    public required bool Approved { get; init; }
}
