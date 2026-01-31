// Copyright (c) Microsoft. All rights reserved.

using AGUIWebChat.Server.Data.Entities;

namespace AGUIWebChat.Server.Services;

/// <summary>
/// Service for evaluating quiz answer submissions.
/// </summary>
public interface IQuizEvaluationService
{
    /// <summary>
    /// Evaluates a user's answer submission for a quiz card.
    /// </summary>
    /// <param name="quizId">The quiz identifier.</param>
    /// <param name="cardId">The question card identifier.</param>
    /// <param name="selectedAnswerIds">The answer IDs selected by the user.</param>
    /// <param name="userId">Optional user identifier for tracking submissions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluation result with score and feedback.</returns>
    Task<QuizEvaluationEntity> EvaluateAnswersAsync(
        string quizId,
        string cardId,
        List<string> selectedAnswerIds,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing evaluation by submission ID.
    /// </summary>
    /// <param name="submissionId">The submission identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluation entity if found, null otherwise.</returns>
    Task<QuizEvaluationEntity?> GetEvaluationBySubmissionIdAsync(
        int submissionId,
        CancellationToken cancellationToken = default);
}
