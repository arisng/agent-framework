// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGUIWebChat.Server.Data.Entities;

/// <summary>
/// Entity representing the evaluation result for a quiz submission.
/// </summary>
[Table("QuizEvaluations")]
public sealed class QuizEvaluationEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the evaluation.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the submission being evaluated.
    /// </summary>
    [Required]
    public int SubmissionId { get; set; }

    /// <summary>
    /// Gets or sets whether the submission was correct.
    /// </summary>
    [Required]
    public bool IsCorrect { get; set; }

    /// <summary>
    /// Gets or sets the score for this submission (0-100 scale).
    /// </summary>
    [Required]
    [Range(0, 100)]
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets the correct answer IDs as JSON array.
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string CorrectAnswerIdsJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets optional feedback message for the user.
    /// </summary>
    public string? Feedback { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the evaluation was performed.
    /// </summary>
    [Required]
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the navigation property to the submission.
    /// </summary>
    [ForeignKey(nameof(SubmissionId))]
    public QuizSubmissionEntity? Submission { get; set; }
}
