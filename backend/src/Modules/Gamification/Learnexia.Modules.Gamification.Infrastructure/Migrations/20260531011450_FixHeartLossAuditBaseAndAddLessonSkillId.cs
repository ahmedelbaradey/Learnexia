using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Gamification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixHeartLossAuditBaseAndAddLessonSkillId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "gamification",
                table: "HeartLosses");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "gamification",
                table: "HeartLosses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "gamification",
                table: "HeartLosses");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                schema: "gamification",
                table: "HeartLosses",
                newName: "SkillId");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                schema: "gamification",
                table: "HeartLosses",
                newName: "LessonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SkillId",
                schema: "gamification",
                table: "HeartLosses",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "LessonId",
                schema: "gamification",
                table: "HeartLosses",
                newName: "DeletedBy");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "gamification",
                table: "HeartLosses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "gamification",
                table: "HeartLosses",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "gamification",
                table: "HeartLosses",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
