// Copyright (c) Microsoft. All rights reserved.

using AGUIDojoServer.Services;

namespace AGUIDojoServer.Api;

/// <summary>
/// Request body for sending an email.
/// </summary>
/// <param name="To">The email address of the recipient.</param>
/// <param name="Subject">The subject line of the email.</param>
/// <param name="Body">The body content of the email.</param>
public sealed record EmailRequest(string To, string Subject, string Body);

/// <summary>
/// Response from email send operation.
/// </summary>
/// <param name="Message">The result message from the email operation.</param>
/// <param name="Success">Indicates whether the email was sent successfully.</param>
public sealed record EmailResponse(string Message, bool Success);

/// <summary>
/// Minimal API endpoints for email operations.
/// </summary>
/// <remarks>
/// These endpoints use the same <see cref="IEmailService"/> as AI Tools,
/// demonstrating shared business services between REST API and agentic workflows.
/// </remarks>
internal static class EmailEndpoints
{
    /// <summary>
    /// Maps email API endpoints to the application.
    /// </summary>
    /// <param name="group">The route group builder for the /api prefix.</param>
    /// <returns>The route group builder with email endpoints mapped.</returns>
    public static RouteGroupBuilder MapEmailEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/email", SendEmailAsync)
            .WithName("SendEmail")
            .WithSummary("Sends an email to a recipient")
            .WithDescription("Sends an email with the specified recipient, subject, and body content.")
            .Produces<EmailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
            .RequireAuthorization();

        return group;
    }

    /// <summary>
    /// Sends an email to the specified recipient.
    /// </summary>
    /// <param name="request">The email request containing recipient, subject, and body.</param>
    /// <param name="emailService">The email service injected via DI.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An <see cref="EmailResponse"/> indicating the result of the operation.</returns>
    private static async Task<IResult> SendEmailAsync(
        EmailRequest request,
        IEmailService emailService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.To))
        {
            return TypedResults.BadRequest(new EmailResponse("Recipient email address is required.", Success: false));
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            return TypedResults.BadRequest(new EmailResponse("Email subject is required.", Success: false));
        }

        string message = await emailService.SendEmailAsync(request.To, request.Subject, request.Body ?? string.Empty, cancellationToken);
        return TypedResults.Ok(new EmailResponse(message, Success: true));
    }
}
