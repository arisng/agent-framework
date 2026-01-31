// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGUIWebChat.Server.Data.Entities;

/// <summary>
/// Entity representing a single quiz attempt (answer submission for a specific card).
/// </summary>
[Table("QuizAttempts")]
public sealed class QuizAttemptEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the quiz attempt.
    /// </summary>
    [Key]
    [Required]
    [MaxLength(100)]
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the quiz session this attempt belongs to.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the question card being answered.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string CardId { get; set; }

    /// <summary>
    /// Gets or sets the selected answer IDs as JSON array string.
    /// </summary>
    [Required]
    public required string SelectedAnswerIdsJson { get; set; }

    /// <summary>
    /// Gets or sets whether the answer was correct.
    /// </summary>
    [Required]
    public required bool IsCorrect { get; set; }

    /// <summary>
    /// Gets or sets the score for this attempt (0.0 to 1.0).
    /// </summary>
    [Required]
    public required double Score { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the attempt was made.
    /// </summary>
    [Required]
    public required DateTime AttemptedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the quiz session this attempt belongs to.
    /// </summary>
    [ForeignKey(nameof(SessionId))]
    public QuizSessionEntity? Session { get; set; }

    /// <summary>
    /// Gets or sets the question card entity associated with this attempt.
    /// </summary>
    [ForeignKey(nameof(CardId))]
    public QuestionCardEntity? Card { get; set; }
}
