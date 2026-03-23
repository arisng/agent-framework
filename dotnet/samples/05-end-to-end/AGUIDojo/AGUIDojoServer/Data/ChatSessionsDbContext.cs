using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AGUIDojoServer.Data;

/// <summary>
/// EF Core DbContext for the Chat Sessions module.
/// </summary>
public sealed class ChatSessionsDbContext : DbContext
{
    public ChatSessionsDbContext(DbContextOptions<ChatSessionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();

    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite does not natively support DateTimeOffset; store as ISO 8601 TEXT for
        // correct ordering and portability to SQL Server / PostgreSQL.
        var dtoConverter = new DateTimeOffsetToStringConverter();
        var nullableDtoConverter = new ValueConverter<DateTimeOffset?, string?>(
            v => v.HasValue ? v.Value.ToString("o") : null,
            v => v != null ? DateTimeOffset.Parse(v) : null);

        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(32);
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.SubjectModule).HasMaxLength(64);
            entity.Property(e => e.SubjectEntityType).HasMaxLength(64);
            entity.Property(e => e.SubjectEntityId).HasMaxLength(128);
            entity.Property(e => e.AguiThreadId).HasMaxLength(128);
            entity.Property(e => e.PreferredModelId).HasMaxLength(64);
            entity.Property(e => e.ServerProtocolVersion).HasMaxLength(64);
            entity.Property(e => e.ConcurrencyStamp)
                .HasMaxLength(32)
                .IsConcurrencyToken();

            entity.Property(e => e.CreatedAt).HasConversion(dtoConverter);
            entity.Property(e => e.LastActivityAt).HasConversion(dtoConverter);
            entity.Property(e => e.ArchivedAt).HasConversion(nullableDtoConverter);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.LastActivityAt);
            entity.HasIndex(e => e.AguiThreadId).IsUnique();
        });

        modelBuilder.Entity<ChatAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(32);
            entity.Property(e => e.FileName).HasMaxLength(512);
            entity.Property(e => e.ContentType).HasMaxLength(128);
            entity.Property(e => e.UploadedAt).HasConversion(dtoConverter);
            entity.Property(e => e.ExpiresAt).HasConversion(dtoConverter);

            entity.HasIndex(e => e.ExpiresAt);
        });
    }
}
