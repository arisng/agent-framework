using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using AGUIDojoClient.Models;
using AGUIDojoClient.Shared;
using Microsoft.Extensions.AI;
using Microsoft.JSInterop;

namespace AGUIDojoClient.Services;

/// <summary>
/// Abstracts browser-based persistence (localStorage + IndexedDB) for session state.
/// </summary>
public interface ISessionPersistenceService
{
    Task SaveMetadataAsync(IEnumerable<SessionMetadataDto> metadata);
    Task<List<SessionMetadataDto>?> LoadMetadataAsync();
    Task SaveActiveSessionIdAsync(string sessionId);
    Task<string?> LoadActiveSessionIdAsync();
    Task SaveConversationAsync(string sessionId, ConversationTree tree);
    Task<ConversationTree?> LoadConversationAsync(string sessionId);
    Task DeleteConversationAsync(string sessionId);
}

/// <summary>
/// Lightweight DTO for persisting session metadata to localStorage.
/// </summary>
public sealed record SessionMetadataDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("endpointPath")] string EndpointPath,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] long CreatedAt,
    [property: JsonPropertyName("lastActivityAt")] long LastActivityAt,
    [property: JsonPropertyName("aguiThreadId")] string? AguiThreadId = null,
    [property: JsonPropertyName("serverSessionId")] string? ServerSessionId = null)
{
    public static SessionMetadataDto FromMetadata(SessionMetadata m) => new(
        m.Id,
        m.Title,
        m.EndpointPath,
        m.Status.ToString(),
        m.CreatedAt.ToUnixTimeMilliseconds(),
        m.LastActivityAt.ToUnixTimeMilliseconds(),
        m.AguiThreadId,
        m.ServerSessionId);

    public SessionMetadata ToMetadata() => new()
    {
        Id = Id,
        Title = Title,
        EndpointPath = EndpointPath,
        Status = Enum.TryParse<SessionStatus>(Status, ignoreCase: true, out var s) ? s : SessionStatus.Completed,
        CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt),
        LastActivityAt = DateTimeOffset.FromUnixTimeMilliseconds(LastActivityAt),
        AguiThreadId = string.IsNullOrWhiteSpace(AguiThreadId) ? SessionMetadata.CreateAguiThreadId() : AguiThreadId,
        ServerSessionId = ServerSessionId,
    };
}

/// <summary>
/// Serializable DTO for a single conversation node.
/// Handles ChatMessage → plain text + role conversion for safe persistence.
/// </summary>
internal sealed record ConversationNodeDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("parentId")] string? ParentId,
    [property: JsonPropertyName("childIds")] List<string> ChildIds,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("authorName")] string? AuthorName,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("additionalProperties")] Dictionary<string, JsonElement>? AdditionalProperties,
    [property: JsonPropertyName("createdAt")] long CreatedAt)
{
    internal static ConversationNodeDto FromNode(ConversationNode node) => new(
        node.Id,
        node.ParentId,
        [.. node.ChildIds],
        node.Message.Role.Value,
        node.Message.AuthorName,
        node.Message.Text,
        SerializeAdditionalProperties(node.Message.AdditionalProperties),
        node.CreatedAt.ToUnixTimeMilliseconds());

    internal ConversationNode ToNode() => new()
    {
        Id = Id,
        ParentId = ParentId,
        Message = CreateMessage(),
        ChildIds = [.. ChildIds],
        CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt),
    };

    private static Dictionary<string, JsonElement>? SerializeAdditionalProperties(AdditionalPropertiesDictionary? additionalProperties)
    {
        if (additionalProperties is null || additionalProperties.Count == 0)
        {
            return null;
        }

        Dictionary<string, JsonElement> serialized = new(StringComparer.Ordinal);
        foreach ((string key, object? value) in additionalProperties)
        {
            if (value is null)
            {
                continue;
            }

            serialized[key] = value is JsonElement jsonElement
                ? jsonElement.Clone()
                : JsonSerializer.SerializeToElement(value, JsonDefaults.Options);
        }

        return serialized.Count == 0 ? null : serialized;
    }

    private ChatMessage CreateMessage()
    {
        ChatMessage message = new(new ChatRole(Role), Text) { AuthorName = AuthorName };

        if (AdditionalProperties is { Count: > 0 })
        {
            message.AdditionalProperties = new AdditionalPropertiesDictionary();
            foreach ((string key, JsonElement value) in AdditionalProperties)
            {
                message.AdditionalProperties[key] = value.Clone();
            }
        }

        return message;
    }
}

