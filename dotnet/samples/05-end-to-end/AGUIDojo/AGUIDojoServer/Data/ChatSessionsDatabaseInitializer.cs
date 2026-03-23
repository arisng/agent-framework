using Microsoft.EntityFrameworkCore;

namespace AGUIDojoServer.Data;

/// <summary>
/// Ensures the AGUIDojo chat-session database contains the tables required by the current server version.
/// </summary>
public static class ChatSessionsDatabaseInitializer
{
    public static async Task InitializeAsync(ChatSessionsDbContext db, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!db.Database.IsSqlite())
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "ChatAttachments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ChatAttachments" PRIMARY KEY,
                "FileName" TEXT NOT NULL,
                "ContentType" TEXT NOT NULL,
                "Data" BLOB NOT NULL,
                "Size" INTEGER NOT NULL,
                "UploadedAt" TEXT NOT NULL,
                "ExpiresAt" TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_ChatAttachments_ExpiresAt" ON "ChatAttachments" ("ExpiresAt");
            """,
            cancellationToken);
    }
}
