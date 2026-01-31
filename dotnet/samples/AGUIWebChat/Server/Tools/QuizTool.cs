// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization;
using AGUIWebChat.Server.Models.Quiz;
using AGUIWebChat.Server.Services;

namespace AGUIWebChatServer.Tools;

/// <summary>
/// Request for getting a quiz by topic or ID.
/// </summary>
public sealed record QuizRequest
{
    /// <summary>
    /// Optional topic to search for. If null, returns a random quiz.
    /// </summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>
    /// Optional quiz ID to retrieve. If specified, ignores topic.
    /// </summary>
    [JsonPropertyName("quizId")]
    public string? QuizId { get; init; }
}

/// <summary>
/// Response containing quiz summary information.
/// </summary>
public sealed record QuizSummary
{
    /// <summary>
    /// The unique identifier of the quiz.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// The title of the quiz.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Optional instructions for the quiz.
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    /// <summary>
    /// Number of questions in the quiz.
    /// </summary>
    [JsonPropertyName("questionCount")]
    public required int QuestionCount { get; init; }
}

/// <summary>
/// Tool for retrieving quiz data from the mock quiz database.
/// This tool does NOT generate quizzes via AI; it fetches existing quizzes from SQLite.
/// </summary>
public sealed class QuizTool
{
    private readonly IMockQuizService _mockQuizService;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<QuizTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizTool"/> class.
    /// </summary>
    /// <param name="mockQuizService">The mock quiz service for retrieving quiz data.</param>
    /// <param name="jsonOptions">JSON serialization options.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public QuizTool(IMockQuizService mockQuizService, JsonSerializerOptions jsonOptions, ILogger<QuizTool> logger)
    {
        this._mockQuizService = mockQuizService ?? throw new ArgumentNullException(nameof(mockQuizService));
        this._jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves a list of all available quizzes with summary information.
    /// Use this when the user asks to "list quizzes" or "show available quizzes".
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON string containing array of quiz summaries.</returns>
    public async Task<string> ListQuizzesAsync(CancellationToken cancellationToken = default)
    {
        this._logger.LogInformation("[QuizTool] Listing all available quizzes");

        try
        {
            List<QuizDto> quizzes = await this._mockQuizService.GetAllQuizzesAsync(cancellationToken);

            List<QuizSummary> summaries = quizzes.ConvertAll(quiz => new QuizSummary
            {
                Id = quiz.Id,
                Title = quiz.Title,
                Instructions = quiz.Instructions,
                QuestionCount = quiz.Cards.Count
            });

            this._logger.LogInformation("[QuizTool] Found {Count} quizzes", summaries.Count);

            return JsonSerializer.Serialize(summaries, this._jsonOptions);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "[QuizTool] Error listing quizzes");
            throw new InvalidOperationException("Failed to retrieve quiz list. Please try again.", ex);
        }
    }

    /// <summary>
    /// Retrieves a quiz by topic or ID.
    /// Use this when the user asks to "show me a quiz", "get quiz about [topic]", or "show quiz [id]".
    /// </summary>
    /// <param name="request">The quiz request containing optional topic or quiz ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON string containing the complete quiz with media type application/vnd.quiz+json.</returns>
    public async Task<string> GetQuizAsync(QuizRequest request, CancellationToken cancellationToken = default)
    {
        this._logger.LogInformation("[QuizTool] Getting quiz. Topic: {Topic}, QuizId: {QuizId}",
            request?.Topic ?? "(none)", request?.QuizId ?? "(none)");

        try
        {
            QuizDto? quiz = null;

            // Priority 1: Get by ID if specified
            if (!string.IsNullOrWhiteSpace(request?.QuizId))
            {
                this._logger.LogInformation("[QuizTool] Fetching quiz by ID: {QuizId}", request.QuizId);
                quiz = await this._mockQuizService.GetQuizByIdAsync(request.QuizId, cancellationToken);

                if (quiz == null)
                {
                    this._logger.LogWarning("[QuizTool] Quiz not found with ID: {QuizId}", request.QuizId);
                    throw new InvalidOperationException($"Quiz not found with ID: {request.QuizId}");
                }
            }
            // Priority 2: Search by topic if specified
            else if (!string.IsNullOrWhiteSpace(request?.Topic))
            {
                this._logger.LogInformation("[QuizTool] Searching quizzes by topic: {Topic}", request.Topic);
                List<QuizDto> quizzes = await this._mockQuizService.GetQuizzesByTopicAsync(request.Topic, cancellationToken);

                if (quizzes.Count == 0)
                {
                    this._logger.LogWarning("[QuizTool] No quizzes found for topic: {Topic}", request.Topic);
                    throw new InvalidOperationException($"No quizzes found for topic: {request.Topic}");
                }

                // Return the first matching quiz
                quiz = quizzes[0];
                this._logger.LogInformation("[QuizTool] Found {Count} quiz(es) for topic, returning first: {Title}",
                    quizzes.Count, quiz.Title);
            }
            // Priority 3: Return a random quiz if no filters specified
            else
            {
                this._logger.LogInformation("[QuizTool] No filters specified, fetching random quiz");
                List<QuizDto> allQuizzes = await this._mockQuizService.GetAllQuizzesAsync(cancellationToken);

                if (allQuizzes.Count == 0)
                {
                    this._logger.LogWarning("[QuizTool] No quizzes available in database");
                    throw new InvalidOperationException("No quizzes available. Please ensure the database is seeded.");
                }

                // Select a random quiz
                Random random = new();
                quiz = allQuizzes[random.Next(allQuizzes.Count)];
                this._logger.LogInformation("[QuizTool] Selected random quiz: {Title}", quiz.Title);
            }

            // Serialize quiz to JSON with correct media type
            this._logger.LogInformation("[QuizTool] Successfully retrieved quiz: {Title} with {CardCount} cards",
                quiz.Title, quiz.Cards.Count);

            return JsonSerializer.Serialize(quiz, this._jsonOptions);
        }
        catch (InvalidOperationException)
        {
            // Re-throw user-facing errors
            throw;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "[QuizTool] Error getting quiz");
            throw new InvalidOperationException("Failed to retrieve quiz. Please try again.", ex);
        }
    }
}
