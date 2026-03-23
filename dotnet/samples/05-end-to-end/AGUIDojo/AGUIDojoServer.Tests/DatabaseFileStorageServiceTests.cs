using AGUIDojoServer.Api;
using AGUIDojoServer.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AGUIDojoServer.Tests;

public sealed class DatabaseFileStorageServiceTests
{
    [Fact]
    public async Task Store_ThenGetAcrossProviderRestart_PreservesAttachment()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using (ServiceProvider firstProvider = CreateServiceProvider(dbPath))
            {
                await InitializeDatabaseAsync(firstProvider);

                IFileStorageService storage = firstProvider.GetRequiredService<IFileStorageService>();
                FileData stored = storage.Store("sunrise.png", "image/png", [1, 2, 3, 4]);

                Assert.Equal("sunrise.png", stored.FileName);
                Assert.Equal("image/png", stored.ContentType);

                using ServiceProvider secondProvider = CreateServiceProvider(dbPath);
                await InitializeDatabaseAsync(secondProvider);

                IFileStorageService restartedStorage = secondProvider.GetRequiredService<IFileStorageService>();
                FileData? restored = restartedStorage.Get(stored.Id);

                Assert.NotNull(restored);
                Assert.Equal(stored.Id, restored.Id);
                Assert.Equal(stored.FileName, restored.FileName);
                Assert.Equal(stored.ContentType, restored.ContentType);
                Assert.Equal(stored.Data.ToArray(), restored.Data.ToArray());
            }
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task InitializeAsync_WithExistingSessionDatabase_AddsChatAttachmentsTable()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            await using (SqliteConnection connection = new($"Data Source={dbPath}"))
            {
                await connection.OpenAsync();
                SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE "ChatSessions" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_ChatSessions" PRIMARY KEY
                    );
                    """;
                await command.ExecuteNonQueryAsync();
            }

            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            await using SqliteConnection verifyConnection = new($"Data Source={dbPath}");
            await verifyConnection.OpenAsync();
            SqliteCommand verifyCommand = verifyConnection.CreateCommand();
            verifyCommand.CommandText =
                """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table' AND name = 'ChatAttachments';
                """;

            long tableCount = (long)(await verifyCommand.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, tableCount);
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Get_WithExpiredAttachment_RemovesAttachmentAndReturnsNull()
    {
        string dbPath = CreateDatabasePath();

        try
        {
            using ServiceProvider provider = CreateServiceProvider(dbPath);
            await InitializeDatabaseAsync(provider);

            using (IServiceScope scope = provider.CreateScope())
            {
                ChatSessionsDbContext db = scope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>();
                db.ChatAttachments.Add(new ChatAttachment
                {
                    Id = "expired-attachment-id",
                    FileName = "expired.png",
                    ContentType = "image/png",
                    Data = [1, 2, 3],
                    Size = 3,
                    UploadedAt = DateTimeOffset.UtcNow.AddDays(-31),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                });
                db.SaveChanges();
            }

            IFileStorageService storage = provider.GetRequiredService<IFileStorageService>();
            FileData? result = storage.Get("expired-attachment-id");

            Assert.Null(result);

            using IServiceScope verificationScope = provider.CreateScope();
            ChatSessionsDbContext verificationDb = verificationScope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>();
            Assert.False(await verificationDb.ChatAttachments.AnyAsync(attachment => attachment.Id == "expired-attachment-id"));
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    private static ServiceProvider CreateServiceProvider(string dbPath)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<ChatSessionsDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddSingleton<IFileStorageService, DatabaseFileStorageService>();
        return services.BuildServiceProvider();
    }

    private static async Task InitializeDatabaseAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        ChatSessionsDbContext db = scope.ServiceProvider.GetRequiredService<ChatSessionsDbContext>();
        await ChatSessionsDatabaseInitializer.InitializeAsync(db);
    }

    private static string CreateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"aguidojo-attachments-{Guid.NewGuid():N}.db");
}
