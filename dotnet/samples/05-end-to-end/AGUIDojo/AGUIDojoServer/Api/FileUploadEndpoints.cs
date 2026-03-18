using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AGUIDojoServer.Api;

/// <summary>
/// In-memory file storage service for multimodal attachments.
/// Stores uploaded files with automatic expiration after 1 hour.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Stores a file and returns its metadata.
    /// </summary>
    FileData Store(string fileName, string contentType, byte[] data);

    /// <summary>
    /// Retrieves a stored file by ID, or null if not found or expired.
    /// </summary>
    FileData? Get(string id);
}

/// <summary>
/// Metadata for a stored file.
/// </summary>
public sealed record FileData(string Id, string FileName, string ContentType, ReadOnlyMemory<byte> Data, DateTimeOffset UploadedAt);

/// <summary>
/// Response payload from file upload.
/// </summary>
internal sealed record FileUploadResponse(string Id, string FileName, string ContentType, long Size);

/// <summary>
/// Thread-safe in-memory file storage with automatic expiration.
/// Registered as Singleton in DI.
/// </summary>
public sealed class InMemoryFileStorageService : IFileStorageService
{
    private static readonly TimeSpan Expiration = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, FileData> _files = new();
    private int _cleanupCounter;

    /// <inheritdoc />
    public FileData Store(string fileName, string contentType, byte[] data)
    {
        string id = Guid.NewGuid().ToString("N");
        var fileData = new FileData(id, fileName, contentType, data, DateTimeOffset.UtcNow);
        _files[id] = fileData;

        // Periodic cleanup every 10 uploads
        if (Interlocked.Increment(ref _cleanupCounter) % 10 == 0)
        {
            CleanupExpired();
        }

        return fileData;
    }

    /// <inheritdoc />
    public FileData? Get(string id) =>
        _files.TryGetValue(id, out FileData? file) && file.UploadedAt + Expiration > DateTimeOffset.UtcNow
            ? file
            : null;

    private void CleanupExpired()
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - Expiration;
        foreach (var kvp in _files)
        {
            if (kvp.Value.UploadedAt < cutoff)
            {
                _files.TryRemove(kvp.Key, out _);
            }
        }
    }
}

/// <summary>
/// Minimal API endpoints for file upload and retrieval.
/// </summary>
internal static class FileUploadEndpoints
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
    };

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Maps file upload and retrieval endpoints to the /api group.
    /// </summary>
    public static RouteGroupBuilder MapFileEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/files", UploadFileAsync)
            .WithName("UploadFile")
            .WithSummary("Upload a file attachment for multimodal chat")
            .Produces<FileUploadResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .DisableAntiforgery();

        group.MapGet("/files/{id}", GetFile)
            .WithName("GetFile")
            .WithSummary("Retrieve a previously uploaded file");

        return group;
    }

    private static async Task<IResult> UploadFileAsync(HttpRequest request, IFileStorageService storage)
    {
        if (!request.HasFormContentType)
        {
            return TypedResults.BadRequest("Expected multipart/form-data.");
        }

        IFormCollection form = await request.ReadFormAsync();
        IFormFile? file = form.Files.Count > 0 ? form.Files[0] : null;

        if (file is null || file.Length == 0)
        {
            return TypedResults.BadRequest("No file provided.");
        }

        if (file.Length > MaxFileSize)
        {
            return TypedResults.BadRequest($"File too large. Maximum size is {MaxFileSize / 1024 / 1024} MB.");
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return TypedResults.BadRequest($"Content type '{file.ContentType}' is not supported. Allowed: {string.Join(", ", AllowedContentTypes)}.");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        FileData stored = storage.Store(file.FileName, file.ContentType, ms.ToArray());

        return TypedResults.Ok(new FileUploadResponse(stored.Id, stored.FileName, stored.ContentType, stored.Data.Length));
    }

    private static FileContentHttpResult GetFile(string id, IFileStorageService storage)
    {
        FileData? file = storage.Get(id);
        if (file is null)
        {
            return TypedResults.File(Encoding.UTF8.GetBytes(MissingAttachmentSvg), "image/svg+xml");
        }

        return TypedResults.File(file.Data.ToArray(), contentType: file.ContentType, enableRangeProcessing: true);
    }

    private const string MissingAttachmentSvg =
        """
        <svg xmlns="http://www.w3.org/2000/svg" width="320" height="180" viewBox="0 0 320 180">
          <rect width="320" height="180" fill="#f3f4f6"/>
          <rect x="16" y="16" width="288" height="148" rx="16" fill="#ffffff" stroke="#d1d5db"/>
          <text x="160" y="78" text-anchor="middle" font-family="Arial, sans-serif" font-size="18" fill="#111827">Attachment unavailable</text>
          <text x="160" y="106" text-anchor="middle" font-family="Arial, sans-serif" font-size="13" fill="#6b7280">This image expired after the server restarted.</text>
        </svg>
        """;
}
