// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGUIWebChat.Server.Data.Entities;

/// <summary>
/// Entity representing a single question card within a quiz.
/// </summary>
[Table("QuestionCards")]
public sealed class QuestionCardEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the question card.
    /// </summary>
    [Key]
    [Required]
    [MaxLength(100)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the foreign key to the parent quiz.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string QuizId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sequence number determining the order of this card in the quiz.
    /// </summary>
    [Required]
    public int Sequence { get; set; }

    /// <summary>
    /// Gets or sets the question content as JSON (includes text, description, mediaUrl).
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string QuestionJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selection rule as JSON (mode, minSelections, maxSelections).
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string SelectionJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correct answer IDs as JSON array.
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string CorrectAnswerIdsJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the correct answer display rule as JSON (visibility, allowReveal).
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string CorrectAnswerDisplayJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the card was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the navigation property to the parent quiz.
    /// </summary>
    [ForeignKey(nameof(QuizId))]
    public QuizEntity? Quiz { get; set; }

    /// <summary>
    /// Gets or sets the collection of answer options for this question card.
    /// </summary>
    public ICollection<AnswerOptionEntity> Answers { get; set; } = new List<AnswerOptionEntity>();
}
