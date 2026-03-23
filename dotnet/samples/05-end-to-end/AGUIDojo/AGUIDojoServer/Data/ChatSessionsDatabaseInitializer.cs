using System.Data.Common;
using Microsoft.Data.Sqlite;
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

        await EnsureSqliteColumnAsync(db, "ChatSessions", "SubjectEntityType", "TEXT", cancellationToken);
        await EnsureSqliteColumnAsync(
            db,
            "ChatSessions",
            "ServerProtocolVersion",
            $"TEXT NOT NULL DEFAULT '{ChatSessionProtocolVersions.Current}'",
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_ChatSessions_Status" ON "ChatSessions" ("Status");
            CREATE INDEX IF NOT EXISTS "IX_ChatSessions_LastActivityAt" ON "ChatSessions" ("LastActivityAt");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ChatSessions_AguiThreadId" ON "ChatSessions" ("AguiThreadId");
            """,
            cancellationToken);
    }

    private static async Task EnsureSqliteColumnAsync(
        ChatSessionsDbContext db,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        if (await SqliteColumnExistsAsync(db, tableName, columnName, cancellationToken))
        {
            return;
        }

        string quotedTableName = QuoteSqliteIdentifier(tableName);
        string quotedColumnName = QuoteSqliteIdentifier(columnName);
        try
        {
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE {quotedTableName} ADD COLUMN {quotedColumnName} {columnDefinition};",
                cancellationToken);
#pragma warning restore EF1002
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 &&
                                         ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // Another initializer instance added the column after the existence check.
        }
    }

    private static async Task<bool> SqliteColumnExistsAsync(
        ChatSessionsDbContext db,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        DbConnection connection = db.Database.GetDbConnection();
        bool shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
#pragma warning disable CA2100
            command.CommandText = $"PRAGMA table_info({QuoteSqliteIdentifier(tableName)});";
#pragma warning restore CA2100
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string QuoteSqliteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) ||
            identifier.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
        {
            throw new InvalidOperationException($"Unsupported SQLite identifier '{identifier}'.");
        }

        return $"\"{identifier}\"";
    }
}
