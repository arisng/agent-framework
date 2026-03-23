using AGUIDojoServer.Data;
using Microsoft.EntityFrameworkCore;

namespace AGUIDojoServer.Api;

/// <summary>
/// Durable file storage backed by the AGUIDojo SQLite database.
/// </summary>
public sealed class DatabaseFileStorageService : IFileStorageService
{
    private const int CleanupIntervalUploads = 10;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private static int s_cleanupCounter;

    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseFileStorageService(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public FileData Store(string fileName, string contentType, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(data);

        string id = Guid.NewGuid().ToString("N");
        DateTimeOffset uploadedAt = DateTimeOffset.UtcNow;

        using IServiceScope scope = _scopeFactory.CreateScope();
        ChatSessionsDbContext db = scope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>();

        db.ChatAttachments.Add(new ChatAttachment
        {
            Id = id,
            FileName = fileName,
            ContentType = contentType,
            Data = data,
            Size = data.LongLength,
            UploadedAt = uploadedAt,
            ExpiresAt = uploadedAt + Retention
        });

        if (Interlocked.Increment(ref s_cleanupCounter) % CleanupIntervalUploads == 0)
        {
            db.ChatAttachments
                .Where(attachment => attachment.ExpiresAt <= uploadedAt)
                .ExecuteDelete();
        }

        db.SaveChanges();

        return new FileData(id, fileName, contentType, data, uploadedAt);
    }

    public FileData? Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        ChatSessionsDbContext db = scope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        ChatAttachment? attachment = db.ChatAttachments
            .AsNoTracking()
            .SingleOrDefault(attachment => attachment.Id == id);

        if (attachment is null)
        {
            return null;
        }

        if (attachment.ExpiresAt <= now)
        {
            db.ChatAttachments
                .Where(candidate => candidate.Id == id)
                .ExecuteDelete();

            return null;
        }

        return new FileData(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.Data,
            attachment.UploadedAt);
    }
}
