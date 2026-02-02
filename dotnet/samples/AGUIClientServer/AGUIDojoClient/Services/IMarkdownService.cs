// Copyright (c) Microsoft. All rights reserved.

namespace AGUIDojoClient.Services;

/// <summary>
/// Interface for Markdown to HTML conversion service.
/// </summary>
public interface IMarkdownService
{
    /// <summary>
    /// Converts Markdown text to HTML.
    /// </summary>
    /// <param name="markdown">The Markdown text to convert.</param>
    /// <returns>The HTML representation of the Markdown.</returns>
    string ToHtml(string? markdown);
}
