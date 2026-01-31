// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Services;

/// <summary>
/// Service implementation for tracking quiz attempts and generating performance analytics.
/// </summary>
public sealed class QuizAnalyticsService : IQuizAnalyticsService
{
    private readonly QuizDbContext _context;
    private readonly ILogger<QuizAnalyticsService> _logger;
    private const string DefaultUserId = "default-user";

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizAnalyticsService"/> class.
    /// </summary>
    /// <param name="context">The quiz database context.</param>
    /// <param name="logger">The logger instance.</param>
    public QuizAnalyticsService(QuizDbContext context, ILogger<QuizAnalyticsService> logger)
    {
        this._context = context ?? throw new ArgumentNullException(nameof(context));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string> RecordAttemptAsync(
        string quizId,
        string cardId,
        string? userId,
        List<string> selectedAnswerIds,
        bool isCorrect,
        double score,
        CancellationToken cancellationToken = default)
    {
        string effectiveUserId = userId ?? DefaultUserId;

        this._logger.LogInformation("Recording attempt for quiz {QuizId}, card {CardId}, user {UserId}", quizId, cardId, effectiveUserId);

        // Find or create an active session for this user and quiz
        QuizSessionEntity? session = await this._context.QuizSessions
            .FirstOrDefaultAsync(
                s => s.QuizId == quizId && s.UserId == effectiveUserId && s.Status == "InProgress",
                cancellationToken);

        if (session == null)
        {
            // Create new session
            string sessionId = $"session-{Guid.NewGuid()}";
            session = new QuizSessionEntity
            {
                Id = sessionId,
                QuizId = quizId,
                UserId = effectiveUserId,
                StartedAt = DateTime.UtcNow,
                Status = "InProgress"
            };

            await this._context.QuizSessions.AddAsync(session, cancellationToken);
            this._logger.LogInformation("Created new quiz session: {SessionId}", sessionId);
        }

        // Create attempt record
        QuizAttemptEntity attempt = new()
        {
            Id = $"attempt-{Guid.NewGuid()}",
            SessionId = session.Id,
            CardId = cardId,
            SelectedAnswerIdsJson = JsonSerializer.Serialize(selectedAnswerIds),
            IsCorrect = isCorrect,
            Score = score,
            AttemptedAt = DateTime.UtcNow
        };

        await this._context.QuizAttempts.AddAsync(attempt, cancellationToken);
        await this._context.SaveChangesAsync(cancellationToken);

        this._logger.LogInformation("Recorded attempt {AttemptId} for session {SessionId}", attempt.Id, session.Id);

        return session.Id;
    }

    /// <inheritdoc/>
    public async Task CompleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        this._logger.LogInformation("Completing session {SessionId}", sessionId);

        QuizSessionEntity? session = await this._context.QuizSessions.FindAsync(new object[] { sessionId }, cancellationToken);

        if (session == null)
        {
            this._logger.LogWarning("Session not found: {SessionId}", sessionId);
            return;
        }

        session.Status = "Completed";
        session.CompletedAt = DateTime.UtcNow;

        await this._context.SaveChangesAsync(cancellationToken);

        this._logger.LogInformation("Session {SessionId} marked as completed", sessionId);
    }

    /// <inheritdoc/>
    public async Task<List<QuizHistoryDto>> GetHistoryAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        string effectiveUserId = userId ?? DefaultUserId;

        this._logger.LogInformation("Retrieving quiz history for user {UserId}", effectiveUserId);

        List<QuizSessionEntity> sessions = await this._context.QuizSessions
            .Include(s => s.Quiz)
            .Include(s => s.Attempts)
            .Where(s => s.UserId == effectiveUserId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken);

        List<QuizHistoryDto> history = sessions.ConvertAll(session =>
        {
            int totalCards = this._context.QuestionCards
                .Count(c => c.QuizId == session.QuizId);

            int completedCards = session.Attempts.Count;

            double averageScore = session.Attempts.Count > 0
                ? session.Attempts.Average(a => a.Score)
                : 0.0;

            return new QuizHistoryDto
            {
                SessionId = session.Id,
                QuizId = session.QuizId,
                QuizTitle = session.Quiz?.Title ?? "Unknown Quiz",
                StartedAt = session.StartedAt,
                CompletedAt = session.CompletedAt,
                Status = session.Status,
                TotalCards = totalCards,
                CompletedCards = completedCards,
                AverageScore = averageScore
            };
        });

        this._logger.LogInformation("Retrieved {Count} quiz sessions for user {UserId}", history.Count, effectiveUserId);

        return history;
    }

    /// <inheritdoc/>
    public async Task<QuizAnalyticsDto> GetAnalyticsAsync(
        string? userId,
        CancellationToken cancellationToken = default)
    {
        string effectiveUserId = userId ?? DefaultUserId;

        this._logger.LogInformation("Calculating analytics for user {UserId}", effectiveUserId);

        List<QuizSessionEntity> sessions = await this._context.QuizSessions
            .Include(s => s.Quiz)
            .Include(s => s.Attempts)
            .Where(s => s.UserId == effectiveUserId)
            .ToListAsync(cancellationToken);

        int totalSessions = sessions.Count;
        int completedSessions = sessions.Count(s => s.Status == "Completed");

        List<QuizAttemptEntity> allAttempts = sessions
            .SelectMany(s => s.Attempts)
            .ToList();

        int totalAttempts = allAttempts.Count;

        double overallAverageScore = totalAttempts > 0
            ? allAttempts.Average(a => a.Score)
            : 0.0;

        double successRate = totalAttempts > 0
            ? (double)allAttempts.Count(a => a.IsCorrect) / totalAttempts
            : 0.0;

        List<string> topicsCovered = sessions
            .Where(s => s.Quiz != null)
            .Select(s => s.Quiz!.Title)
            .Distinct()
            .ToList();

        Dictionary<string, double> scoresByTopic = sessions
            .Where(s => s.Quiz != null && s.Attempts.Count > 0)
            .GroupBy(s => s.Quiz!.Title)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(s => s.Attempts).Average(a => a.Score));

        QuizAnalyticsDto analytics = new()
        {
            TotalSessions = totalSessions,
            CompletedSessions = completedSessions,
            TotalAttempts = totalAttempts,
            OverallAverageScore = overallAverageScore,
            SuccessRate = successRate,
            TopicsCovered = topicsCovered,
            ScoresByTopic = scoresByTopic
        };

        this._logger.LogInformation(
            "Analytics calculated: {TotalSessions} sessions, {TotalAttempts} attempts, {AvgScore:P0} average score",
            totalSessions,
            totalAttempts,
            overallAverageScore);

        return analytics;
    }
}
