// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGUIWebChat.Server.Data.Entities;

/// <summary>
/// Entity representing a single answer option for a quiz question card.
/// </summary>
[Table("AnswerOptions")]
public sealed class AnswerOptionEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the answer option.
    /// </summary>
    [Key]
    [Required]
    [MaxLength(100)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the foreign key to the parent question card.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string QuestionCardId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the answer text.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional description for the answer.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets optional media URL for the answer.
    /// </summary>
    [MaxLength(2000)]
    public Uri? MediaUrl { get; set; }

    /// <summary>
    /// Gets or sets whether this answer option is disabled.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the answer option was created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the navigation property to the parent question card.
    /// </summary>
    [ForeignKey(nameof(QuestionCardId))]
    public QuestionCardEntity? QuestionCard { get; set; }
}
