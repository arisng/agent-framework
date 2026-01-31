// Copyright (c) Microsoft. All rights reserved.

using AGUIWebChat.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Data;

/// <summary>
/// Database context for quiz-related entities using SQLite.
/// </summary>
public sealed class QuizDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuizDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to configure the context.</param>
    public QuizDbContext(DbContextOptions<QuizDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the DbSet for quizzes.
    /// </summary>
    public DbSet<QuizEntity> Quizzes { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DbSet for question cards.
    /// </summary>
    public DbSet<QuestionCardEntity> QuestionCards { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DbSet for answer options.
    /// </summary>
    public DbSet<AnswerOptionEntity> AnswerOptions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DbSet for quiz submissions.
    /// </summary>
    public DbSet<QuizSubmissionEntity> QuizSubmissions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DbSet for quiz evaluations.
    /// </summary>
    public DbSet<QuizEvaluationEntity> QuizEvaluations { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DbSet for quiz sessions.
    /// </summary>
    public DbSet<QuizSessionEntity> QuizSessions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DbSet for quiz attempts.
    /// </summary>
    public DbSet<QuizAttemptEntity> QuizAttempts { get; set; } = null!;

    /// <summary>
    /// Configures the entity relationships and constraints.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Quiz entity
        modelBuilder.Entity<QuizEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.CreatedAt);

            // Configure one-to-many relationship: Quiz -> QuestionCards
            entity.HasMany(e => e.Cards)
                  .WithOne(e => e.Quiz)
                  .HasForeignKey(e => e.QuizId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure QuestionCard entity
        modelBuilder.Entity<QuestionCardEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.QuizId, e.Sequence });

            // Configure one-to-many relationship: QuestionCard -> AnswerOptions
            entity.HasMany(e => e.Answers)
                  .WithOne(e => e.QuestionCard)
                  .HasForeignKey(e => e.QuestionCardId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AnswerOption entity
        modelBuilder.Entity<AnswerOptionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QuestionCardId);
        });

        // Configure QuizSubmission entity
        modelBuilder.Entity<QuizSubmissionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QuizId);
            entity.HasIndex(e => e.CardId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.SubmittedAt);

            // Configure one-to-one relationship: QuizSubmission -> QuizEvaluation
            entity.HasOne(e => e.Evaluation)
                  .WithOne(e => e.Submission)
                  .HasForeignKey<QuizEvaluationEntity>(e => e.SubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure QuizEvaluation entity
        modelBuilder.Entity<QuizEvaluationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SubmissionId)
                  .IsUnique(); // Ensure one evaluation per submission
            entity.HasIndex(e => e.EvaluatedAt);
        });

        // Configure QuizSession entity
        modelBuilder.Entity<QuizSessionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QuizId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.Status);

            // Configure one-to-many relationship: QuizSession -> QuizAttempts
            entity.HasMany(e => e.Attempts)
                  .WithOne(e => e.Session)
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure QuizAttempt entity
        modelBuilder.Entity<QuizAttemptEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CardId);
            entity.HasIndex(e => e.AttemptedAt);
        });
    }
}
