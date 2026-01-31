// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AGUIWebChat.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Data;

/// <summary>
/// Seeds the database with mock quiz data for testing and demonstration purposes.
/// </summary>
public static class QuizDataSeeder
{
    /// <summary>
    /// Seeds the database with sample quizzes if it is empty.
    /// </summary>
    /// <param name="context">The quiz database context.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public static async Task SeedAsync(QuizDbContext context)
    {
        // Check if database already has data
        if (await context.Quizzes.AnyAsync())
        {
            return; // Database already seeded
        }

        List<QuizEntity> quizzes = CreateSampleQuizzes();

        await context.Quizzes.AddRangeAsync(quizzes);
        await context.SaveChangesAsync();
    }

    private static List<QuizEntity> CreateSampleQuizzes()
    {
        return new List<QuizEntity>
        {
            CreateProgrammingQuiz(),
            CreateScienceQuiz(),
            CreateHistoryQuiz(),
            CreateGeographyQuiz(),
            CreateMathQuiz()
        };
    }

    /// <summary>
    /// Creates a programming quiz with single-select questions.
    /// </summary>
    private static QuizEntity CreateProgrammingQuiz()
    {
        const string quizId = "quiz-programming-001";
        DateTime now = DateTime.UtcNow;

        QuizEntity quiz = new()
        {
            Id = quizId,
            Title = "C# Programming Fundamentals",
            Instructions = "Test your knowledge of C# programming basics. Select the best answer for each question.",
            CreatedAt = now,
            UpdatedAt = now,
            Cards = new List<QuestionCardEntity>()
        };

        // Question 1: Basic syntax
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-1",
            QuizId = quizId,
            Sequence = 1,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "What keyword is used to declare a constant in C#?",
                description = "Choose the correct keyword for declaring immutable values."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "single",
                minSelections = 1,
                maxSelections = 1
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[] { $"{quizId}-card-1-ans-2" }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-1-ans-1", QuestionCardId = $"{quizId}-card-1", Text = "var", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-2", QuestionCardId = $"{quizId}-card-1", Text = "const", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-3", QuestionCardId = $"{quizId}-card-1", Text = "let", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-4", QuestionCardId = $"{quizId}-card-1", Text = "final", CreatedAt = now }
            }
        });

        // Question 2: Access modifiers
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-2",
            QuizId = quizId,
            Sequence = 2,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "Which access modifier makes a member accessible only within the same class?",
                description = "Consider the scope visibility of the modifier."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "single",
                minSelections = 1,
                maxSelections = 1
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[] { $"{quizId}-card-2-ans-1" }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-2-ans-1", QuestionCardId = $"{quizId}-card-2", Text = "private", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-2", QuestionCardId = $"{quizId}-card-2", Text = "public", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-3", QuestionCardId = $"{quizId}-card-2", Text = "protected", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-4", QuestionCardId = $"{quizId}-card-2", Text = "internal", CreatedAt = now }
            }
        });

        // Question 3: Data types
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-3",
            QuizId = quizId,
            Sequence = 3,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "What is the default value of a bool variable in C#?",
                mediaUrl = "https://via.placeholder.com/600x400/0078D4/FFFFFF?text=C%23+Data+Types"
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "single",
                minSelections = 1,
                maxSelections = 1
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[] { $"{quizId}-card-3-ans-2" }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-3-ans-1", QuestionCardId = $"{quizId}-card-3", Text = "true", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-2", QuestionCardId = $"{quizId}-card-3", Text = "false", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-3", QuestionCardId = $"{quizId}-card-3", Text = "null", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-4", QuestionCardId = $"{quizId}-card-3", Text = "0", CreatedAt = now }
            }
        });

        return quiz;
    }

    /// <summary>
    /// Creates a science quiz with multi-select questions.
    /// </summary>
    private static QuizEntity CreateScienceQuiz()
    {
        const string quizId = "quiz-science-001";
        DateTime now = DateTime.UtcNow;

        QuizEntity quiz = new()
        {
            Id = quizId,
            Title = "Basic Science Concepts",
            Instructions = "Select all correct answers for each question. Some questions may have multiple correct answers.",
            CreatedAt = now,
            UpdatedAt = now,
            Cards = new List<QuestionCardEntity>()
        };

        // Question 1: States of matter (multi-select)
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-1",
            QuizId = quizId,
            Sequence = 1,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "Which of the following are states of matter?",
                description = "Select all that apply."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "multiple",
                minSelections = 1,
                maxSelections = 5
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[]
            {
                $"{quizId}-card-1-ans-1",
                $"{quizId}-card-1-ans-2",
                $"{quizId}-card-1-ans-3",
                $"{quizId}-card-1-ans-4"
            }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-1-ans-1", QuestionCardId = $"{quizId}-card-1", Text = "Solid", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-2", QuestionCardId = $"{quizId}-card-1", Text = "Liquid", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-3", QuestionCardId = $"{quizId}-card-1", Text = "Gas", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-4", QuestionCardId = $"{quizId}-card-1", Text = "Plasma", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-5", QuestionCardId = $"{quizId}-card-1", Text = "Energy", CreatedAt = now }
            }
        });

        // Question 2: Renewable energy (multi-select)
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-2",
            QuizId = quizId,
            Sequence = 2,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "Which of these are renewable energy sources?",
                description = "Choose all renewable sources.",
                mediaUrl = "https://www.w3schools.com/html/mov_bbb.mp4"
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "multiple",
                minSelections = 1,
                maxSelections = 6
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[]
            {
                $"{quizId}-card-2-ans-1",
                $"{quizId}-card-2-ans-2",
                $"{quizId}-card-2-ans-3",
                $"{quizId}-card-2-ans-4"
            }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-2-ans-1", QuestionCardId = $"{quizId}-card-2", Text = "Solar", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-2", QuestionCardId = $"{quizId}-card-2", Text = "Wind", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-3", QuestionCardId = $"{quizId}-card-2", Text = "Hydroelectric", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-4", QuestionCardId = $"{quizId}-card-2", Text = "Geothermal", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-5", QuestionCardId = $"{quizId}-card-2", Text = "Coal", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-6", QuestionCardId = $"{quizId}-card-2", Text = "Natural Gas", CreatedAt = now }
            }
        });

        return quiz;
    }

    /// <summary>
    /// Creates a history quiz with single-select questions.
    /// </summary>
    private static QuizEntity CreateHistoryQuiz()
    {
        const string quizId = "quiz-history-001";
        DateTime now = DateTime.UtcNow;

        QuizEntity quiz = new()
        {
            Id = quizId,
            Title = "World History Milestones",
            Instructions = "Answer questions about important historical events.",
            CreatedAt = now,
            UpdatedAt = now,
            Cards = new List<QuestionCardEntity>()
        };

        // Question 1: World War II
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-1",
            QuizId = quizId,
            Sequence = 1,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "In which year did World War II end?",
                description = "Consider the end of the war in Europe and Asia."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "single",
                minSelections = 1,
                maxSelections = 1
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[] { $"{quizId}-card-1-ans-3" }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-1-ans-1", QuestionCardId = $"{quizId}-card-1", Text = "1943", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-2", QuestionCardId = $"{quizId}-card-1", Text = "1944", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-3", QuestionCardId = $"{quizId}-card-1", Text = "1945", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-4", QuestionCardId = $"{quizId}-card-1", Text = "1946", CreatedAt = now }
            }
        });

        // Question 2: Moon landing
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-2",
            QuizId = quizId,
            Sequence = 2,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "Who was the first person to walk on the moon?",
                mediaUrl = "https://via.placeholder.com/600x400/1C1C1C/FFFFFF?text=Space+Exploration"
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "single",
                minSelections = 1,
                maxSelections = 1
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[] { $"{quizId}-card-2-ans-1" }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-2-ans-1", QuestionCardId = $"{quizId}-card-2", Text = "Neil Armstrong", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-2", QuestionCardId = $"{quizId}-card-2", Text = "Buzz Aldrin", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-3", QuestionCardId = $"{quizId}-card-2", Text = "Yuri Gagarin", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-4", QuestionCardId = $"{quizId}-card-2", Text = "John Glenn", CreatedAt = now }
            }
        });

        // Question 3: Renaissance
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-3",
            QuizId = quizId,
            Sequence = 3,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "In which country did the Renaissance begin?",
                description = "The Renaissance was a cultural movement in the 14th-17th centuries."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "single",
                minSelections = 1,
                maxSelections = 1
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[] { $"{quizId}-card-3-ans-2" }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-3-ans-1", QuestionCardId = $"{quizId}-card-3", Text = "France", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-2", QuestionCardId = $"{quizId}-card-3", Text = "Italy", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-3", QuestionCardId = $"{quizId}-card-3", Text = "Spain", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-4", QuestionCardId = $"{quizId}-card-3", Text = "England", CreatedAt = now }
            }
        });

        return quiz;
    }

    /// <summary>
    /// Creates a geography quiz with mixed question types.
    /// </summary>
    private static QuizEntity CreateGeographyQuiz()
    {
        const string quizId = "quiz-geography-001";
        DateTime now = DateTime.UtcNow;

        QuizEntity quiz = new()
        {
            Id = quizId,
            Title = "World Geography",
            Instructions = "Test your knowledge of countries, capitals, and landmarks around the world.",
            CreatedAt = now,
            UpdatedAt = now,
            Cards = new List<QuestionCardEntity>()
        };

        // Question 1: Largest country (single-select)
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-1",
            QuizId = quizId,
            Sequence = 1,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "Which is the largest country by land area?",
                description = "Consider total land area only."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "single",
                minSelections = 1,
                maxSelections = 1
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[] { $"{quizId}-card-1-ans-1" }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-1-ans-1", QuestionCardId = $"{quizId}-card-1", Text = "Russia", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-2", QuestionCardId = $"{quizId}-card-1", Text = "Canada", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-3", QuestionCardId = $"{quizId}-card-1", Text = "China", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-4", QuestionCardId = $"{quizId}-card-1", Text = "United States", CreatedAt = now }
            }
        });

        // Question 2: African countries (multi-select)
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-2",
            QuizId = quizId,
            Sequence = 2,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "Which of these countries are located in Africa?",
                description = "Select all African countries."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "multiple",
                minSelections = 1,
                maxSelections = 6
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[]
            {
                $"{quizId}-card-2-ans-1",
                $"{quizId}-card-2-ans-2",
                $"{quizId}-card-2-ans-4"
            }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-2-ans-1", QuestionCardId = $"{quizId}-card-2", Text = "Kenya", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-2", QuestionCardId = $"{quizId}-card-2", Text = "Egypt", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-3", QuestionCardId = $"{quizId}-card-2", Text = "Brazil", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-4", QuestionCardId = $"{quizId}-card-2", Text = "Nigeria", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-5", QuestionCardId = $"{quizId}-card-2", Text = "India", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-6", QuestionCardId = $"{quizId}-card-2", Text = "Australia", CreatedAt = now }
            }
        });

        // Question 3: Capital of France (single-select)
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-3",
            QuizId = quizId,
            Sequence = 3,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "What is the capital of France?",
                description = "Choose the correct capital city."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "single",
                minSelections = 1,
                maxSelections = 1
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[] { $"{quizId}-card-3-ans-1" }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-3-ans-1", QuestionCardId = $"{quizId}-card-3", Text = "Paris", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-2", QuestionCardId = $"{quizId}-card-3", Text = "London", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-3", QuestionCardId = $"{quizId}-card-3", Text = "Berlin", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-4", QuestionCardId = $"{quizId}-card-3", Text = "Rome", CreatedAt = now }
            }
        });

        return quiz;
    }

    /// <summary>
    /// Creates a mathematics quiz with mixed question types.
    /// </summary>
    private static QuizEntity CreateMathQuiz()
    {
        const string quizId = "quiz-math-001";
        DateTime now = DateTime.UtcNow;

        QuizEntity quiz = new()
        {
            Id = quizId,
            Title = "Basic Mathematics",
            Instructions = "Solve these fundamental math problems.",
            CreatedAt = now,
            UpdatedAt = now,
            Cards = new List<QuestionCardEntity>()
        };

        // Question 1: Prime numbers (multi-select)
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-1",
            QuizId = quizId,
            Sequence = 1,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "Which of the following numbers are prime?",
                description = "A prime number has exactly two factors: 1 and itself."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "multiple",
                minSelections = 1,
                maxSelections = 6
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[]
            {
                $"{quizId}-card-1-ans-1",
                $"{quizId}-card-1-ans-2",
                $"{quizId}-card-1-ans-4",
                $"{quizId}-card-1-ans-6"
            }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-1-ans-1", QuestionCardId = $"{quizId}-card-1", Text = "2", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-2", QuestionCardId = $"{quizId}-card-1", Text = "3", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-3", QuestionCardId = $"{quizId}-card-1", Text = "4", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-4", QuestionCardId = $"{quizId}-card-1", Text = "5", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-5", QuestionCardId = $"{quizId}-card-1", Text = "6", CreatedAt = now },
                new() { Id = $"{quizId}-card-1-ans-6", QuestionCardId = $"{quizId}-card-1", Text = "7", CreatedAt = now }
            }
        });

        // Question 2: Pythagorean theorem (single-select)
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-2",
            QuizId = quizId,
            Sequence = 2,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "In a right triangle, if the two legs are 3 and 4 units long, what is the length of the hypotenuse?",
                description = "Use the Pythagorean theorem: a² + b² = c²"
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "single",
                minSelections = 1,
                maxSelections = 1
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[] { $"{quizId}-card-2-ans-2" }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-2-ans-1", QuestionCardId = $"{quizId}-card-2", Text = "4 units", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-2", QuestionCardId = $"{quizId}-card-2", Text = "5 units", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-3", QuestionCardId = $"{quizId}-card-2", Text = "6 units", CreatedAt = now },
                new() { Id = $"{quizId}-card-2-ans-4", QuestionCardId = $"{quizId}-card-2", Text = "7 units", CreatedAt = now }
            }
        });

        // Question 3: Quadratic formula (single-select)
        quiz.Cards.Add(new QuestionCardEntity
        {
            Id = $"{quizId}-card-3",
            QuizId = quizId,
            Sequence = 3,
            QuestionJson = JsonSerializer.Serialize(new
            {
                text = "What is the value of x when x² - 5x + 6 = 0?",
                description = "Factor or use the quadratic formula."
            }),
            SelectionJson = JsonSerializer.Serialize(new
            {
                mode = "multiple",
                minSelections = 1,
                maxSelections = 4
            }),
            CorrectAnswerIdsJson = JsonSerializer.Serialize(new[]
            {
                $"{quizId}-card-3-ans-2",
                $"{quizId}-card-3-ans-3"
            }),
            CorrectAnswerDisplayJson = JsonSerializer.Serialize(new
            {
                visibility = "afterSubmission",
                allowReveal = true
            }),
            CreatedAt = now,
            Answers = new List<AnswerOptionEntity>
            {
                new() { Id = $"{quizId}-card-3-ans-1", QuestionCardId = $"{quizId}-card-3", Text = "x = 1", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-2", QuestionCardId = $"{quizId}-card-3", Text = "x = 2", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-3", QuestionCardId = $"{quizId}-card-3", Text = "x = 3", CreatedAt = now },
                new() { Id = $"{quizId}-card-3-ans-4", QuestionCardId = $"{quizId}-card-3", Text = "x = 4", CreatedAt = now }
            }
        });

        return quiz;
    }
}
