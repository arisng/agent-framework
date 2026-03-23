using AGUIDojoServer.Data;
using Microsoft.EntityFrameworkCore;

namespace AGUIDojoServer.ChatSessions;

/// <summary>
/// Service for managing chat session lifecycle.
/// </summary>
public sealed class ChatSessionService(ChatSessionsDbContext db)
{
    /// <summary>Lists active sessions, ordered by most recent activity.</summary>
    public async Task<List<ChatSessionSummary>> ListSessionsAsync(CancellationToken ct = default)
    {
        return await db.ChatSessions
            .Where(s => s.Status == ChatSessionStatus.Active)
            .OrderByDescending(s => s.LastActivityAt)
            .Select(s => new ChatSessionSummary
            {
                Id = s.Id,
                Title = s.Title,
                Status = s.Status.ToString(),
                CreatedAt = s.CreatedAt,
                LastActivityAt = s.LastActivityAt,
                SubjectModule = s.SubjectModule,
                SubjectEntityId = s.SubjectEntityId,
                PreferredModelId = s.PreferredModelId,
                AguiThreadId = s.AguiThreadId,
            })
            .ToListAsync(ct);
    }

    /// <summary>Gets a session by ID.</summary>
    public async Task<ChatSessionDetail?> GetSessionAsync(string id, CancellationToken ct = default)
    {
        var session = await db.ChatSessions.FindAsync([id], ct);
        if (session is null)
        {
            return null;
        }

        return new ChatSessionDetail
        {
            Id = session.Id,
            Title = session.Title,
            Status = session.Status.ToString(),
            CreatedAt = session.CreatedAt,
            LastActivityAt = session.LastActivityAt,
            ArchivedAt = session.ArchivedAt,
            SubjectModule = session.SubjectModule,
            SubjectEntityId = session.SubjectEntityId,
            AguiThreadId = session.AguiThreadId,
            PreferredModelId = session.PreferredModelId,
        };
    }

    /// <summary>Archives a session by setting its status and timestamp.</summary>
    public async Task<bool> ArchiveSessionAsync(string id, CancellationToken ct = default)
    {
        var session = await db.ChatSessions.FindAsync([id], ct);
        if (session is null)
        {
            return false;
        }

        session.Status = ChatSessionStatus.Archived;
        session.ArchivedAt = DateTimeOffset.UtcNow;
        session.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Ensures a server-owned session exists for the given AG-UI thread ID.
    /// Creates one if none exists, updates LastActivityAt if one does.
    /// Returns the server session ID.
    /// </summary>
    public async Task<string> EnsureSessionForThreadAsync(string aguiThreadId, CancellationToken ct = default)
    {
        var session = await db.ChatSessions
            .FirstOrDefaultAsync(s => s.AguiThreadId == aguiThreadId, ct);

        if (session is not null)
        {
            session.LastActivityAt = DateTimeOffset.UtcNow;
            session.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync(ct);
            return session.Id;
        }

        session = new ChatSession
        {
            AguiThreadId = aguiThreadId,
        };
        db.ChatSessions.Add(session);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique constraint violation — another request already created a session for
            // this threadId. Detach the failed entity, look up the winner, and update it.
            db.Entry(session).State = EntityState.Detached;
            session = await db.ChatSessions
                .FirstAsync(s => s.AguiThreadId == aguiThreadId, ct);
            session.LastActivityAt = DateTimeOffset.UtcNow;
            session.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync(ct);
        }

        return session.Id;
    }
}
