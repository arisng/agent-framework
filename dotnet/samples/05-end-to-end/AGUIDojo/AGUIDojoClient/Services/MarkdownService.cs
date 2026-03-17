// Copyright (c) Microsoft. All rights reserved.

using Markdig;

namespace AGUIDojoClient.Services;

/// <summary>
/// Service for converting Markdown text to HTML.
/// </summary>
public sealed class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownService"/> class.
    /// </summary>
    public MarkdownService()
    {
        this._pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoLinks()
            .UseTaskLists()
            .UseEmojiAndSmiley()
            .Build();
    }

    /// <summary>
    /// Converts Markdown text to sanitized HTML.
    /// </summary>
    /// <param name="markdown">The Markdown text to convert.</param>
    /// <returns>The HTML representation of the Markdown, or the original text if null/empty.</returns>
    public string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return Markdown.ToHtml(markdown, this._pipeline);
    }
}
