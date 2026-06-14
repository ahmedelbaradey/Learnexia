using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttemptServedDifficultyMix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServedDifficultyMix",
                schema: "learning",
                table: "Attempts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetDifficulty",
                schema: "learning",
                table: "Attempts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServedDifficultyMix",
                schema: "learning",
                table: "Attempts");

            migrationBuilder.DropColumn(
                name: "TargetDifficulty",
                schema: "learning",
                table: "Attempts");
        }
    }
}
