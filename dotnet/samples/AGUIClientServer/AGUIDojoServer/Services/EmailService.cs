// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoServer.Services;

/// <summary>
/// Default implementation of <see cref="IEmailService"/> that provides simulated email functionality.
/// </summary>
/// <remarks>
/// This implementation simulates email sending and returns a success message with recipient details.
/// In a production scenario, this would integrate with an actual email service provider.
/// </remarks>
public sealed class EmailService : IEmailService
{
    /// <inheritdoc/>
    public Task<string> SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        // Simulate sending email
        // In a production scenario, this would call a real email service (SendGrid, Azure Communication Services, etc.)
        string result = $"Email sent successfully to {to} with subject '{subject}'.";
        return Task.FromResult(result);
    }
}
