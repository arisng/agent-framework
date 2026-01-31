// Copyright (c) Microsoft. All rights reserved.

using AGUIWebChat.Server.Models.Quiz;

namespace AGUIWebChat.Server.Services;

/// <summary>
/// Service interface for retrieving quiz data from the database.
/// </summary>
public interface IMockQuizService
{
    /// <summary>
    /// Retrieves all available quizzes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all quizzes.</returns>
    Task<List<QuizDto>> GetAllQuizzesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific quiz by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the quiz.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The quiz if found, null otherwise.</returns>
    Task<QuizDto?> GetQuizByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves quizzes filtered by topic (searches in title and instructions).
    /// </summary>
    /// <param name="topic">The topic to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of quizzes matching the topic.</returns>
    Task<List<QuizDto>> GetQuizzesByTopicAsync(string topic, CancellationToken cancellationToken = default);
}
