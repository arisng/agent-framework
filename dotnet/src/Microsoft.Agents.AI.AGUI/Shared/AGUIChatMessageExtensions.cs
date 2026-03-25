// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.AI;

#if ASPNETCORE
namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Shared;
#else
namespace Microsoft.Agents.AI.AGUI.Shared;
#endif

internal static class AGUIChatMessageExtensions
{
    private static readonly ChatRole s_developerChatRole = new("developer");
    internal const string ServerToolReplayMarkerKey = "agui_server_tool_call";

    public static IEnumerable<ChatMessage> AsChatMessages(
        this IEnumerable<AGUIMessage> aguiMessages,
        JsonSerializerOptions jsonSerializerOptions)
    {
        foreach (var message in aguiMessages)
        {
            var role = MapChatRole(message.Role);

            switch (message)
            {
                case AGUIToolMessage toolMessage:
                {
                    object? result;
                    if (string.IsNullOrEmpty(toolMessage.Content))
                    {
                        result = toolMessage.Content;
                    }
                    else
                    {
                        // Try to deserialize as JSON, but fall back to string if it fails
                        try
                        {
                            result = JsonSerializer.Deserialize(toolMessage.Content, AGUIJsonSerializerContext.Default.JsonElement);
                        }
                        catch (JsonException)
                        {
                            result = toolMessage.Content;
                        }
                    }

                    yield return new ChatMessage(
                        role,
                        [
                            new FunctionResultContent(
                                    toolMessage.ToolCallId,
                                    result)
                        ]);
                    break;
                }

                case AGUIAssistantMessage assistantMessage when assistantMessage.ToolCalls is { Length: > 0 }:
                {
                    var contents = new List<AIContent>();

                    if (!string.IsNullOrEmpty(assistantMessage.Content))
                    {
                        contents.Add(new TextContent(assistantMessage.Content));
                    }

                    // Add tool calls
                    foreach (var toolCall in assistantMessage.ToolCalls)
                    {
                        Dictionary<string, object?>? arguments = null;
                        if (!string.IsNullOrEmpty(toolCall.Function.Arguments))
                        {
                            arguments = (Dictionary<string, object?>?)JsonSerializer.Deserialize(
                                toolCall.Function.Arguments,
                                jsonSerializerOptions.GetTypeInfo(typeof(Dictionary<string, object?>)));
                        }

                        contents.Add(new FunctionCallContent(
                            toolCall.Id,
                            toolCall.Function.Name,
                            arguments));
                    }

                    yield return new ChatMessage(role, contents)
                    {
                        MessageId = message.Id
                    };
                    break;
                }

                default:
                {
                    string content = message switch
                    {
                        AGUIDeveloperMessage dev => dev.Content,
                        AGUISystemMessage sys => sys.Content,
                        AGUIUserMessage user => user.Content,
                        AGUIAssistantMessage asst => asst.Content,
                        _ => string.Empty
                    };

                    yield return new ChatMessage(role, content)
                    {
                        MessageId = message.Id
                    };
                    break;
                }
            }
        }
    }

