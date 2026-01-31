// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGUIWebChat.Server.Data.Entities;

/// <summary>
/// Entity representing a user's submission of answers for a quiz card.
/// </summary>
[Table("QuizSubmissions")]
public sealed class QuizSubmissionEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the submission.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the quiz identifier this submission belongs to.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string QuizId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the question card identifier this submission is for.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CardId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user identifier who made this submission.
    /// Optional for anonymous submissions.
    /// </summary>
    [MaxLength(100)]
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the selected answer IDs as JSON array.
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string SelectedAnswerIdsJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the timestamp when the submission was made.
    /// </summary>
    [Required]
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the navigation property to the evaluation result.
    /// </summary>
    public QuizEvaluationEntity? Evaluation { get; set; }
}
