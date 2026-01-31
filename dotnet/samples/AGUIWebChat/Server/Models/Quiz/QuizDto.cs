// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AGUIWebChat.Server.Models.Quiz;

/// <summary>
/// Data transfer object representing a complete quiz.
/// </summary>
public sealed record QuizDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    [JsonPropertyName("cards")]
    public required List<QuestionCardDto> Cards { get; init; }
}

/// <summary>
/// Data transfer object representing a single question card.
/// </summary>
public sealed record QuestionCardDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("sequence")]
    public required int Sequence { get; init; }

    [JsonPropertyName("question")]
    public required QuestionContentDto Question { get; init; }

    [JsonPropertyName("answers")]
    public required List<AnswerOptionDto> Answers { get; init; }

    [JsonPropertyName("selection")]
    public required SelectionRuleDto Selection { get; init; }

    [JsonPropertyName("correctAnswerIds")]
    public required List<string> CorrectAnswerIds { get; init; }

    [JsonPropertyName("correctAnswerDisplay")]
    public required CorrectAnswerDisplayRuleDto CorrectAnswerDisplay { get; init; }

    [JsonPropertyName("userChoiceIds")]
    public required List<string> UserChoiceIds { get; init; }

    [JsonPropertyName("evaluation")]
    public CardEvaluationDto? Evaluation { get; init; }
}

/// <summary>
/// Data transfer object representing question content.
/// </summary>
public sealed record QuestionContentDto
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("mediaUrl")]
    public Uri? MediaUrl { get; init; }
}

/// <summary>
/// Data transfer object representing an answer option.
/// </summary>
public sealed record AnswerOptionDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("mediaUrl")]
    public Uri? MediaUrl { get; init; }

    [JsonPropertyName("isDisabled")]
    public bool? IsDisabled { get; init; }
}

/// <summary>
/// Data transfer object representing selection rules.
/// </summary>
public sealed record SelectionRuleDto
{
    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    [JsonPropertyName("minSelections")]
    public int? MinSelections { get; init; }

    [JsonPropertyName("maxSelections")]
    public int? MaxSelections { get; init; }
}

/// <summary>
/// Data transfer object representing correct answer display rules.
/// </summary>
public sealed record CorrectAnswerDisplayRuleDto
{
    [JsonPropertyName("visibility")]
    public required string Visibility { get; init; }

    [JsonPropertyName("allowReveal")]
    public bool? AllowReveal { get; init; }
}

/// <summary>
/// Data transfer object representing card evaluation results.
/// </summary>
public sealed record CardEvaluationDto
{
    [JsonPropertyName("isCorrect")]
    public required bool IsCorrect { get; init; }

    [JsonPropertyName("score")]
    public double? Score { get; init; }

    [JsonPropertyName("feedback")]
    public string? Feedback { get; init; }
}
