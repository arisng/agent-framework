// Copyright (c) Microsoft. All rights reserved.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGUIWebChatServer.Migrations;

/// <inheritdoc />
public partial class AddAnalyticsEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "QuizSessions",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                QuizId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuizSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_QuizSessions_Quizzes_QuizId",
                    column: x => x.QuizId,
                    principalTable: "Quizzes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "QuizAttempts",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                CardId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                SelectedAnswerIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                IsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                Score = table.Column<double>(type: "REAL", nullable: false),
                AttemptedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuizAttempts", x => x.Id);
                table.ForeignKey(
                    name: "FK_QuizAttempts_QuestionCards_CardId",
                    column: x => x.CardId,
                    principalTable: "QuestionCards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_QuizAttempts_QuizSessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "QuizSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_QuizAttempts_AttemptedAt",
            table: "QuizAttempts",
            column: "AttemptedAt");

        migrationBuilder.CreateIndex(
            name: "IX_QuizAttempts_CardId",
            table: "QuizAttempts",
            column: "CardId");

        migrationBuilder.CreateIndex(
            name: "IX_QuizAttempts_SessionId",
            table: "QuizAttempts",
            column: "SessionId");

        migrationBuilder.CreateIndex(
            name: "IX_QuizSessions_QuizId",
            table: "QuizSessions",
            column: "QuizId");

        migrationBuilder.CreateIndex(
            name: "IX_QuizSessions_StartedAt",
            table: "QuizSessions",
            column: "StartedAt");

        migrationBuilder.CreateIndex(
            name: "IX_QuizSessions_Status",
            table: "QuizSessions",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_QuizSessions_UserId",
            table: "QuizSessions",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "QuizAttempts");

        migrationBuilder.DropTable(
            name: "QuizSessions");
    }
}
