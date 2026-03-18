using System.Text;
using System.Text.RegularExpressions;

namespace AGUIDojoClient.Helpers;

/// <summary>
/// Parses and formats hidden attachment markers embedded in user chat message text.
/// </summary>
public static partial class MessageAttachmentMarkers
{
    /// <summary>
    /// Removes attachment markers from the supplied message text.
    /// </summary>
    /// <param name="text">The raw message text containing zero or more attachment markers.</param>
    /// <returns>The user-visible message text without attachment markers.</returns>
    public static string Strip(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return AttachmentMarkerRegex().Replace(text, "").TrimEnd();
    }

    /// <summary>
    /// Extracts attachment markers from the supplied message text.
    /// </summary>
    /// <param name="text">The raw message text containing zero or more attachment markers.</param>
    /// <returns>The parsed attachment markers in message order.</returns>
    public static List<MessageAttachmentMarker> Extract(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        MatchCollection matches = AttachmentMarkerRegex().Matches(text);
        if (matches.Count == 0)
        {
            return [];
        }

        var result = new List<MessageAttachmentMarker>(matches.Count);
        foreach (Match match in matches)
        {
            result.Add(new MessageAttachmentMarker(
                match.Groups[1].Value,
                match.Groups[2].Value,
                match.Groups[3].Value));
        }

        return result;
    }

    /// <summary>
    /// Rebuilds raw message text by appending attachment markers after visible text.
    /// </summary>
    /// <param name="text">The visible user-entered message text.</param>
    /// <param name="attachments">The attachments to encode into hidden markers.</param>
    /// <returns>A message payload suitable for persistence and server-side attachment resolution.</returns>
    public static string Append(string? text, IEnumerable<MessageAttachmentMarker> attachments)
    {
        string visibleText = text?.Trim() ?? string.Empty;
        var builder = new StringBuilder(visibleText);

        foreach (MessageAttachmentMarker attachment in attachments)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("<!-- file:")
                .Append(attachment.Id)
                .Append(':')
                .Append(attachment.FileName)
                .Append(':')
                .Append(attachment.ContentType)
                .Append(" -->");
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"<!-- file:([a-f0-9]+):([^:]+):([^ ]+) -->", RegexOptions.Compiled)]
    private static partial Regex AttachmentMarkerRegex();
}

/// <summary>
/// Represents a parsed hidden attachment marker stored inside a user message.
/// </summary>
/// <param name="Id">The uploaded file identifier.</param>
/// <param name="FileName">The original file name.</param>
/// <param name="ContentType">The uploaded MIME content type.</param>
public sealed record MessageAttachmentMarker(string Id, string FileName, string ContentType);
