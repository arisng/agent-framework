// Copyright (c) Microsoft. All rights reserved.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGUIWebChatServer.Migrations;

/// <inheritdoc />
public partial class AddQuizSubmissionAndEvaluation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "QuizSubmissions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                QuizId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                CardId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                SelectedAnswerIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
                table.PrimaryKey("PK_QuizSubmissions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "QuizEvaluations",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SubmissionId = table.Column<int>(type: "INTEGER", nullable: false),
                IsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                Score = table.Column<int>(type: "INTEGER", nullable: false),
                CorrectAnswerIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                Feedback = table.Column<string>(type: "TEXT", nullable: true),
                EvaluatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuizEvaluations", x => x.Id);
                table.ForeignKey(
                    name: "FK_QuizEvaluations_QuizSubmissions_SubmissionId",
                    column: x => x.SubmissionId,
                    principalTable: "QuizSubmissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_QuizEvaluations_EvaluatedAt",
            table: "QuizEvaluations",
            column: "EvaluatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_QuizEvaluations_SubmissionId",
            table: "QuizEvaluations",
            column: "SubmissionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_QuizSubmissions_CardId",
            table: "QuizSubmissions",
            column: "CardId");

        migrationBuilder.CreateIndex(
            name: "IX_QuizSubmissions_QuizId",
            table: "QuizSubmissions",
            column: "QuizId");

        migrationBuilder.CreateIndex(
            name: "IX_QuizSubmissions_SubmittedAt",
            table: "QuizSubmissions",
            column: "SubmittedAt");

        migrationBuilder.CreateIndex(
            name: "IX_QuizSubmissions_UserId",
            table: "QuizSubmissions",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "QuizEvaluations");

        migrationBuilder.DropTable(
            name: "QuizSubmissions");
    }
}
