// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace AGUIDojoServer.Api;

/// <summary>
/// Minimal API endpoints for LLM-powered session title generation.
/// </summary>
internal static class TitleEndpoints
{
    /// <summary>
    /// Maps title generation endpoints to the application.
    /// </summary>
    /// <param name="group">The route group builder for the /api prefix.</param>
    /// <returns>The route group builder with title endpoints mapped.</returns>
    public static RouteGroupBuilder MapTitleEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/title", GenerateTitleAsync)
            .WithName("GenerateTitle")
            .WithSummary("Generates a session title from conversation messages")
            .WithDescription("Uses an LLM to generate a concise 4-8 word title summarizing the conversation topic.")
            .Produces<TitleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// Generates a concise session title from the first few conversation messages.
    /// </summary>
    /// <param name="request">The title generation request containing conversation messages.</param>
    /// <param name="chatClient">The chat client for LLM inference.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="TitleResponse"/> containing the generated title.</returns>
    private static async Task<IResult> GenerateTitleAsync(
        TitleRequest request,
        IChatClient chatClient,
        CancellationToken cancellationToken)
    {
        if (request.Messages is not { Count: > 0 })
        {
            return TypedResults.BadRequest("At least one message is required.");
        }

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "Generate a concise 4-8 word title summarizing the conversation topic. " +
                "Return ONLY the title text — no quotes, no punctuation, no explanation.")
        };

        // Include up to the first 4 messages (2 turns) for context
        foreach (TitleMessage msg in request.Messages.Take(4))
        {
            ChatRole role = string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase)
                ? ChatRole.User
                : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, msg.Content));
        }

        ChatResponse response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        string title = response.Text?.Trim().Trim('"', '.', '!') ?? "Untitled Chat";

        // Enforce reasonable length
        if (title.Length > 60)
        {
            title = string.Concat(title.AsSpan(0, 57), "...");
        }

        return TypedResults.Ok(new TitleResponse(title));
    }
}

/// <summary>Request payload for title generation.</summary>
/// <param name="Messages">The conversation messages to generate a title from.</param>
internal sealed record TitleRequest(List<TitleMessage> Messages);

/// <summary>A single message in the title generation request.</summary>
/// <param name="Role">The message role ("user" or "assistant").</param>
/// <param name="Content">The message content text.</param>
internal sealed record TitleMessage(string Role, string Content);

/// <summary>Response payload containing the generated title.</summary>
/// <param name="Title">The generated session title.</param>
internal sealed record TitleResponse(string Title);
