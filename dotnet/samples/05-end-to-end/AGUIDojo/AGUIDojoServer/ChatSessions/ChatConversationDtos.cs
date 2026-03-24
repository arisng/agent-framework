using System.Text.Json;

namespace AGUIDojoServer.ChatSessions;

/// <summary>Conversation graph projection used to rehydrate the active branch.</summary>
public sealed class ChatConversationGraph
{
    public string? RootId { get; init; }

    public string? ActiveLeafId { get; init; }

    public required List<ChatConversationNodeDto> Nodes { get; init; }
}

/// <summary>Single node in the server-owned conversation graph.</summary>
public sealed class ChatConversationNodeDto
{
    public required string Id { get; init; }

    public string? ParentId { get; init; }

    public required List<string> ChildIds { get; init; }

    public string? MessageId { get; init; }

    public required string Role { get; init; }

    public string? AuthorName { get; init; }

    public string? Text { get; init; }

    public JsonElement? Content { get; init; }

    public JsonElement? AdditionalProperties { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Request body for updating the active leaf of a persisted conversation.</summary>
public sealed class ChatConversationActiveLeafUpdate
{
    public required string ActiveLeafId { get; init; }
}
