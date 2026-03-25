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

        group.MapGet("/chat-sessions/{id}", async (
            string id,
            ChatSessionService service,
            CancellationToken ct) =>
        {
            var session = await service.GetSessionAsync(id, ct);
            return session is not null ? Results.Ok(session) : Results.NotFound();
        })
        .WithName("GetChatSession")
        .WithDescription("Gets detail for a specific chat session with thin workspace summary counts.")
        .Produces<ChatSessionDetail>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/chat-sessions/{id}/workspace", async (string id, ChatSessionWorkspaceService workspaceService, CancellationToken ct) =>
        {
            ChatSessionWorkspaceDto? workspace = await workspaceService.GetWorkspaceAsync(id, ct);
            return workspace is not null ? Results.Ok(workspace) : Results.NotFound();
        })
        .WithName("GetChatSessionWorkspace")
        .WithDescription("Gets the current durable workspace projection, approvals, audit entries, and file references for a chat session.")
        .Produces<ChatSessionWorkspaceDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/chat-sessions/{id}/workspace/import", async (
            string id,
            ChatSessionWorkspaceImportRequest request,
            ChatSessionWorkspaceService workspaceService,
            CancellationToken ct) =>
        {
            try
            {
                await workspaceService.ImportWorkspaceAsync(id, request, ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("was not found.", StringComparison.Ordinal))
            {
                return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Chat session not found", detail: ex.Message);
            }
        })
        .WithName("ImportChatSessionWorkspace")
        .WithDescription("Imports best-effort browser-local workspace state into the durable server-owned projection for a chat session.")
        .Accepts<ChatSessionWorkspaceImportRequest>("application/json")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/chat-sessions/{id}/conversation", async (string id, ChatConversationService conversationService, CancellationToken ct) =>
        {
            ChatConversationGraph? conversation = await conversationService.GetConversationAsync(id, ct);
            return conversation is not null ? Results.Ok(conversation) : Results.NotFound();
        })
        .WithName("GetChatSessionConversation")
        .WithDescription("Gets the canonical branching conversation graph for a specific chat session.")
        .Produces<ChatConversationGraph>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/chat-sessions/{id}/active-leaf", async (
            string id,
            ChatConversationActiveLeafUpdate request,
            ChatConversationService conversationService,
            CancellationToken ct) =>
        {
            try
            {
                bool updated = await conversationService.SetActiveLeafAsync(id, request.ActiveLeafId, ct);
                return updated ? Results.NoContent() : Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid active leaf", detail: ex.Message);
            }
        })
        .WithName("SetChatSessionActiveLeaf")
        .WithDescription("Sets the active branch leaf for a specific chat session.")
        .Accepts<ChatConversationActiveLeafUpdate>("application/json")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/chat-sessions/{id}/conversation", async (
            string id,
            ChatConversationService conversationService,
            CancellationToken ct) =>
        {
            bool cleared = await conversationService.ClearConversationAsync(id, ct);
            return cleared ? Results.NoContent() : Results.NotFound();
        })
        .WithName("ClearChatSessionConversation")
        .WithDescription("Clears the canonical conversation graph and derived workspace state for a specific chat session.")
        .Produces(StatusCodes.Status204NoContent)
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
