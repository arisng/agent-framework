// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Data.Entities;
using AGUIWebChat.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Controllers;

/// <summary>
/// API controller for quiz operations.
/// </summary>
[ApiController]
[Route("api/quiz")]
public sealed class QuizController : ControllerBase
{
    private readonly IQuizEvaluationService _evaluationService;
    private readonly IQuizAnalyticsService _analyticsService;
    private readonly QuizDbContext _dbContext;
    private readonly ILogger<QuizController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizController"/> class.
    /// </summary>
    /// <param name="evaluationService">The quiz evaluation service.</param>
    /// <param name="analyticsService">The quiz analytics service.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public QuizController(
        IQuizEvaluationService evaluationService,
        IQuizAnalyticsService analyticsService,
        QuizDbContext dbContext,
        ILogger<QuizController> logger)
    {
        this._evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        this._analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        this._dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Submits quiz answers for evaluation.
    /// </summary>
    /// <param name="request">The quiz submission request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluation result.</returns>
    /// <response code="200">Returns the evaluation result with score and feedback.</response>
    /// <response code="400">If the request is invalid or validation fails.</response>
    /// <response code="404">If the quiz or question card is not found.</response>
    /// <response code="500">If an internal server error occurs.</response>
    [HttpPost("submit")]
    [ProducesResponseType(typeof(QuizSubmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitAnswersAsync(
        [FromBody] QuizSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            this._logger.LogWarning("Received null submission request");
            return this.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = "Submission request cannot be null."
            });
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.QuizId))
        {
            this._logger.LogWarning("Submission request missing QuizId");
            return this.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = "QuizId is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.CardId))
        {
            this._logger.LogWarning("Submission request missing CardId");
            return this.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = "CardId is required."
            });
        }

        if (request.SelectedAnswerIds == null || request.SelectedAnswerIds.Count == 0)
        {
            this._logger.LogWarning("Submission request missing SelectedAnswerIds");
            return this.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = "At least one answer must be selected."
            });
        }

        try
        {
            // Verify quiz exists in database
            bool quizExists = await this._dbContext.Quizzes
                .AnyAsync(q => q.Id == request.QuizId, cancellationToken);

            if (!quizExists)
            {
                this._logger.LogWarning("Quiz not found: QuizId={QuizId}", request.QuizId);
                return this.NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Quiz Not Found",
                    Detail = $"Quiz with ID '{request.QuizId}' does not exist."
                });
            }

            // Verify question card exists in database
            bool cardExists = await this._dbContext.QuestionCards
                .AnyAsync(c => c.Id == request.CardId && c.QuizId == request.QuizId, cancellationToken);

            if (!cardExists)
            {
                this._logger.LogWarning(
                    "Question card not found: QuizId={QuizId}, CardId={CardId}",
                    request.QuizId, request.CardId);
                return this.NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Question Card Not Found",
                    Detail = $"Question card with ID '{request.CardId}' does not exist in quiz '{request.QuizId}'."
                });
            }

            this._logger.LogInformation(
                "Processing quiz submission: QuizId={QuizId}, CardId={CardId}, UserId={UserId}",
                request.QuizId, request.CardId, request.UserId ?? "anonymous");

            // Call evaluation service
            QuizEvaluationEntity evaluation = await this._evaluationService.EvaluateAnswersAsync(
                request.QuizId,
                request.CardId,
                request.SelectedAnswerIds,
                request.UserId,
                cancellationToken);

            // Map entity to response DTO
            QuizSubmissionResponse response = new()
            {
                SubmissionId = evaluation.SubmissionId,
                IsCorrect = evaluation.IsCorrect,
                Score = evaluation.Score,
                CorrectAnswerIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(evaluation.CorrectAnswerIdsJson)
                    ?? new List<string>(),
                Feedback = evaluation.Feedback,
                EvaluatedAt = evaluation.EvaluatedAt
            };

            this._logger.LogInformation(
                "Quiz submission evaluated successfully: SubmissionId={SubmissionId}, Score={Score}",
                evaluation.SubmissionId, evaluation.Score);

            return this.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // This should not happen as we pre-validate existence, but handle gracefully
            this._logger.LogError(ex, "Validation error during quiz submission: {Message}", ex.Message);
            return this.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource Not Found",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Internal server error during quiz submission");
            return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while processing the quiz submission."
            });
        }
    }

    /// <summary>
    /// Gets the quiz history for a user.
    /// </summary>
    /// <param name="userId">The optional user ID (defaults to "default-user").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of quiz history items.</returns>
    /// <response code="200">Returns the quiz history.</response>
    /// <response code="500">If an internal server error occurs.</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<QuizHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] string? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            this._logger.LogInformation("Fetching quiz history for user: {UserId}", userId ?? "default-user");

            List<QuizHistoryDto> history = await this._analyticsService.GetHistoryAsync(userId, cancellationToken);

            this._logger.LogInformation("Retrieved {Count} quiz history items", history.Count);

            return this.Ok(history);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error retrieving quiz history");
            return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while retrieving quiz history."
            });
        }
    }

    /// <summary>
    /// Gets quiz analytics for a user.
    /// </summary>
    /// <param name="userId">The optional user ID (defaults to "default-user").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analytics data including scores, success rates, and topics.</returns>
    /// <response code="200">Returns the analytics data.</response>
    /// <response code="500">If an internal server error occurs.</response>
    [HttpGet("analytics")]
    [ProducesResponseType(typeof(QuizAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAnalyticsAsync(
        [FromQuery] string? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            this._logger.LogInformation("Fetching quiz analytics for user: {UserId}", userId ?? "default-user");

            QuizAnalyticsDto analytics = await this._analyticsService.GetAnalyticsAsync(userId, cancellationToken);

            this._logger.LogInformation("Retrieved analytics: {TotalSessions} sessions, {TotalAttempts} attempts",
                analytics.TotalSessions, analytics.TotalAttempts);

            return this.Ok(analytics);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error retrieving quiz analytics");
            return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while retrieving quiz analytics."
            });
        }
    }

    /// <summary>
    /// Gets progress for a specific quiz.
    /// </summary>
    /// <param name="quizId">The quiz ID.</param>
    /// <param name="userId">The optional user ID (defaults to "default-user").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Progress information for the quiz.</returns>
    /// <response code="200">Returns the quiz progress.</response>
    /// <response code="404">If the quiz is not found.</response>
    /// <response code="500">If an internal server error occurs.</response>
    [HttpGet("progress/{quizId}")]
    [ProducesResponseType(typeof(QuizProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProgressAsync(
        string quizId,
        [FromQuery] string? userId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            this._logger.LogInformation("Fetching progress for quiz {QuizId}, user {UserId}", quizId, userId ?? "default-user");

            string effectiveUserId = userId ?? "default-user";

            // Get all sessions for this quiz and user (most recent first)
            List<QuizHistoryDto> sessions = await this._analyticsService.GetHistoryAsync(effectiveUserId, cancellationToken);
            QuizHistoryDto? mostRecentSession = sessions
                .Where(s => s.QuizId == quizId)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefault();

            if (mostRecentSession == null)
            {
                return this.NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Quiz Progress Not Found",
                    Detail = $"No progress found for quiz '{quizId}'."
                });
            }

            QuizProgressDto progress = new()
            {
                QuizId = mostRecentSession.QuizId,
                QuizTitle = mostRecentSession.QuizTitle,
                SessionId = mostRecentSession.SessionId,
                Status = mostRecentSession.Status,
                TotalCards = mostRecentSession.TotalCards,
                CompletedCards = mostRecentSession.CompletedCards,
                AverageScore = mostRecentSession.AverageScore,
                StartedAt = mostRecentSession.StartedAt,
                CompletedAt = mostRecentSession.CompletedAt
            };

            this._logger.LogInformation("Progress retrieved: {CompletedCards}/{TotalCards} cards completed",
                progress.CompletedCards, progress.TotalCards);

            return this.Ok(progress);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error retrieving quiz progress for quiz {QuizId}", quizId);
            return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while retrieving quiz progress."
            });
        }
    }
}

