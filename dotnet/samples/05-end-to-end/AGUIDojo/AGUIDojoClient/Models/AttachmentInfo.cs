namespace AGUIDojoClient.Models;

/// <summary>
/// Metadata for a file attachment uploaded to the server.
/// Used by <c>ChatInput</c> to track pending attachments before sending.
/// </summary>
/// <param name="Id">Server-assigned file identifier.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME content type (e.g., image/png).</param>
/// <param name="Size">File size in bytes.</param>
public sealed record AttachmentInfo(string Id, string FileName, string ContentType, long Size);
