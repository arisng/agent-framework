// Copyright (c) Microsoft. All rights reserved.

using AGUIWebChatServer.Models;

namespace AGUIWebChatServer.Tools;

/// <summary>
/// Defines the interface for quiz generation tool that creates interactive quiz content.
/// </summary>
public interface IQuizGeneratorTool
{
    /// <summary>
    /// Generates a quiz asynchronously based on the provided request parameters.
    /// </summary>
    /// <param name="request">The quiz generation request containing topic, difficulty, number of questions, and question types.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the generated quiz JSON string.</returns>
    Task<string> GenerateQuizAsync(QuizGenerationRequest request, CancellationToken cancellationToken = default);
}
