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

    public DbSet<ChatConversationNode> ChatConversationNodes => Set<ChatConversationNode>();

    public DbSet<ChatSessionApprovalRecord> ChatSessionApprovalRecords => Set<ChatSessionApprovalRecord>();

    public DbSet<ChatSessionAuditEvent> ChatSessionAuditEvents => Set<ChatSessionAuditEvent>();

    public DbSet<ChatSessionWorkspaceSnapshot> ChatSessionWorkspaceSnapshots => Set<ChatSessionWorkspaceSnapshot>();

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
            entity.Property(e => e.RootMessageId).HasMaxLength(32);
            entity.Property(e => e.ActiveLeafMessageId).HasMaxLength(32);
            entity.Property(e => e.SubjectModule).HasMaxLength(64);
            entity.Property(e => e.SubjectEntityType).HasMaxLength(64);
            entity.Property(e => e.SubjectEntityId).HasMaxLength(128);
            entity.Property(e => e.OwnerId).HasMaxLength(128);
            entity.Property(e => e.TenantId).HasMaxLength(128);
            entity.Property(e => e.WorkflowInstanceId).HasMaxLength(128);
            entity.Property(e => e.RuntimeInstanceId).HasMaxLength(128);
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
            entity.HasIndex(e => new { e.OwnerId, e.Status, e.LastActivityAt });
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => e.AguiThreadId).IsUnique();
        });

        modelBuilder.Entity<ChatConversationNode>(entity =>
        {
            entity.HasKey(e => new { e.SessionId, e.NodeId });
            entity.Property(e => e.SessionId).HasMaxLength(32);
            entity.Property(e => e.NodeId).HasMaxLength(32);
            entity.Property(e => e.ParentNodeId).HasMaxLength(32);
            entity.Property(e => e.RuntimeMessageId).HasMaxLength(128);
            entity.Property(e => e.Role).HasMaxLength(32);
            entity.Property(e => e.AuthorName).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasConversion(dtoConverter);

            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => new { e.SessionId, e.ParentNodeId, e.SiblingOrder }).IsUnique();

            entity.HasOne<ChatSession>()
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<ChatSessionApprovalRecord>(entity =>
        {
            entity.HasKey(e => new { e.SessionId, e.ApprovalId });
            entity.Property(e => e.SessionId).HasMaxLength(32);
            entity.Property(e => e.ApprovalId).HasMaxLength(128);
            entity.Property(e => e.FunctionName).HasMaxLength(256);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.RequestNodeId).HasMaxLength(32);
            entity.Property(e => e.ResponseNodeId).HasMaxLength(32);
            entity.Property(e => e.ResolutionSource).HasMaxLength(64);
            entity.Property(e => e.ConcurrencyStamp)
                .HasMaxLength(32)
                .IsConcurrencyToken();
            entity.Property(e => e.RequestedAt).HasConversion(dtoConverter);
            entity.Property(e => e.ResolvedAt).HasConversion(nullableDtoConverter);

            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => new { e.SessionId, e.Status });

            entity.HasOne<ChatSession>()
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatSessionAuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.SessionId).HasMaxLength(32);
            entity.Property(e => e.EventType).HasConversion<string>().HasMaxLength(64);
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.RelatedNodeId).HasMaxLength(32);
            entity.Property(e => e.CorrelationId).HasMaxLength(128);
            entity.Property(e => e.OccurredAt).HasConversion(dtoConverter);

            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => new { e.SessionId, e.OccurredAt });
            entity.HasIndex(e => new { e.SessionId, e.EventType });

            entity.HasOne<ChatSession>()
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatSessionWorkspaceSnapshot>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.Property(e => e.SessionId).HasMaxLength(32);
            entity.Property(e => e.Source).HasMaxLength(64);
            entity.Property(e => e.ConcurrencyStamp)
                .HasMaxLength(32)
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasConversion(dtoConverter);

            entity.HasOne<ChatSession>()
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
