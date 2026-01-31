// Copyright (c) Microsoft. All rights reserved.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGUIWebChatServer.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    private static readonly string[] s_indexColumns = ["QuizId", "Sequence"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Quizzes",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                Instructions = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Quizzes", x => x.Id));

        migrationBuilder.CreateTable(
            name: "QuestionCards",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                QuizId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                QuestionJson = table.Column<string>(type: "TEXT", nullable: false),
                SelectionJson = table.Column<string>(type: "TEXT", nullable: false),
                CorrectAnswerIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                CorrectAnswerDisplayJson = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_QuestionCards", x => x.Id);
                table.ForeignKey(
                    name: "FK_QuestionCards_Quizzes_QuizId",
                    column: x => x.QuizId,
                    principalTable: "Quizzes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AnswerOptions",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                QuestionCardId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                MediaUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                IsDisabled = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnswerOptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_AnswerOptions_QuestionCards_QuestionCardId",
                    column: x => x.QuestionCardId,
                    principalTable: "QuestionCards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AnswerOptions_QuestionCardId",
            table: "AnswerOptions",
            column: "QuestionCardId");

        migrationBuilder.CreateIndex(
            name: "IX_QuestionCards_QuizId_Sequence",
            table: "QuestionCards",
            columns: s_indexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_Quizzes_CreatedAt",
            table: "Quizzes",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_Quizzes_Title",
            table: "Quizzes",
            column: "Title");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AnswerOptions");

        migrationBuilder.DropTable(
            name: "QuestionCards");

        migrationBuilder.DropTable(
            name: "Quizzes");
    }
}
