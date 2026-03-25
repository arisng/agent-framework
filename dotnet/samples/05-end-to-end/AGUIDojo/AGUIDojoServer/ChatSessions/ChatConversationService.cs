using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using AGUIDojoServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using ChatConversationEntity = AGUIDojoServer.Data.ChatConversationNode;

namespace AGUIDojoServer.ChatSessions;

/// <summary>
/// Persists and projects the canonical server-owned branching conversation graph.
/// </summary>
internal sealed class ChatConversationService(ChatSessionsDbContext db, ChatSessionWorkspaceService workspaceService)
{
    private const string RootParentKey = "<root>";
    private const int MaxConcurrencyRetries = 2;

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Gets the persisted conversation graph for a session.</summary>
    public async Task<ChatConversationGraph?> GetConversationAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        ChatSession? session = await db.ChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null)
        {
            return null;
        }

        List<ChatConversationEntity> nodes = await LoadNodesAsync(sessionId, ct);
        return BuildConversation(session, nodes);
    }

    /// <summary>
    /// Upserts an ordered active-branch message path into the canonical graph and updates the active leaf.
    /// </summary>
    public async Task<ChatConversationGraph> PersistConversationAsync(
        string sessionId,
        IEnumerable<ChatMessage> orderedMessages,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(orderedMessages);

        List<ChatMessage> orderedPath = orderedMessages.ToList();

        for (int attempt = 0; ; attempt++)
        {
            db.ChangeTracker.Clear();

            try
            {
                return await PersistConversationOnceAsync(sessionId, orderedPath, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Another request updated the session row between load and save.
                // Rebuild from the latest persisted graph and retry.
            }
        }
    }

    /// <summary>Updates the active leaf to an existing persisted node.</summary>
    public async Task<bool> SetActiveLeafAsync(string sessionId, string activeLeafId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(activeLeafId);

        ChatSession? session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return false;
        }

        bool nodeExists = await db.ChatConversationNodes.AnyAsync(
            node => node.SessionId == sessionId && node.NodeId == activeLeafId,
            ct);
        if (!nodeExists)
        {
            throw new ArgumentException(
                $"Conversation node '{activeLeafId}' does not belong to session '{sessionId}'.",
                nameof(activeLeafId));
        }

        session.ActiveLeafMessageId = activeLeafId;
        session.LastActivityAt = DateTimeOffset.UtcNow;
        session.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        await workspaceService.RefreshDerivedStateAsync(sessionId, ct);
        return true;
    }

    private async Task<ChatConversationGraph> PersistConversationOnceAsync(
        string sessionId,
        IReadOnlyList<ChatMessage> orderedPath,
        CancellationToken ct)
    {
        ChatSession session = await db.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException($"Chat session '{sessionId}' was not found.");

        List<ChatConversationEntity> nodes = await db.ChatConversationNodes
            .Where(node => node.SessionId == sessionId)
            .OrderBy(node => node.CreatedAt)
            .ThenBy(node => node.SiblingOrder)
            .ToListAsync(ct);

        var childrenByParent = nodes
            .GroupBy(node => NormalizeParentKey(node.ParentNodeId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(node => node.SiblingOrder).ToList(),
                StringComparer.Ordinal);

        string? currentParentId = null;
        string? rootId = session.RootMessageId;

        foreach (ChatMessage message in orderedPath)
        {
            StoredChatMessage storedMessage = StoredChatMessage.From(message);
            string parentKey = NormalizeParentKey(currentParentId);

            if (!childrenByParent.TryGetValue(parentKey, out List<ChatConversationEntity>? siblings))
            {
                siblings = [];
                childrenByParent[parentKey] = siblings;
            }

            ChatConversationEntity? matchingNode = siblings.FirstOrDefault(node => StoredChatMessage.Matches(node, storedMessage));
            if (matchingNode is null)
            {
                matchingNode = new ChatConversationEntity
                {
                    SessionId = sessionId,
                    NodeId = Guid.NewGuid().ToString("N"),
                    ParentNodeId = currentParentId,
                    SiblingOrder = siblings.Count,
                    RuntimeMessageId = storedMessage.MessageId,
                    Role = storedMessage.Role,
                    AuthorName = storedMessage.AuthorName,
                    Text = storedMessage.Text,
                    ContentJson = storedMessage.ContentJson,
                    AdditionalPropertiesJson = storedMessage.AdditionalPropertiesJson,
                    CreatedAt = storedMessage.CreatedAt,
                };

                db.ChatConversationNodes.Add(matchingNode);
                nodes.Add(matchingNode);
                siblings.Add(matchingNode);
            }
            else if (string.IsNullOrWhiteSpace(matchingNode.RuntimeMessageId) &&
                     !string.IsNullOrWhiteSpace(storedMessage.MessageId))
            {
                matchingNode.RuntimeMessageId = storedMessage.MessageId;
            }

            string? normalizedNodeContentJson = NormalizeContentJson(matchingNode.ContentJson);
            if (!string.Equals(matchingNode.ContentJson, normalizedNodeContentJson, StringComparison.Ordinal))
            {
                matchingNode.ContentJson = normalizedNodeContentJson;
            }

            rootId ??= matchingNode.NodeId;
            currentParentId = matchingNode.NodeId;
        }

        session.RootMessageId = rootId;
        session.ActiveLeafMessageId = currentParentId;
        session.LastActivityAt = DateTimeOffset.UtcNow;
        session.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        await db.SaveChangesAsync(ct);
        await workspaceService.RefreshDerivedStateAsync(sessionId, ct);
        return BuildConversation(session, nodes);
    }

    /// <summary>Clears the persisted conversation graph for a session.</summary>
    public async Task<bool> ClearConversationAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        ChatSession? session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
        {
            return false;
        }

        List<ChatConversationEntity> nodes = await db.ChatConversationNodes
            .Where(node => node.SessionId == sessionId)
            .ToListAsync(ct);

        if (nodes.Count > 0)
        {
            db.ChatConversationNodes.RemoveRange(nodes);
        }

        session.RootMessageId = null;
        session.ActiveLeafMessageId = null;
        session.LastActivityAt = DateTimeOffset.UtcNow;
        session.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        await workspaceService.ClearWorkspaceAsync(sessionId, ct);
        return true;
    }

    private async Task<List<ChatConversationEntity>> LoadNodesAsync(string sessionId, CancellationToken ct)
    {
        return await db.ChatConversationNodes
            .AsNoTracking()
            .Where(node => node.SessionId == sessionId)
            .OrderBy(node => node.CreatedAt)
            .ThenBy(node => node.SiblingOrder)
            .ToListAsync(ct);
    }

    private static ChatConversationGraph BuildConversation(ChatSession session, IReadOnlyCollection<ChatConversationEntity> nodes)
    {
        var childIdsByParent = nodes
            .Where(node => node.ParentNodeId is not null)
            .GroupBy(node => node.ParentNodeId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(node => node.SiblingOrder)
                    .Select(node => node.NodeId)
                    .ToList(),
                StringComparer.Ordinal);

        return new ChatConversationGraph
        {
            RootId = session.RootMessageId,
            ActiveLeafId = session.ActiveLeafMessageId,
            Nodes = nodes
                .OrderBy(node => node.CreatedAt)
                .ThenBy(node => node.SiblingOrder)
                .Select(node => new ChatConversationNodeDto
                {
                    Id = node.NodeId,
                    ParentId = node.ParentNodeId,
                    ChildIds = childIdsByParent.TryGetValue(node.NodeId, out List<string>? childIds) ? childIds : [],
                    MessageId = node.RuntimeMessageId,
                    Role = node.Role,
                    AuthorName = node.AuthorName,
                    Text = node.Text,
                    Content = ParseJson(NormalizeContentJson(node.ContentJson)),
                    AdditionalProperties = ParseJson(node.AdditionalPropertiesJson),
                    CreatedAt = node.CreatedAt,
                })
                .ToList(),
        };
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string NormalizeParentKey(string? parentId) => parentId ?? RootParentKey;

    private static string? NormalizeContentJson(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return null;
        }

        try
        {
            List<StoredContentPayload>? payloads = JsonSerializer.Deserialize<List<StoredContentPayload>>(contentJson, s_jsonOptions);
            if (payloads is null || payloads.Count == 0)
            {
                return null;
            }

            List<StoredContentPayload> normalizedPayloads = [];
            HashSet<string> seenFunctionResultCallIds = new(StringComparer.Ordinal);

            foreach (StoredContentPayload payload in payloads)
            {
                if (string.Equals(payload.Type, nameof(FunctionResultContent), StringComparison.Ordinal) &&
                    TryGetFunctionResultCallId(payload.Value, out string? callId) &&
                    !seenFunctionResultCallIds.Add(callId))
                {
                    continue;
                }

                normalizedPayloads.Add(payload);
            }

            return normalizedPayloads.Count == 0
                ? null
                : JsonSerializer.Serialize(normalizedPayloads, s_jsonOptions);
        }
        catch (JsonException)
        {
            return contentJson;
        }
    }

    private static bool TryGetFunctionResultCallId(JsonElement payloadValue, [NotNullWhen(true)] out string? callId)
    {
        callId = null;
        if (payloadValue.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!payloadValue.TryGetProperty("callId", out JsonElement callIdElement) &&
            !payloadValue.TryGetProperty("CallId", out callIdElement))
        {
            return false;
        }

        string? parsedCallId = callIdElement.GetString();
        if (string.IsNullOrWhiteSpace(parsedCallId))
        {
            return false;
        }

        callId = parsedCallId;
        return true;
    }

    private sealed record StoredChatMessage(
        string Role,
        string? AuthorName,
        string? Text,
        string? MessageId,
        string? ContentJson,
        string? AdditionalPropertiesJson,
        DateTimeOffset CreatedAt)
    {
        public static StoredChatMessage From(ChatMessage message) => new(
            message.Role.Value,
            message.AuthorName,
            message.Text,
            message.MessageId,
            SerializeContents(message.Contents),
            SerializeAdditionalProperties(message.AdditionalProperties),
            message.CreatedAt ?? DateTimeOffset.UtcNow);

        public static bool Matches(ChatConversationEntity node, StoredChatMessage message)
        {
            return string.Equals(node.Role, message.Role, StringComparison.Ordinal) &&
                   string.Equals(node.AuthorName, message.AuthorName, StringComparison.Ordinal) &&
                   string.Equals(node.Text, message.Text, StringComparison.Ordinal) &&
                   string.Equals(NormalizeContentJson(node.ContentJson), message.ContentJson, StringComparison.Ordinal) &&
                   string.Equals(node.AdditionalPropertiesJson, message.AdditionalPropertiesJson, StringComparison.Ordinal);
        }

        private static string? SerializeContents(IList<AIContent> contents)
        {
            if (contents.Count == 0)
            {
                return null;
            }

            List<StoredContentPayload> payloads = contents
                .Where(content => content is not FunctionResultContent functionResult ||
                    string.IsNullOrWhiteSpace(functionResult.CallId) ||
                    IsFirstFunctionResult(contents, functionResult))
                .Select(content => new StoredContentPayload(
                    content.GetType().Name,
                    SerializeValue(content)))
                .ToList();

            return JsonSerializer.Serialize(payloads, s_jsonOptions);
        }

        private static string? SerializeAdditionalProperties(AdditionalPropertiesDictionary? additionalProperties)
        {
            if (additionalProperties is null || additionalProperties.Count == 0)
            {
                return null;
            }

            SortedDictionary<string, JsonElement> serialized = new(StringComparer.Ordinal);
            foreach ((string key, object? value) in additionalProperties.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
            {
                serialized[key] = SerializeValue(value);
            }

            return JsonSerializer.Serialize(serialized, s_jsonOptions);
        }

        private static JsonElement SerializeValue(object? value)
        {
            if (value is null)
            {
                return JsonSerializer.SerializeToElement<object?>(null, s_jsonOptions);
            }

            return value switch
            {
                JsonElement jsonElement => jsonElement.Clone(),
                _ => JsonSerializer.SerializeToElement(value, value.GetType(), s_jsonOptions),
            };
        }

        private static bool IsFirstFunctionResult(IList<AIContent> contents, FunctionResultContent functionResult)
        {
            for (int index = 0; index < contents.Count; index++)
            {
                if (!ReferenceEquals(contents[index], functionResult) &&
                    contents[index] is FunctionResultContent otherResult &&
                    string.Equals(otherResult.CallId, functionResult.CallId, StringComparison.Ordinal))
                {
                    return false;
                }

                if (ReferenceEquals(contents[index], functionResult))
                {
                    return true;
                }
            }

            return true;
        }
    }

    private sealed record StoredContentPayload(string Type, JsonElement Value);
}
