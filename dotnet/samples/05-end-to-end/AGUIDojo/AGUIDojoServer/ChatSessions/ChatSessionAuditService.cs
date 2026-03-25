using System.Text.Json;
using AGUIDojoServer.Data;
using Microsoft.EntityFrameworkCore;

namespace AGUIDojoServer.ChatSessions;

public sealed class ChatSessionAuditService(ChatSessionsDbContext db)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AppendAsync(
        string sessionId,
        ChatAuditEventType eventType,
        string title,
        string? summary = null,
        object? data = null,
        DateTimeOffset? occurredAt = null,
        string? relatedNodeId = null,
        string? correlationId = null,
        string? eventId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        string resolvedEventId = string.IsNullOrWhiteSpace(eventId) ? Guid.NewGuid().ToString("N") : eventId;
        ChatSessionAuditEvent? existing = await db.ChatSessionAuditEvents.FirstOrDefaultAsync(
            item => item.Id == resolvedEventId,
            ct);

        string? dataJson = data is null ? null : JsonSerializer.Serialize(data, s_jsonOptions);

        if (existing is null)
        {
            db.ChatSessionAuditEvents.Add(new ChatSessionAuditEvent
            {
                Id = resolvedEventId,
                SessionId = sessionId,
                EventType = eventType,
                Title = title,
                Summary = summary,
                DataJson = dataJson,
                OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
                RelatedNodeId = relatedNodeId,
                CorrelationId = correlationId,
            });
        }
        else
        {
            existing.EventType = eventType;
            existing.Title = title;
            existing.Summary = summary;
            existing.DataJson = dataJson;
            existing.OccurredAt = occurredAt ?? existing.OccurredAt;
            existing.RelatedNodeId = relatedNodeId ?? existing.RelatedNodeId;
            existing.CorrelationId = correlationId ?? existing.CorrelationId;
        }

        await db.SaveChangesAsync(ct);
    }
}
