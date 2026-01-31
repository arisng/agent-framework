// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUIWebChat.Client.Models.QuizModels;

namespace AGUIWebChat.Client.Services;

/// <summary>
/// Implementation of <see cref="IQuizService"/> for submitting quiz answers and retrieving evaluations.
/// </summary>
public sealed class QuizService : IQuizService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    public QuizService(IHttpClientFactory httpClientFactory)
    {
        this._httpClient = httpClientFactory.CreateClient("aguiserver");
        this._jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <inheritdoc />
    public async Task<CardEvaluation> SubmitAnswersAsync(string quizId, string cardId, List<string> selectedAnswerIds)
    {
        try
        {
            SubmissionRequest request = new()
            {
                QuizId = quizId,
                CardId = cardId,
                SelectedAnswerIds = selectedAnswerIds
            };

            HttpResponseMessage response = await this._httpClient.PostAsJsonAsync("/api/quiz/submit", request, this._jsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Failed to submit quiz answers. Status: {response.StatusCode}, Content: {errorContent}");
            }

            CardEvaluation? evaluation = await response.Content.ReadFromJsonAsync<CardEvaluation>(this._jsonOptions);

            if (evaluation is null)
            {
                throw new InvalidOperationException("Received null evaluation response from server.");
            }

            return evaluation;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Network error while submitting quiz answers: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize evaluation response: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException($"Request timed out while submitting quiz answers: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Request payload for quiz answer submission.
    /// </summary>
    private sealed record SubmissionRequest
    {
        public required string QuizId { get; init; }
        public required string CardId { get; init; }
        public required List<string> SelectedAnswerIds { get; init; }
    }
}
