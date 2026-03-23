using System.Diagnostics.CodeAnalysis;

namespace AGUIDojoServer.Data;

/// <summary>
/// Durable storage record for an uploaded multimodal attachment.
/// </summary>
public sealed class ChatAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core persists attachment payloads as byte arrays.")]
    public byte[] Data { get; set; } = [];

    public long Size { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }
}
