// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoServer.Services;

/// <summary>
/// Default implementation of <see cref="IDocumentService"/> that provides simulated document functionality.
/// </summary>
/// <remarks>
/// This implementation simulates document writing and returns a success message.
/// The actual document content is tracked via AG-UI state updates in the PredictiveStateUpdatesAgent.
/// In a production scenario, this would integrate with a document storage system.
/// </remarks>
public sealed class DocumentService : IDocumentService
{
    /// <inheritdoc/>
    public Task<string> WriteDocumentAsync(string document, CancellationToken cancellationToken = default)
    {
        // Simply return success - the document is tracked via state updates in the agent layer
        // In a production scenario, this would save the document to a storage system
        return Task.FromResult("Document written successfully");
    }
}
