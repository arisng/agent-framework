// Copyright (c) Microsoft. All rights reserved.

using AGUIWebChat.Client.Models.QuizModels;

namespace AGUIWebChat.Client.Services;

/// <summary>
/// Service for submitting quiz answers and retrieving evaluations.
/// </summary>
public interface IQuizService
{
    /// <summary>
    /// Submits the selected answers for a specific quiz card and returns the evaluation result.
    /// </summary>
    /// <param name="quizId">The unique identifier of the quiz.</param>
    /// <param name="cardId">The unique identifier of the question card.</param>
    /// <param name="selectedAnswerIds">The list of selected answer IDs.</param>
    /// <returns>The evaluation result for the submitted answers.</returns>
    Task<CardEvaluation> SubmitAnswersAsync(string quizId, string cardId, List<string> selectedAnswerIds);
}
