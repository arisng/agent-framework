// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Data.Entities;
using AGUIWebChat.Server.Models.Quiz;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Services;

/// <summary>
/// Service implementation for retrieving quiz data from SQLite database.
/// </summary>
public sealed class MockQuizService : IMockQuizService
{
    private readonly QuizDbContext _context;
    private readonly ILogger<MockQuizService> _logger;
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="MockQuizService"/> class.
    /// </summary>
    /// <param name="context">The quiz database context.</param>
    /// <param name="logger">The logger instance.</param>
    public MockQuizService(QuizDbContext context, ILogger<MockQuizService> logger)
    {
        this._context = context ?? throw new ArgumentNullException(nameof(context));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<List<QuizDto>> GetAllQuizzesAsync(CancellationToken cancellationToken = default)
    {
        this._logger.LogInformation("Retrieving all quizzes from database");

        List<QuizEntity> quizEntities = await this._context.Quizzes
            .Include(q => q.Cards)
                .ThenInclude(c => c.Answers)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

        this._logger.LogInformation("Retrieved {Count} quizzes", quizEntities.Count);

        return quizEntities.ConvertAll(this.MapEntityToDto);
    }

    /// <inheritdoc/>
    public async Task<QuizDto?> GetQuizByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            this._logger.LogWarning("GetQuizByIdAsync called with null or empty ID");
            return null;
        }

        this._logger.LogInformation("Retrieving quiz with ID: {QuizId}", id);

        QuizEntity? quizEntity = await this._context.Quizzes
            .Include(q => q.Cards)
                .ThenInclude(c => c.Answers)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quizEntity == null)
        {
            this._logger.LogWarning("Quiz not found with ID: {QuizId}", id);
            return null;
        }

        this._logger.LogInformation("Found quiz: {Title}", quizEntity.Title);

        return this.MapEntityToDto(quizEntity);
    }

    /// <inheritdoc/>
    public async Task<List<QuizDto>> GetQuizzesByTopicAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            this._logger.LogWarning("GetQuizzesByTopicAsync called with null or empty topic");
            return new List<QuizDto>();
        }

        this._logger.LogInformation("Searching quizzes by topic: {Topic}", topic);

        List<QuizEntity> quizEntities = await this._context.Quizzes
            .Include(q => q.Cards)
                .ThenInclude(c => c.Answers)
            .Where(q => q.Title.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                       (q.Instructions != null && q.Instructions.Contains(topic, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

        this._logger.LogInformation("Found {Count} quizzes matching topic: {Topic}", quizEntities.Count, topic);

        return quizEntities.ConvertAll(this.MapEntityToDto);
    }

    /// <summary>
    /// Maps a quiz entity to a quiz DTO.
    /// </summary>
    /// <param name="entity">The quiz entity.</param>
    /// <returns>The mapped quiz DTO.</returns>
    private QuizDto MapEntityToDto(QuizEntity entity)
    {
        return new QuizDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Instructions = entity.Instructions,
            Cards = entity.Cards
                .OrderBy(c => c.Sequence)
                .Select(this.MapCardEntityToDto)
                .ToList()
        };
    }

    /// <summary>
    /// Maps a question card entity to a question card DTO.
    /// </summary>
    /// <param name="entity">The question card entity.</param>
    /// <returns>The mapped question card DTO.</returns>
    private QuestionCardDto MapCardEntityToDto(QuestionCardEntity entity)
    {
        // Deserialize JSON fields
        QuestionContentDto question = this.DeserializeJson<QuestionContentDto>(entity.QuestionJson, "question content");
        SelectionRuleDto selection = this.DeserializeJson<SelectionRuleDto>(entity.SelectionJson, "selection rule");
        List<string> correctAnswerIds = this.DeserializeJson<List<string>>(entity.CorrectAnswerIdsJson, "correct answer IDs");
        CorrectAnswerDisplayRuleDto correctAnswerDisplay = this.DeserializeJson<CorrectAnswerDisplayRuleDto>(
            entity.CorrectAnswerDisplayJson, "correct answer display rule");

        return new QuestionCardDto
        {
            Id = entity.Id,
            Sequence = entity.Sequence,
            Question = question,
            Answers = entity.Answers.Select(MapAnswerEntityToDto).ToList(),
            Selection = selection,
            CorrectAnswerIds = correctAnswerIds,
            CorrectAnswerDisplay = correctAnswerDisplay,
            UserChoiceIds = new List<string>(), // Default empty for new cards
            Evaluation = null // No evaluation initially
        };
    }

    /// <summary>
    /// Maps an answer option entity to an answer option DTO.
    /// </summary>
    /// <param name="entity">The answer option entity.</param>
    /// <returns>The mapped answer option DTO.</returns>
    private static AnswerOptionDto MapAnswerEntityToDto(AnswerOptionEntity entity)
    {
        return new AnswerOptionDto
        {
            Id = entity.Id,
            Text = entity.Text,
            Description = entity.Description,
            MediaUrl = entity.MediaUrl,
            IsDisabled = entity.IsDisabled ? true : null // Only set if true
        };
    }

    /// <summary>
    /// Deserializes a JSON string to the specified type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="json">The JSON string.</param>
    /// <param name="fieldName">The field name for logging purposes.</param>
    /// <returns>The deserialized object.</returns>
    private T DeserializeJson<T>(string json, string fieldName)
    {
        try
        {
            T? result = JsonSerializer.Deserialize<T>(json, s_jsonOptions);

            if (result == null)
            {
                this._logger.LogError("Failed to deserialize {FieldName}: result was null. JSON: {Json}", fieldName, json);
                throw new InvalidOperationException($"Failed to deserialize {fieldName}");
            }

            return result;
        }
        catch (JsonException ex)
        {
            this._logger.LogError(ex, "JSON deserialization error for {FieldName}. JSON: {Json}", fieldName, json);
            throw;
        }
    }
}