    public static IEnumerable<AGUIMessage> AsAGUIMessages(
        this IEnumerable<ChatMessage> chatMessages,
        JsonSerializerOptions jsonSerializerOptions)
    {
        PendingAssistantToolCallTurn? pendingAssistantTurn = null;
        HashSet<string> suppressedToolCallIds = [];

        foreach (var message in chatMessages)
        {
            message.MessageId ??= Guid.NewGuid().ToString("N");
            if (message.Role == ChatRole.Tool)
            {
                List<AGUIToolMessage> toolMessages = [.. MapToolMessages(jsonSerializerOptions, message, excludedToolCallIds: suppressedToolCallIds)];
                if (pendingAssistantTurn is null)
                {
                    foreach (var toolMessage in toolMessages)
                    {
                        if (string.IsNullOrEmpty(toolMessage.Content))
                        {
                            continue;
                        }

                        yield return toolMessage;
                    }

                    continue;
                }

                List<AGUIToolMessage> unmatchedToolMessages = [];
                foreach (var toolMessage in toolMessages)
                {
                    if (!pendingAssistantTurn.TryAddToolMessage(toolMessage))
                    {
                        unmatchedToolMessages.Add(toolMessage);
                    }
                }

                if (pendingAssistantTurn.IsComplete)
                {
                    foreach (var pendingMessage in pendingAssistantTurn.Emit())
                    {
                        yield return pendingMessage;
                    }

                    pendingAssistantTurn = null;
                }

                foreach (var toolMessage in unmatchedToolMessages)
                {
                    yield return toolMessage;
                }

                continue;
            }

            if (pendingAssistantTurn is not null)
            {
                suppressedToolCallIds.UnionWith(pendingAssistantTurn.RemainingToolCallIds);
                foreach (var pendingMessage in pendingAssistantTurn.Emit())
                {
                    yield return pendingMessage;
                }

                pendingAssistantTurn = null;
            }

            if (message.Role == ChatRole.Assistant)
            {
                var assistantMessage = MapAssistantMessage(jsonSerializerOptions, message);
                HashSet<string> toolCallIds = [.. message.Contents
                    .OfType<FunctionCallContent>()
                    .Where(content => !IsServerToolReplayContent(content))
                    .Select(content => content.CallId)
                    .Where(callId => !string.IsNullOrWhiteSpace(callId))!];

                if (toolCallIds.Count == 0)
                {
                    if (assistantMessage != null)
                    {
                        yield return assistantMessage;
                    }

                    continue;
                }

                pendingAssistantTurn = new PendingAssistantToolCallTurn(assistantMessage, toolCallIds);
                pendingAssistantTurn.AddToolMessages(MapToolMessages(jsonSerializerOptions, message, pendingAssistantTurn.RemainingToolCallIds));

                if (!pendingAssistantTurn.IsComplete)
                {
                    continue;
                }

                foreach (var pendingMessage in pendingAssistantTurn.Emit())
                {
                    yield return pendingMessage;
                }

                pendingAssistantTurn = null;
                continue;
            }

            yield return message.Role.Value switch
            {
                AGUIRoles.Developer => new AGUIDeveloperMessage { Id = message.MessageId, Content = message.Text ?? string.Empty },
                AGUIRoles.System => new AGUISystemMessage { Id = message.MessageId, Content = message.Text ?? string.Empty },
                AGUIRoles.User => new AGUIUserMessage { Id = message.MessageId, Content = message.Text ?? string.Empty },
                _ => throw new InvalidOperationException($"Unknown role: {message.Role.Value}")
            };
        }

        if (pendingAssistantTurn is not null)
        {
            foreach (var pendingMessage in pendingAssistantTurn.Emit(includeUnmatchedAssistantToolCalls: true))
            {
                yield return pendingMessage;
            }
        }
    }

    private static AGUIAssistantMessage? MapAssistantMessage(JsonSerializerOptions jsonSerializerOptions, ChatMessage message)
    {
        List<AGUIToolCall>? toolCalls = null;
        string? textContent = null;

        foreach (var content in message.Contents)
        {
            if (content is FunctionCallContent functionCall)
            {
                if (IsServerToolReplayContent(functionCall))
                {
                    continue;
                }

                var argumentsJson = functionCall.Arguments is null ?
                    "{}" :
                    JsonSerializer.Serialize(functionCall.Arguments, jsonSerializerOptions.GetTypeInfo(typeof(IDictionary<string, object?>)));
                toolCalls ??= [];
                toolCalls.Add(new AGUIToolCall
                {
                    Id = functionCall.CallId,
                    Type = "function",
                    Function = new AGUIFunctionCall
                    {
                        Name = functionCall.Name,
                        Arguments = argumentsJson
                    }
                });
            }
            else if (content is TextContent textContentItem)
            {
                textContent = textContentItem.Text;
            }
        }

        // Create message with tool calls and/or text content
        if (toolCalls?.Count > 0 || !string.IsNullOrEmpty(textContent))
        {
            return new AGUIAssistantMessage
            {
                Id = message.MessageId,
                Content = textContent ?? string.Empty,
                ToolCalls = toolCalls?.Count > 0 ? toolCalls.ToArray() : null
            };
        }

        return null;
    }

    private static IEnumerable<AGUIToolMessage> MapToolMessages(
        JsonSerializerOptions jsonSerializerOptions,
        ChatMessage message,
        HashSet<string>? allowedToolCallIds = null,
        HashSet<string>? excludedToolCallIds = null)
    {
        foreach (var content in message.Contents)
        {
            if (content is FunctionResultContent functionResult)
            {
                if (IsServerToolReplayContent(functionResult))
                {
                    continue;
                }

                if (excludedToolCallIds is not null &&
                    !string.IsNullOrWhiteSpace(functionResult.CallId) &&
                    excludedToolCallIds.Contains(functionResult.CallId))
                {
                    continue;
                }

                if (allowedToolCallIds is not null &&
                    (string.IsNullOrWhiteSpace(functionResult.CallId) || !allowedToolCallIds.Contains(functionResult.CallId)))
                {
                    continue;
                }

                yield return new AGUIToolMessage
                {
                    Id = functionResult.CallId,
                    ToolCallId = functionResult.CallId,
                    Content = functionResult.Result is null ?
                        string.Empty :
                        JsonSerializer.Serialize(functionResult.Result, jsonSerializerOptions.GetTypeInfo(functionResult.Result.GetType()))
                };
            }
        }
    }

