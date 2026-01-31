// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGUIWebChat.Server.Data.Entities;

/// <summary>
/// Entity representing a quiz session (user's attempt to complete a quiz).
/// </summary>
[Table("QuizSessions")]
public sealed class QuizSessionEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the quiz session.
    /// </summary>
    [Key]
    [Required]
    [MaxLength(100)]
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the quiz being attempted.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string QuizId { get; set; }

    /// <summary>
    /// Gets or sets the user ID (for multi-user scenarios).
    /// </summary>
    [MaxLength(100)]
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the session started.
    /// </summary>
    [Required]
    public required DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the session was completed (null if in progress).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the session status: "InProgress" or "Completed".
    /// </summary>
    [Required]
    [MaxLength(20)]
    public required string Status { get; set; } = "InProgress";

    // Navigation properties

    /// <summary>
    /// Gets or sets the quiz entity associated with this session.
    /// </summary>
    [ForeignKey(nameof(QuizId))]
    public QuizEntity? Quiz { get; set; }

    /// <summary>
    /// Gets or sets the collection of attempts (card answers) in this session.
    /// </summary>
    public ICollection<QuizAttemptEntity> Attempts { get; set; } = new List<QuizAttemptEntity>();
}
