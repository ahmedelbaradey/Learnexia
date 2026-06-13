using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentSkillMasteryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentSkillMasteries",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    MasteryPercentage = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AttemptsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastPracticedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    ReviewIntervalDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NextReviewDueAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    RepetitionNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    DeletedBy = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSkillMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentSkillMasteries_Skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "learning",
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillMasteries_SkillId",
                schema: "learning",
                table: "StudentSkillMasteries",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillMasteries_StudentId",
                schema: "learning",
                table: "StudentSkillMasteries",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UX_StudentSkillMasteries_StudentId_SkillId",
                schema: "learning",
                table: "StudentSkillMasteries",
                columns: new[] { "StudentId", "SkillId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentSkillMasteries",
                schema: "learning");
        }
    }
}