    public static ChatRole MapChatRole(string role) =>
        string.Equals(role, AGUIRoles.System, StringComparison.OrdinalIgnoreCase) ? ChatRole.System :
        string.Equals(role, AGUIRoles.User, StringComparison.OrdinalIgnoreCase) ? ChatRole.User :
        string.Equals(role, AGUIRoles.Assistant, StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant :
        string.Equals(role, AGUIRoles.Developer, StringComparison.OrdinalIgnoreCase) ? s_developerChatRole :
        string.Equals(role, AGUIRoles.Tool, StringComparison.OrdinalIgnoreCase) ? ChatRole.Tool :
        throw new InvalidOperationException($"Unknown chat role: {role}");

    private static bool IsServerToolReplayContent(AIContent content)
    {
        if (content.AdditionalProperties is null ||
            !content.AdditionalProperties.TryGetValue(ServerToolReplayMarkerKey, out object? marker))
        {
            return false;
        }

        return marker switch
        {
            true => true,
            string s when bool.TryParse(s, out bool value) => value,
            JsonElement jsonElement when jsonElement.ValueKind is JsonValueKind.True => true,
            _ => false
        };
    }

    private sealed class PendingAssistantToolCallTurn
    {
        private readonly AGUIAssistantMessage? _assistantMessage;
        private readonly List<AGUIToolMessage> _toolMessages = [];
        private readonly List<string> _matchedToolCallIds = [];

        public PendingAssistantToolCallTurn(AGUIAssistantMessage? assistantMessage, HashSet<string> remainingToolCallIds)
        {
            _assistantMessage = assistantMessage;
            RemainingToolCallIds = remainingToolCallIds;
        }

        public HashSet<string> RemainingToolCallIds { get; }

        public bool IsComplete => RemainingToolCallIds.Count == 0;

        public bool TryAddToolMessage(AGUIToolMessage toolMessage)
        {
            if (!RemainingToolCallIds.Remove(toolMessage.ToolCallId))
            {
                return false;
            }

            _matchedToolCallIds.Add(toolMessage.ToolCallId);
            _toolMessages.Add(toolMessage);
            return true;
        }

        public void AddToolMessages(IEnumerable<AGUIToolMessage> toolMessages)
        {
            foreach (var toolMessage in toolMessages)
            {
                _ = TryAddToolMessage(toolMessage);
            }
        }

        public IEnumerable<AGUIMessage> Emit(bool includeUnmatchedAssistantToolCalls = false)
        {
            AGUIAssistantMessage? assistantMessage = BuildAssistantMessage(includeUnmatchedAssistantToolCalls);
            if (assistantMessage is not null)
            {
                yield return assistantMessage;
            }

            foreach (var toolMessage in _toolMessages)
            {
                yield return toolMessage;
            }
        }

        private AGUIAssistantMessage? BuildAssistantMessage(bool includeUnmatchedAssistantToolCalls)
        {
            if (_assistantMessage is null)
            {
                return null;
            }

            if (includeUnmatchedAssistantToolCalls && _matchedToolCallIds.Count == 0)
            {
                return _assistantMessage;
            }

            AGUIToolCall[]? toolCalls = null;
            if (_assistantMessage.ToolCalls is { Length: > 0 } && _matchedToolCallIds.Count > 0)
            {
                HashSet<string> matchedToolCallIds = [.. _matchedToolCallIds];
                toolCalls = [.. _assistantMessage.ToolCalls.Where(toolCall => matchedToolCallIds.Contains(toolCall.Id))];
            }

            if (toolCalls is null && string.IsNullOrEmpty(_assistantMessage.Content))
            {
                return null;
            }

            if ((toolCalls?.Length ?? 0) == (_assistantMessage.ToolCalls?.Length ?? 0))
            {
                return _assistantMessage;
            }

            return new AGUIAssistantMessage
            {
                Id = _assistantMessage.Id,
                Content = _assistantMessage.Content,
                ToolCalls = toolCalls
            };
        }
    }
}
