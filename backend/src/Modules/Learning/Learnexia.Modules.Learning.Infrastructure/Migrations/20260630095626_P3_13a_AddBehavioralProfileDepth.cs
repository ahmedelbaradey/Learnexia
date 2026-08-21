using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P3_13a_AddBehavioralProfileDepth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "GritScore",
                schema: "learning",
                table: "StudentLearningProfiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MasteryVelocity",
                schema: "learning",
                table: "StudentLearningProfiles",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GritScore",
                schema: "learning",
                table: "StudentLearningProfiles");

            migrationBuilder.DropColumn(
                name: "MasteryVelocity",
                schema: "learning",
                table: "StudentLearningProfiles");
        }
    }
}
