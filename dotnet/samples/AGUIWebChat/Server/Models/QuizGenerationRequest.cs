// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIWebChatServer.Models;

/// <summary>
/// Represents a request to generate a quiz with specified parameters.
/// </summary>
public sealed record QuizGenerationRequest
{
    /// <summary>
    /// Gets the topic or subject matter for the quiz.
    /// </summary>
    /// <example>Python Programming Basics</example>
    [JsonPropertyName("topic")]
    public required string Topic { get; init; }

    /// <summary>
    /// Gets the difficulty level of the quiz questions.
    /// </summary>
    /// <remarks>
    /// Valid values: "easy", "medium", "hard"
    /// </remarks>
    /// <example>medium</example>
    [JsonPropertyName("difficulty")]
    public required string Difficulty { get; init; }

    /// <summary>
    /// Gets the number of questions to generate for the quiz.
    /// </summary>
    /// <remarks>
    /// Must be a positive integer. Typical range: 1-20.
    /// </remarks>
    /// <example>5</example>
    [JsonPropertyName("numberOfQuestions")]
    public required int NumberOfQuestions { get; init; }

    /// <summary>
    /// Gets the types of questions to include in the quiz.
    /// </summary>
    /// <remarks>
    /// Array of question type strings. Valid values: "single-select", "multi-select", "mixed".
    /// If empty or contains "mixed", a combination of different question types will be generated.
    /// </remarks>
    /// <example>["single-select", "multi-select"]</example>
    [JsonPropertyName("questionTypes")]
    public required List<string> QuestionTypes { get; init; }
}