/// <summary>
/// Request model for quiz answer submission.
/// </summary>
public sealed record QuizSubmissionRequest
{
    /// <summary>
    /// Gets or sets the quiz identifier.
    /// </summary>
    [Required]
    public string QuizId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the question card identifier.
    /// </summary>
    [Required]
    public string CardId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected answer IDs.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<string> SelectedAnswerIds { get; init; } = new();

    /// <summary>
    /// Gets or sets the optional user identifier.
    /// </summary>
    public string? UserId { get; init; }
}

/// <summary>
/// Response model for quiz submission evaluation.
/// </summary>
public sealed record QuizSubmissionResponse
{
    /// <summary>
    /// Gets or sets the submission identifier.
    /// </summary>
    public int SubmissionId { get; init; }

    /// <summary>
    /// Gets or sets whether the submission was correct.
    /// </summary>
    public bool IsCorrect { get; init; }

    /// <summary>
    /// Gets or sets the score (0-100 scale).
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    /// Gets or sets the correct answer IDs.
    /// </summary>
    public List<string> CorrectAnswerIds { get; init; } = new();

    /// <summary>
    /// Gets or sets the feedback message.
    /// </summary>
    public string? Feedback { get; init; }

    /// <summary>
    /// Gets or sets the evaluation timestamp.
    /// </summary>
    public DateTime EvaluatedAt { get; init; }
}

/// <summary>
/// Response model for quiz progress information.
/// </summary>
public sealed record QuizProgressDto
{
    /// <summary>
    /// Gets or sets the quiz identifier.
    /// </summary>
    public required string QuizId { get; init; }

    /// <summary>
    /// Gets or sets the quiz title.
    /// </summary>
    public required string QuizTitle { get; init; }

    /// <summary>
    /// Gets or sets the session identifier.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the session status (InProgress or Completed).
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the total number of cards in the quiz.
    /// </summary>
    public required int TotalCards { get; init; }

    /// <summary>
    /// Gets or sets the number of completed cards.
    /// </summary>
    public required int CompletedCards { get; init; }

    /// <summary>
    /// Gets or sets the average score for completed cards (0.0 to 1.0).
    /// </summary>
    public required double AverageScore { get; init; }

    /// <summary>
    /// Gets or sets the session start timestamp.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// Gets or sets the session completion timestamp (null if in progress).
    /// </summary>
    public DateTime? CompletedAt { get; init; }
}
