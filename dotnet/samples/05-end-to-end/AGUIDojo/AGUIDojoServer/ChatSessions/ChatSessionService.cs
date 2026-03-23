using AGUIDojoServer.Data;
using Microsoft.EntityFrameworkCore;

namespace AGUIDojoServer.ChatSessions;

/// <summary>
/// Service for managing chat session lifecycle.
/// </summary>
public sealed class ChatSessionService(ChatSessionsDbContext db)
{
    private const int MaxTitleLength = 80;

    public sealed record ChatSessionEnsureRequest
    {
        public required string AguiThreadId { get; init; }
        public string? FirstUserMessage { get; init; }
        public string? SubjectModule { get; init; }
        public string? SubjectEntityType { get; init; }
        public string? SubjectEntityId { get; init; }
        public string? PreferredModelId { get; init; }
        public string ServerProtocolVersion { get; init; } = ChatSessionProtocolVersions.Current;
    }

    public sealed record ChatSessionEnsureResult
    {
        public required string SessionId { get; init; }
        public required string ServerProtocolVersion { get; init; }
    }

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
                SubjectEntityType = s.SubjectEntityType,
                SubjectEntityId = s.SubjectEntityId,
                PreferredModelId = s.PreferredModelId,
                AguiThreadId = s.AguiThreadId,
                ServerProtocolVersion = s.ServerProtocolVersion,
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
            SubjectEntityType = session.SubjectEntityType,
            SubjectEntityId = session.SubjectEntityId,
            AguiThreadId = session.AguiThreadId,
            PreferredModelId = session.PreferredModelId,
            ServerProtocolVersion = session.ServerProtocolVersion,
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
    /// Creates one if none exists, updates LastActivityAt if one does, and backfills
    /// a thin list/detail title from the first user message when the row is still untitled.
    /// Returns the server session ID.
    /// </summary>
    public async Task<string> EnsureSessionForThreadAsync(
        string aguiThreadId,
        string? firstUserMessage = null,
        CancellationToken ct = default)
        => (await EnsureSessionForThreadAsync(
            new ChatSessionEnsureRequest
            {
                AguiThreadId = aguiThreadId,
                FirstUserMessage = firstUserMessage,
            },
            ct)).SessionId;

    /// <summary>
    /// Ensures a server-owned session exists for the given AG-UI thread ID and thin
    /// lifecycle metadata. Creates one if none exists and promotes archived rows back
    /// to active when a new persisted turn arrives for the same thread.
    /// </summary>
    public async Task<ChatSessionEnsureResult> EnsureSessionForThreadAsync(
        ChatSessionEnsureRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string normalizedProtocolVersion = string.IsNullOrWhiteSpace(request.ServerProtocolVersion)
            ? ChatSessionProtocolVersions.Current
            : request.ServerProtocolVersion;
        var session = await db.ChatSessions
            .FirstOrDefaultAsync(s => s.AguiThreadId == request.AguiThreadId, ct);

        if (session is not null)
        {
            ApplyEnsureRequest(session, request, normalizedProtocolVersion);
            await SaveSessionChangesAsync(session, request, normalizedProtocolVersion, ct);
            return new ChatSessionEnsureResult
            {
                SessionId = session.Id,
                ServerProtocolVersion = session.ServerProtocolVersion,
            };
        }

        session = new ChatSession
        {
            AguiThreadId = request.AguiThreadId,
        };
        ApplyEnsureRequest(session, request, normalizedProtocolVersion);
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
                .FirstAsync(s => s.AguiThreadId == request.AguiThreadId, ct);
            ApplyEnsureRequest(session, request, normalizedProtocolVersion);
            await SaveSessionChangesAsync(session, request, normalizedProtocolVersion, ct);
        }

        return new ChatSessionEnsureResult
        {
            SessionId = session.Id,
            ServerProtocolVersion = session.ServerProtocolVersion,
        };
    }

    private static void ApplyEnsureRequest(
        ChatSession session,
        ChatSessionEnsureRequest request,
        string normalizedProtocolVersion)
    {
        session.LastActivityAt = DateTimeOffset.UtcNow;
        session.ServerProtocolVersion = normalizedProtocolVersion;

        if (session.Status == ChatSessionStatus.Archived)
        {
            session.Status = ChatSessionStatus.Active;
            session.ArchivedAt = null;
        }

        string? titleCandidate = BuildTitleCandidate(request.FirstUserMessage);
        if (string.IsNullOrWhiteSpace(session.Title) && !string.IsNullOrWhiteSpace(titleCandidate))
        {
            session.Title = titleCandidate;
        }

        ApplyImmutableMetadata(
            currentValue: session.SubjectModule,
            requestedValue: request.SubjectModule,
            fieldName: nameof(ChatSession.SubjectModule),
            assign: value => session.SubjectModule = value);

        ApplyImmutableMetadata(
            currentValue: session.SubjectEntityType,
            requestedValue: request.SubjectEntityType,
            fieldName: nameof(ChatSession.SubjectEntityType),
            assign: value => session.SubjectEntityType = value);

        ApplyImmutableMetadata(
            currentValue: session.SubjectEntityId,
            requestedValue: request.SubjectEntityId,
            fieldName: nameof(ChatSession.SubjectEntityId),
            assign: value => session.SubjectEntityId = value);

        if (!string.IsNullOrWhiteSpace(request.PreferredModelId))
        {
            session.PreferredModelId = request.PreferredModelId;
        }
    }

    private async Task SaveSessionChangesAsync(
        ChatSession session,
        ChatSessionEnsureRequest request,
        string normalizedProtocolVersion,
        CancellationToken ct)
    {
        const int MaxConcurrencyRetries = 2;

        for (int attempt = 0; ; attempt++)
        {
            session.ConcurrencyStamp = Guid.NewGuid().ToString("N");

            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                await db.Entry(session).ReloadAsync(ct);
                ApplyEnsureRequest(session, request, normalizedProtocolVersion);
            }
        }
    }

    private static void ApplyImmutableMetadata(
        string? currentValue,
        string? requestedValue,
        string fieldName,
        Action<string> assign)
    {
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            assign(requestedValue);
            return;
        }

        if (!string.Equals(currentValue, requestedValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Chat session {fieldName} is immutable once set. Existing='{currentValue}', Requested='{requestedValue}'.");
        }
    }

    private static string? BuildTitleCandidate(string? firstUserMessage)
    {
        if (string.IsNullOrWhiteSpace(firstUserMessage))
        {
            return null;
        }

        string collapsed = string.Join(
            " ",
            firstUserMessage
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (string.IsNullOrWhiteSpace(collapsed))
        {
            return null;
        }

        return collapsed.Length <= MaxTitleLength
            ? collapsed
            : $"{collapsed[..(MaxTitleLength - 3)]}...";
    }
}
