using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentLearningProfileTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentLearningProfiles",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    QuestionTypeAffinity = table.Column<string>(type: "jsonb", nullable: true),
                    RecurringErrorClusters = table.Column<string>(type: "jsonb", nullable: true),
                    AttentionSpanMinutes = table.Column<int>(type: "integer", nullable: true),
                    FatigueSignal = table.Column<string>(type: "jsonb", nullable: true),
                    PreferredExplanationStyle = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DataPointCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastRecomputedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_StudentLearningProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_StudentLearningProfiles_StudentId",
                schema: "learning",
                table: "StudentLearningProfiles",
                column: "StudentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentLearningProfiles",
                schema: "learning");
        }
    }
}
