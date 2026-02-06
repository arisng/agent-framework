// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoServer.Services;

/// <summary>
/// Provides document writing functionality.
/// </summary>
/// <remarks>
/// This service is shared between Minimal API endpoints and AI Tools,
/// enabling consistent document operations across the application.
/// </remarks>
public interface IDocumentService
{
    /// <summary>
    /// Writes a document with the specified content.
    /// </summary>
    /// <param name="document">The document content to write, typically in markdown format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A message indicating the result of the document operation.</returns>
    Task<string> WriteDocumentAsync(string document, CancellationToken cancellationToken = default);
}
