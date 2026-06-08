using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7_04_AddQuizQuestionSequenceOrderIsActiveAndSoftDeleteFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "learning",
                table: "QuizQuestions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceOrder",
                schema: "learning",
                table: "QuizQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_QuizQuestions_LessonId_SequenceOrder",
                schema: "learning",
                table: "QuizQuestions",
                columns: new[] { "LessonId", "SequenceOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuizQuestions_LessonId_SequenceOrder",
                schema: "learning",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "learning",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "SequenceOrder",
                schema: "learning",
                table: "QuizQuestions");
        }
    }
}
