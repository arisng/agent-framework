// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;

namespace AGUIWebChat.Client.Services;

public abstract record AGUIUpdate;
public record AGUITextPart(string Text) : AGUIUpdate;
public record AGUIDataSnapshot(string JsonData) : AGUIUpdate;
public record AGUIDataDelta(string JsonData) : AGUIUpdate;
public record AGUIToolCall(FunctionCallContent Content) : AGUIUpdate;
public record AGUIToolResult(FunctionResultContent Content) : AGUIUpdate;
public record AGUIConversationIdUpdate(string ConversationId) : AGUIUpdate;
public record AGUIRawUpdate(ChatResponseUpdate Update) : AGUIUpdate;

public class AGUIProtocolService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<AGUIProtocolService> _logger;

    public AGUIProtocolService(IChatClient chatClient, ILogger<AGUIProtocolService> logger)
    {
        this._chatClient = chatClient;
        this._logger = logger;
    }

    public async IAsyncEnumerable<AGUIUpdate> StreamResponseAsync(
        IList<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in this._chatClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            // Always yield the raw update first so the consumer can maintain history
            yield return new AGUIRawUpdate(update);

            if (!string.IsNullOrEmpty(update.ConversationId))
            {
                yield return new AGUIConversationIdUpdate(update.ConversationId);
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return new AGUITextPart(update.Text);
            }

            foreach (var content in update.Contents)
            {
                if (content is FunctionCallContent call)
                {
                    this._logger.LogInformation("[AG-UI] Tool call received: {Name} (ID: {CallId})", call.Name, call.CallId);
                    yield return new AGUIToolCall(call);
                }
                else if (content is FunctionResultContent result)
                {
                    this._logger.LogInformation("[AG-UI] Tool result received: CallId={CallId}, Result={Result}",
                        result.CallId, result.Result?.ToString()?.Substring(0, Math.Min(200, result.Result?.ToString()?.Length ?? 0)));
                    yield return new AGUIToolResult(result);
                }
                else if (content is DataContent dataContent)
                {
                    if (IsJsonPatchMediaType(dataContent.MediaType))
                    {
                        string text = GetDataContentText(dataContent);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            yield return new AGUIDataDelta(text);
                        }
                    }
                    else if (IsJsonSnapshotMediaType(dataContent.MediaType))
                    {
                        string text = GetDataContentText(dataContent);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            yield return new AGUIDataSnapshot(text);
                        }
                    }
                }
            }
        }
    }

    private static string GetDataContentText(DataContent dataContent)
    {
        // Check for Data (BinaryData)
        if (!dataContent.Data.IsEmpty)
        {
            return Encoding.UTF8.GetString(dataContent.Data.Span);
        }

        return dataContent.Uri ?? string.Empty;
    }

    private static bool IsJsonPatchMediaType(string? mediaType)
    {
        return string.Equals(mediaType, "application/json-patch+json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJsonSnapshotMediaType(string? mediaType)
    {
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);
    }
}
