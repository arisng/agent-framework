// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using AGUIDojoServer.Services;

namespace AGUIDojoServer.Tools;

/// <summary>
/// DI-compatible AI Tool wrapper for email operations.
/// </summary>
/// <remarks>
/// <para>
/// This class is designed to be registered as a Singleton in the DI container,
/// as AI Tools are registered as KeyedSingleton by the Agent Framework.
/// </para>
/// <para>
/// To access scoped services (like <see cref="IEmailService"/>), the tool
/// uses <see cref="IHttpContextAccessor"/> to resolve the service from
/// <see cref="HttpContext.RequestServices"/> at execution time.
/// </para>
/// </remarks>
public sealed class EmailTool
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailTool"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor for resolving scoped services.</param>
    public EmailTool(IHttpContextAccessor httpContextAccessor)
    {
        this._httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Sends an email to a recipient.
    /// </summary>
    /// <param name="to">The email address of the recipient.</param>
    /// <param name="subject">The subject line of the email.</param>
    /// <param name="body">The body content of the email.</param>
    /// <returns>A message indicating the result of the email operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when HttpContext is not available or service cannot be resolved.</exception>
    [Description("Send an email to a recipient.")]
    public async Task<string> SendEmailAsync(
        [Description("The email address of the recipient.")] string to,
        [Description("The subject line of the email.")] string subject,
        [Description("The body content of the email.")] string body)
    {
        var httpContext = this._httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available. This tool must be called within an HTTP request context.");

        var emailService = httpContext.RequestServices.GetRequiredService<IEmailService>();
        return await emailService.SendEmailAsync(to, subject, body, httpContext.RequestAborted);
    }
}
