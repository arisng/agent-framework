using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using AGUIDojoServer.Api;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AGUIDojoServer.Multimodal;

/// <summary>
/// Agent wrapper that resolves file attachment markers in user messages
/// and injects the actual binary image data as <see cref="DataContent"/>
/// so the LLM receives proper multimodal content.
/// </summary>
/// <remarks>
/// Attachment markers are embedded in user message text by the client in the format:
/// <c>&lt;!-- file:ID:FILENAME:CONTENTTYPE --&gt;</c>
/// This agent strips the markers from the text and adds the corresponding
/// file data from <see cref="IFileStorageService"/> as <see cref="DataContent"/> entries.
/// </remarks>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by ChatClientAgentFactory.CreateUnifiedAgent")]
internal sealed partial class MultimodalAttachmentAgent : DelegatingAIAgent
{
    private readonly IFileStorageService _fileStorage;

    public MultimodalAttachmentAgent(AIAgent innerAgent, IFileStorageService fileStorage)
        : base(innerAgent)
    {
        _fileStorage = fileStorage;
    }

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return RunCoreStreamingAsync(messages, session, options, cancellationToken)
            .ToAgentResponseAsync(cancellationToken);
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ChatMessage> messageList = messages.ToList();
        ResolveAttachments(messageList);

        await foreach (AgentResponseUpdate update in InnerAgent
            .RunStreamingAsync(messageList, session, options, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return update;
        }
    }

    private void ResolveAttachments(List<ChatMessage> messages)
    {
        foreach (ChatMessage message in messages)
        {
            if (message.Role != ChatRole.User)
            {
                continue;
            }

            string? text = message.Text;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            MatchCollection matches = AttachmentMarkerRegex().Matches(text);
            if (matches.Count == 0)
            {
                continue;
            }

            string cleanText = AttachmentMarkerRegex().Replace(text, "").TrimEnd();
            List<string> missingAttachments = [];

            foreach (Match match in matches)
            {
                string fileId = match.Groups[1].Value;
                string fileName = match.Groups[2].Value;

                FileData? fileData = _fileStorage.Get(fileId);
                if (fileData is not null)
                {
                    message.Contents.Add(new DataContent(fileData.Data.ToArray(), fileData.ContentType) { Name = fileData.FileName });
                }
                else
                {
                    missingAttachments.Add(fileName);
                }
            }

            string resolvedText = missingAttachments.Count == 0
                ? cleanText
                : AppendMissingAttachmentNotice(cleanText, missingAttachments);
            TextContent? textContent = message.Contents.OfType<TextContent>().FirstOrDefault();
            if (textContent is not null)
            {
                textContent.Text = resolvedText;
            }
            else if (!string.IsNullOrWhiteSpace(resolvedText))
            {
                message.Contents.Insert(0, new TextContent(resolvedText));
            }
        }
    }

    private static string AppendMissingAttachmentNotice(string visibleText, List<string> missingAttachments)
    {
        var builder = new StringBuilder(visibleText);
        if (builder.Length > 0)
        {
            builder.AppendLine().AppendLine();
        }

        builder.Append(missingAttachments.Count == 1
            ? $"[Image attachment unavailable: {missingAttachments[0]}]"
            : $"[Image attachments unavailable: {string.Join(", ", missingAttachments)}]");

        return builder.ToString();
    }

    [GeneratedRegex(@"<!-- file:([a-f0-9]+):([^:]+):([^ ]+) -->", RegexOptions.Compiled)]
    private static partial Regex AttachmentMarkerRegex();
}
