using AGUIDojoServer.ChatSessions;

namespace AGUIDojoServer.Api;

/// <summary>
/// Minimal API endpoints for chat session lifecycle.
/// </summary>
public static class ChatSessionEndpoints
{
    /// <summary>
    /// Maps the /api/chat-sessions endpoints to the route group.
    /// </summary>
    public static RouteGroupBuilder MapChatSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/chat-sessions", async (ChatSessionService service, CancellationToken ct) =>
        {
            var sessions = await service.ListSessionsAsync(ct);
            return Results.Ok(sessions);
        })
        .WithName("ListChatSessions")
        .WithDescription("Lists active chat sessions ordered by most recent activity.")
        .Produces<List<ChatSessionSummary>>(StatusCodes.Status200OK);

        group.MapGet("/chat-sessions/{id}", async (string id, ChatSessionService service, CancellationToken ct) =>
        {
            var session = await service.GetSessionAsync(id, ct);
            return session is not null ? Results.Ok(session) : Results.NotFound();
        })
        .WithName("GetChatSession")
        .WithDescription("Gets detail for a specific chat session.")
        .Produces<ChatSessionDetail>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/chat-sessions/{id}/archive", async (string id, ChatSessionService service, CancellationToken ct) =>
        {
            bool archived = await service.ArchiveSessionAsync(id, ct);
            return archived ? Results.NoContent() : Results.NotFound();
        })
        .WithName("ArchiveChatSession")
        .WithDescription("Archives a chat session.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}
