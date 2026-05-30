using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Gamification.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Apply: dotnet ef database update --context GamificationDbContext
    //        --project backend/src/Modules/Gamification/Learnexia.Modules.Gamification.Infrastructure
    //        --startup-project backend/src/Host/Learnexia.Host
    public partial class AddStreakColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentStreak",
                schema: "gamification",
                table: "StudentXpProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastActivityDateUtc",
                schema: "gamification",
                table: "StudentXpProfiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LongestStreak",
                schema: "gamification",
                table: "StudentXpProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentStreak",
                schema: "gamification",
                table: "StudentXpProfiles");

            migrationBuilder.DropColumn(
                name: "LastActivityDateUtc",
                schema: "gamification",
                table: "StudentXpProfiles");

            migrationBuilder.DropColumn(
                name: "LongestStreak",
                schema: "gamification",
                table: "StudentXpProfiles");
        }
    }
}
