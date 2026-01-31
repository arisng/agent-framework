// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Services;

/// <summary>
/// Service implementation for evaluating quiz answer submissions.
/// </summary>
public sealed class QuizEvaluationService : IQuizEvaluationService
{
    private readonly QuizDbContext _dbContext;
    private readonly ILogger<QuizEvaluationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizEvaluationService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public QuizEvaluationService(QuizDbContext dbContext, ILogger<QuizEvaluationService> logger)
    {
        this._dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<QuizEvaluationEntity> EvaluateAnswersAsync(
        string quizId,
        string cardId,
        List<string> selectedAnswerIds,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(quizId))
        {
            throw new ArgumentException("Quiz ID cannot be null or whitespace.", nameof(quizId));
        }

        if (string.IsNullOrWhiteSpace(cardId))
        {
            throw new ArgumentException("Card ID cannot be null or whitespace.", nameof(cardId));
        }

        if (selectedAnswerIds == null || selectedAnswerIds.Count == 0)
        {
            throw new ArgumentException("Selected answer IDs cannot be null or empty.", nameof(selectedAnswerIds));
        }

        this._logger.LogInformation(
            "Evaluating quiz submission: QuizId={QuizId}, CardId={CardId}, SelectedAnswerIds={SelectedAnswerIds}",
            quizId, cardId, string.Join(",", selectedAnswerIds));

        // Retrieve the question card with correct answers
        QuestionCardEntity? questionCard = await this._dbContext.QuestionCards
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId && c.QuizId == quizId, cancellationToken);

        if (questionCard == null)
        {
            this._logger.LogWarning(
                "Question card not found: QuizId={QuizId}, CardId={CardId}",
                quizId, cardId);
            throw new InvalidOperationException($"Question card not found: QuizId={quizId}, CardId={cardId}");
        }

        // Deserialize correct answer IDs from JSON
        List<string> correctAnswerIds = JsonSerializer.Deserialize<List<string>>(questionCard.CorrectAnswerIdsJson)
            ?? new List<string>();

        // Deserialize selection rule to determine evaluation mode
        SelectionRuleData? selectionRule = JsonSerializer.Deserialize<SelectionRuleData>(questionCard.SelectionJson);
        string selectionMode = selectionRule?.Mode ?? "single";

        // Evaluate the submission
        bool isCorrect;
        int score;
        string feedback;

        if (selectionMode == "single")
        {
            // Single-select: exact match required
            isCorrect = selectedAnswerIds.Count == 1
                && correctAnswerIds.Count == 1
                && selectedAnswerIds[0] == correctAnswerIds[0];
            score = isCorrect ? 100 : 0;
            feedback = isCorrect
                ? "Correct!"
                : $"Incorrect. The correct answer is: {string.Join(", ", correctAnswerIds)}";
        }
        else
        {
            // Multi-select: unordered set comparison
            HashSet<string> selectedSet = new(selectedAnswerIds, StringComparer.OrdinalIgnoreCase);
            HashSet<string> correctSet = new(correctAnswerIds, StringComparer.OrdinalIgnoreCase);

            isCorrect = selectedSet.SetEquals(correctSet);

            // Calculate partial score for multi-select
            // Score = (correct selections / total correct answers) * 100 - (incorrect selections penalty)
            int correctSelections = selectedSet.Intersect(correctSet).Count();
            int incorrectSelections = selectedSet.Except(correctSet).Count();
            int missedSelections = correctSet.Except(selectedSet).Count();

            if (isCorrect)
            {
                score = 100;
                feedback = "Correct! You selected all the right answers.";
            }
            else if (correctSelections > 0)
            {
                // Partial credit: (correct / total_correct) * 100 - (incorrect * 10)
                double partialScore = (double)correctSelections / correctSet.Count * 100;
                partialScore -= incorrectSelections * 10; // Penalty for wrong selections
                score = Math.Max(0, (int)Math.Round(partialScore));

                feedback = $"Partially correct. You got {correctSelections} out of {correctSet.Count} correct answers. ";
                if (incorrectSelections > 0)
                {
                    feedback += $"You selected {incorrectSelections} incorrect answer(s). ";
                }
                if (missedSelections > 0)
                {
                    feedback += $"You missed {missedSelections} correct answer(s). ";
                }
                feedback += $"The correct answers are: {string.Join(", ", correctAnswerIds)}";
            }
            else
            {
                score = 0;
                feedback = $"Incorrect. The correct answers are: {string.Join(", ", correctAnswerIds)}";
            }
        }

        // Store submission in database
        QuizSubmissionEntity submission = new()
        {
            QuizId = quizId,
            CardId = cardId,
            UserId = userId,
            SelectedAnswerIdsJson = JsonSerializer.Serialize(selectedAnswerIds),
            SubmittedAt = DateTime.UtcNow
        };

        this._dbContext.QuizSubmissions.Add(submission);
        await this._dbContext.SaveChangesAsync(cancellationToken);

        // Store evaluation in database
        QuizEvaluationEntity evaluation = new()
        {
            SubmissionId = submission.Id,
            IsCorrect = isCorrect,
            Score = score,
            CorrectAnswerIdsJson = JsonSerializer.Serialize(correctAnswerIds),
            Feedback = feedback,
            EvaluatedAt = DateTime.UtcNow
        };

        this._dbContext.QuizEvaluations.Add(evaluation);
        await this._dbContext.SaveChangesAsync(cancellationToken);

        this._logger.LogInformation(
            "Evaluation completed: SubmissionId={SubmissionId}, IsCorrect={IsCorrect}, Score={Score}",
            submission.Id, isCorrect, score);

        return evaluation;
    }

    /// <inheritdoc/>
    public async Task<QuizEvaluationEntity?> GetEvaluationBySubmissionIdAsync(
        int submissionId,
        CancellationToken cancellationToken = default)
    {
        return await this._dbContext.QuizEvaluations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.SubmissionId == submissionId, cancellationToken);
    }

    /// <summary>
    /// Helper class for deserializing selection rule JSON.
    /// </summary>
#pragma warning disable CA1812 // Class is instantiated via JSON deserialization
    private sealed record SelectionRuleData
#pragma warning restore CA1812
    {
        public string Mode { get; init; } = "single";
        public int? MinSelections { get; init; }
        public int? MaxSelections { get; init; }
    }
}