/// <summary>
/// Serializable DTO for an entire conversation tree.
/// </summary>
internal sealed record ConversationTreeDto(
    [property: JsonPropertyName("rootId")] string? RootId,
    [property: JsonPropertyName("activeLeafId")] string? ActiveLeafId,
    [property: JsonPropertyName("nodes")] List<ConversationNodeDto> Nodes)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static ConversationTreeDto FromTree(ConversationTree tree) => new(
        tree.RootId,
        tree.ActiveLeafId,
        [.. tree.Nodes.Values.Select(ConversationNodeDto.FromNode)]);

    internal ConversationTree ToTree()
    {
        var nodes = Nodes
            .Select(dto => dto.ToNode())
            .ToImmutableDictionary(n => n.Id);

        return new ConversationTree
        {
            RootId = RootId,
            ActiveLeafId = ActiveLeafId,
            Nodes = nodes,
        };
    }

    internal string Serialize() => JsonSerializer.Serialize(this, s_jsonOptions);

    internal static ConversationTreeDto? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<ConversationTreeDto>(json, s_jsonOptions); }
        catch { return null; }
    }
}

/// <summary>
/// JS interop service bridging Fluxor state to browser storage via sessionPersistence.js.
/// </summary>
public sealed class SessionPersistenceService : ISessionPersistenceService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IJSRuntime _js;

    public SessionPersistenceService(IJSRuntime js) => _js = js;

    public async Task SaveMetadataAsync(IEnumerable<SessionMetadataDto> metadata)
    {
        try
        {
            string json = JsonSerializer.Serialize(metadata, s_jsonOptions);
            await _js.InvokeVoidAsync("sessionPersistence.saveMetadata", json);
        }
        catch (JSDisconnectedException) { }
        catch (Exception ex) { Console.Error.WriteLine($"[SessionPersistence] SaveMetadata failed: {ex.Message}"); }
    }

    public async Task<List<SessionMetadataDto>?> LoadMetadataAsync()
    {
        try
        {
            string? json = await _js.InvokeAsync<string?>("sessionPersistence.loadMetadata");
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<List<SessionMetadataDto>>(json, s_jsonOptions);
        }
        catch (JSDisconnectedException) { return null; }
        catch { return null; }
    }

    public async Task SaveActiveSessionIdAsync(string sessionId)
    {
        try { await _js.InvokeVoidAsync("sessionPersistence.saveActiveSessionId", sessionId); }
        catch (JSDisconnectedException) { }
        catch (Exception ex) { Console.Error.WriteLine($"[SessionPersistence] SaveActiveSessionId failed: {ex.Message}"); }
    }

    public async Task<string?> LoadActiveSessionIdAsync()
    {
        try { return await _js.InvokeAsync<string?>("sessionPersistence.loadActiveSessionId"); }
        catch (JSDisconnectedException) { return null; }
        catch { return null; }
    }

    public async Task SaveConversationAsync(string sessionId, ConversationTree tree)
    {
        try
        {
            string treeJson = ConversationTreeDto.FromTree(tree).Serialize();
            await _js.InvokeVoidAsync("sessionPersistence.saveConversation", sessionId, treeJson);
        }
        catch (JSDisconnectedException) { }
        catch (Exception ex) { Console.Error.WriteLine($"[SessionPersistence] SaveConversation failed: {ex.Message}"); }
    }

    public async Task<ConversationTree?> LoadConversationAsync(string sessionId)
    {
        try
        {
            string? json = await _js.InvokeAsync<string?>("sessionPersistence.loadConversation", sessionId);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return ConversationTreeDto.Deserialize(json)?.ToTree();
        }
        catch (JSDisconnectedException) { return null; }
        catch { return null; }
    }

    public async Task DeleteConversationAsync(string sessionId)
    {
        try { await _js.InvokeVoidAsync("sessionPersistence.deleteConversation", sessionId); }
        catch (JSDisconnectedException) { }
        catch (Exception ex) { Console.Error.WriteLine($"[SessionPersistence] DeleteConversation failed: {ex.Message}"); }
    }
}
