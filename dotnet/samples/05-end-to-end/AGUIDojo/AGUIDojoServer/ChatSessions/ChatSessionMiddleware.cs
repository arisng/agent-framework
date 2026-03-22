// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;

namespace AGUIDojoServer.ChatSessions;

/// <summary>
/// Middleware that ensures a server-owned session exists for each /chat request.
/// Creates an implicit session on first turn using the AG-UI thread ID.
/// </summary>
public sealed class ChatSessionMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Intercepts chat requests and ensures the backing server session exists.
    /// Reads threadId from the AG-UI JSON request body (RunAgentInput.threadId).
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ChatSessionService sessionService)
    {
        if (context.Request.Path.StartsWithSegments("/chat", StringComparison.OrdinalIgnoreCase) &&
            HttpMethods.IsPost(context.Request.Method))
        {
            // AG-UI sends threadId inside the JSON request body (RunAgentInput).
            // Buffer the body so MapAGUI can still read it downstream.
            context.Request.EnableBuffering();

            string? threadId = await ExtractThreadIdAsync(context.Request.Body, context.RequestAborted);

            // Rewind the body for the next handler.
            context.Request.Body.Position = 0;

            if (!string.IsNullOrEmpty(threadId))
            {
                context.Response.Headers["X-Session-Id"] =
                    await sessionService.EnsureSessionForThreadAsync(threadId, context.RequestAborted);
            }
        }

        await next(context);
    }

    private static async Task<string?> ExtractThreadIdAsync(Stream body, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("threadId", out JsonElement threadIdElement))
            {
                return threadIdElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Malformed body — let the downstream handler deal with it.
        }

        return null;
    }
}
