// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AGUIWebChat.Client.Models.QuizModels;

/// <summary>
/// Represents a complete quiz containing multiple question cards.
/// </summary>
public sealed record Quiz
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    [JsonPropertyName("cards")]
    public required List<QuestionCard> Cards { get; init; }
}

/// <summary>
/// Represents a single question card within a quiz.
/// </summary>
public sealed record QuestionCard
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("sequence")]
    public required int Sequence { get; init; }

    [JsonPropertyName("question")]
    public required QuestionContent Question { get; init; }

    [JsonPropertyName("answers")]
    public required List<AnswerOption> Answers { get; init; }

    [JsonPropertyName("selection")]
    public required SelectionRule Selection { get; init; }

    [JsonPropertyName("correctAnswerIds")]
    public required List<string> CorrectAnswerIds { get; init; }

    [JsonPropertyName("correctAnswerDisplay")]
    public required CorrectAnswerDisplayRule CorrectAnswerDisplay { get; init; }

    [JsonPropertyName("userChoiceIds")]
    public required List<string> UserChoiceIds { get; init; }

    [JsonPropertyName("evaluation")]
    public CardEvaluation? Evaluation { get; init; }
}

/// <summary>
/// Represents the content of a quiz question.
/// </summary>
public sealed record QuestionContent
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("mediaUrl")]
    public Uri? MediaUrl { get; init; }
}

/// <summary>
/// Represents a single answer option for a quiz question.
/// </summary>
public sealed record AnswerOption
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
/// Defines the selection rules for a quiz question (single vs. multiple selection).
/// </summary>
public sealed record SelectionRule
{
    [JsonPropertyName("mode")]
    public required string Mode { get; init; } // "single" or "multiple"

    [JsonPropertyName("minSelections")]
    public int? MinSelections { get; init; }

    [JsonPropertyName("maxSelections")]
    public int? MaxSelections { get; init; }
}

/// <summary>
/// Defines when and how correct answers should be displayed.
/// </summary>
public sealed record CorrectAnswerDisplayRule
{
    [JsonPropertyName("visibility")]
    public required string Visibility { get; init; } // "never" | "afterSubmit" | "afterReveal" | "always"

    [JsonPropertyName("allowReveal")]
    public bool? AllowReveal { get; init; }
}

/// <summary>
/// Represents the evaluation results for a submitted quiz card.
/// </summary>
public sealed record CardEvaluation
{
    [JsonPropertyName("isCorrect")]
    public required bool IsCorrect { get; init; }

    [JsonPropertyName("score")]
    public double? Score { get; init; }

    [JsonPropertyName("feedback")]
    public string? Feedback { get; init; }
}
