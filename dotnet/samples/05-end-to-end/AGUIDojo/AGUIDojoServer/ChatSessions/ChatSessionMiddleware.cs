using System.Text.Json;

namespace AGUIDojoServer.ChatSessions;

/// <summary>
/// Middleware that ensures a server-owned session exists for each /chat request.
/// Creates an implicit session on first turn using the AG-UI thread ID.
/// </summary>
public sealed class ChatSessionMiddleware(RequestDelegate next)
{
    private readonly record struct ChatSessionRequestContext(
        string? ThreadId,
        string? FirstUserMessage,
        string? SubjectModule,
        string? SubjectEntityType,
        string? SubjectEntityId,
        string? PreferredModelId);

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

            ChatSessionRequestContext requestContext = await ExtractRequestContextAsync(context.Request.Body, context.RequestAborted);

            // Rewind the body for the next handler.
            context.Request.Body.Position = 0;

            if (!string.IsNullOrEmpty(requestContext.ThreadId))
            {
                ChatSessionService.ChatSessionEnsureResult sessionResult =
                    await sessionService.EnsureSessionForThreadAsync(
                        new ChatSessionService.ChatSessionEnsureRequest
                        {
                            AguiThreadId = requestContext.ThreadId,
                            FirstUserMessage = requestContext.FirstUserMessage,
                            SubjectModule = requestContext.SubjectModule,
                            SubjectEntityType = requestContext.SubjectEntityType,
                            SubjectEntityId = requestContext.SubjectEntityId,
                            PreferredModelId = requestContext.PreferredModelId,
                        },
                        context.RequestAborted);

                context.Response.Headers["X-Session-Id"] = sessionResult.SessionId;
                context.Response.Headers["X-Chat-Session-Protocol"] = sessionResult.ServerProtocolVersion;
            }
        }

        await next(context);
    }

    private static async Task<ChatSessionRequestContext> ExtractRequestContextAsync(Stream body, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            JsonElement root = doc.RootElement;
            string? threadId = root.TryGetProperty("threadId", out JsonElement threadIdElement)
                ? threadIdElement.GetString()
                : null;
            JsonElement forwardedProps = root.TryGetProperty("forwardedProps", out JsonElement forwardedPropsElement)
                ? forwardedPropsElement
                : default;

            string? firstUserMessage = null;
            if (root.TryGetProperty("messages", out JsonElement messagesElement) &&
                messagesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement messageElement in messagesElement.EnumerateArray())
                {
                    if (!messageElement.TryGetProperty("role", out JsonElement roleElement) ||
                        !string.Equals(roleElement.GetString(), "user", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (messageElement.TryGetProperty("content", out JsonElement contentElement) &&
                        contentElement.ValueKind == JsonValueKind.String)
                    {
                        firstUserMessage = contentElement.GetString();
                        break;
                    }
                }
            }

            return new ChatSessionRequestContext(
                threadId,
                firstUserMessage,
                TryGetStringProperty(forwardedProps, "subjectModule"),
                TryGetStringProperty(forwardedProps, "subjectEntityType"),
                TryGetStringProperty(forwardedProps, "subjectEntityId"),
                TryGetStringProperty(forwardedProps, "preferredModelId"));
        }
        catch (JsonException)
        {
            // Malformed body — let the downstream handler deal with it.
        }

        return default;
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
