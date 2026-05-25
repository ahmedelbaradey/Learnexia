using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttemptQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StudentAnswers_AttemptId_QuestionId",
                schema: "learning",
                table: "StudentAnswers",
                columns: new[] { "AttemptId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_StudentId_Status",
                schema: "learning",
                table: "Attempts",
                columns: new[] { "StudentId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentAnswers_AttemptId_QuestionId",
                schema: "learning",
                table: "StudentAnswers");

            migrationBuilder.DropIndex(
                name: "IX_Attempts_StudentId_Status",
                schema: "learning",
                table: "Attempts");
        }
    }
}
