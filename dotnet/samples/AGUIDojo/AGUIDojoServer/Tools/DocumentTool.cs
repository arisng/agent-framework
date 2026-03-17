// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using AGUIDojoServer.Services;

namespace AGUIDojoServer.Tools;

/// <summary>
/// DI-compatible AI Tool wrapper for document operations.
/// </summary>
/// <remarks>
/// <para>
/// This class is designed to be registered as a Singleton in the DI container,
/// as AI Tools are registered as KeyedSingleton by the Agent Framework.
/// </para>
/// <para>
/// To access scoped services (like <see cref="IDocumentService"/>), the tool
/// uses <see cref="IHttpContextAccessor"/> to resolve the service from
/// <see cref="HttpContext.RequestServices"/> at execution time.
/// </para>
/// </remarks>
public sealed class DocumentTool
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentTool"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor for resolving scoped services.</param>
    public DocumentTool(IHttpContextAccessor httpContextAccessor)
    {
        this._httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Writes a document in markdown format.
    /// </summary>
    /// <param name="document">The document content to write.</param>
    /// <returns>A message indicating the result of the document operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when HttpContext is not available or service cannot be resolved.</exception>
    [Description("Write a document in markdown format.")]
    public async Task<string> WriteDocumentAsync(
        [Description("The document content to write.")] string document)
    {
        var httpContext = this._httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available. This tool must be called within an HTTP request context.");

        var documentService = httpContext.RequestServices.GetRequiredService<IDocumentService>();
        return await documentService.WriteDocumentAsync(document, httpContext.RequestAborted);
    }
}
