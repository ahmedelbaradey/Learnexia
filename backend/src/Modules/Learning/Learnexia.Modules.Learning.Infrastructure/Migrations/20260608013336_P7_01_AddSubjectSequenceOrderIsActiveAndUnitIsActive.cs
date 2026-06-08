using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7_01_AddSubjectSequenceOrderIsActiveAndUnitIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "learning",
                table: "Units",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "learning",
                table: "Subjects",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceOrder",
                schema: "learning",
                table: "Subjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_GradeId_SequenceOrder",
                schema: "learning",
                table: "Subjects",
                columns: new[] { "GradeId", "SequenceOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subjects_GradeId_SequenceOrder",
                schema: "learning",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "learning",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "learning",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "SequenceOrder",
                schema: "learning",
                table: "Subjects");
        }
    }
}
