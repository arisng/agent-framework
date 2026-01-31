// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGUIWebChat.Server.Data.Entities;

/// <summary>
/// Entity representing a complete quiz containing multiple question cards.
/// </summary>
[Table("Quizzes")]
public sealed class QuizEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the quiz.
    /// </summary>
    [Key]
    [Required]
    [MaxLength(100)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the quiz.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional instructions for the quiz.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the quiz was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the quiz was last updated.
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the collection of question cards belonging to this quiz.
    /// </summary>
    public ICollection<QuestionCardEntity> Cards { get; set; } = new List<QuestionCardEntity>();
}
