// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoServer.Services;

/// <summary>
/// Provides email sending functionality.
/// </summary>
/// <remarks>
/// This service is shared between Minimal API endpoints and AI Tools,
/// enabling consistent email operations across the application.
/// </remarks>
public interface IEmailService
{
    /// <summary>
    /// Sends an email to the specified recipient.
    /// </summary>
    /// <param name="to">The email address of the recipient.</param>
    /// <param name="subject">The subject line of the email.</param>
    /// <param name="body">The body content of the email.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A message indicating the result of the email operation.</returns>
    Task<string> SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
