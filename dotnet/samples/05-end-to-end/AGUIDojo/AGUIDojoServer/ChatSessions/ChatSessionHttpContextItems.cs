using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace AGUIDojoServer.ChatSessions;

internal sealed record ChatSessionRoutingContext
{
    public required string SessionId { get; init; }

    public required string ServerProtocolVersion { get; init; }

    public string? OwnerId { get; init; }

    public string? TenantId { get; init; }

    public string? SubjectModule { get; init; }

    public string? SubjectEntityType { get; init; }

    public string? SubjectEntityId { get; init; }

    public string? WorkflowInstanceId { get; init; }

    public string? RuntimeInstanceId { get; init; }

    public string? AguiThreadId { get; init; }

    public string? PreferredModelId { get; init; }
}

internal static class ChatSessionHttpContextItems
{
    public const string SessionId = "aguidojo.chatSessionId";

    public const string RoutingContext = "aguidojo.chatSessionRoutingContext";

    public static void SetRoutingContext(HttpContext context, ChatSessionRoutingContext routingContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(routingContext);

        context.Items[SessionId] = routingContext.SessionId;
        context.Items[RoutingContext] = routingContext;
    }

    public static bool TryGetRoutingContext(
        HttpContext? context,
        [NotNullWhen(true)] out ChatSessionRoutingContext? routingContext)
    {
        if (context?.Items.TryGetValue(RoutingContext, out object? rawRoutingContext) == true &&
            rawRoutingContext is ChatSessionRoutingContext typedRoutingContext)
        {
            routingContext = typedRoutingContext;
            return true;
        }

        routingContext = null;
        return false;
    }

    public static bool TryGetSessionId(HttpContext? context, [NotNullWhen(true)] out string? sessionId)
    {
        if (TryGetRoutingContext(context, out ChatSessionRoutingContext? routingContext) &&
            !string.IsNullOrWhiteSpace(routingContext.SessionId))
        {
            sessionId = routingContext.SessionId;
            return true;
        }

        if (context?.Items.TryGetValue(SessionId, out object? rawSessionId) == true &&
            rawSessionId is string typedSessionId &&
            !string.IsNullOrWhiteSpace(typedSessionId))
        {
            sessionId = typedSessionId;
            return true;
        }

        sessionId = null;
        return false;
    }
}
