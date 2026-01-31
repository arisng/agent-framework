// Copyright (c) Microsoft. All rights reserved.

namespace AGUIWebChat.Server.Services;

/// <summary>
/// Service interface for tracking quiz attempts and generating performance analytics.
/// </summary>
public interface IQuizAnalyticsService
{
    /// <summary>
    /// Records a quiz attempt (card answer submission) in a session.
    /// Creates a new session if one doesn't exist.
    /// </summary>
    /// <param name="quizId">The ID of the quiz being attempted.</param>
    /// <param name="cardId">The ID of the question card being answered.</param>
    /// <param name="userId">The user ID (optional, defaults to "default-user").</param>
    /// <param name="selectedAnswerIds">The list of selected answer IDs.</param>
    /// <param name="isCorrect">Whether the answer was correct.</param>
    /// <param name="score">The score for this attempt (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session ID.</returns>
    Task<string> RecordAttemptAsync(
        string quizId,
        string cardId,
        string? userId,
        List<string> selectedAnswerIds,
        bool isCorrect,
        double score,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a quiz session as completed.
    /// </summary>
    /// <param name="sessionId">The session ID to complete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CompleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the quiz history for a user (list of past sessions with scores).
    /// </summary>
    /// <param name="userId">The user ID (optional, defaults to "default-user").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of quiz history items.</returns>
    Task<List<QuizHistoryDto>> GetHistoryAsync(
        string? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets analytics for a user (total attempts, average score, topics covered, success rate).
    /// </summary>
    /// <param name="userId">The user ID (optional, defaults to "default-user").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analytics data.</returns>
    Task<QuizAnalyticsDto> GetAnalyticsAsync(
        string? userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Data transfer object for quiz history item.
/// </summary>
public sealed record QuizHistoryDto
{
    public required string SessionId { get; init; }
    public required string QuizId { get; init; }
    public required string QuizTitle { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required string Status { get; init; }
    public required int TotalCards { get; init; }
    public required int CompletedCards { get; init; }
    public required double AverageScore { get; init; }
}

/// <summary>
/// Data transfer object for quiz analytics.
/// </summary>
public sealed record QuizAnalyticsDto
{
    public required int TotalSessions { get; init; }
    public required int CompletedSessions { get; init; }
    public required int TotalAttempts { get; init; }
    public required double OverallAverageScore { get; init; }
    public required double SuccessRate { get; init; }
    public required List<string> TopicsCovered { get; init; }
    public required Dictionary<string, double> ScoresByTopic { get; init; }
}
