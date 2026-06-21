using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P5_07_AddCalibration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlagReason",
                schema: "learning",
                table: "QuizQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualityState",
                schema: "learning",
                table: "QuizQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "QuestionDifficultyStats",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    PValue = table.Column<double>(type: "double precision", nullable: false),
                    AvgTimeSpentSeconds = table.Column<double>(type: "double precision", nullable: false),
                    HintUsageRate = table.Column<double>(type: "double precision", nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
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
                    table.PrimaryKey("PK_QuestionDifficultyStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionDifficultyStats_QuizQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "learning",
                        principalTable: "QuizQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionRecalibrationProposals",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    AuthoredDifficulty = table.Column<int>(type: "integer", nullable: false),
                    ProposedDifficulty = table.Column<int>(type: "integer", nullable: false),
                    EvidencePValue = table.Column<double>(type: "double precision", nullable: false),
                    EvidenceSampleSize = table.Column<int>(type: "integer", nullable: false),
                    ReasonCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_QuestionRecalibrationProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionRecalibrationProposals_QuizQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "learning",
                        principalTable: "QuizQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_QuestionDifficultyStats_QuestionId",
                schema: "learning",
                table: "QuestionDifficultyStats",
                column: "QuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionRecalibrationProposals_QuestionId_Status",
                schema: "learning",
                table: "QuestionRecalibrationProposals",
                columns: new[] { "QuestionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_QuestionRecalibrationProposals_QuestionId_Pending",
                schema: "learning",
                table: "QuestionRecalibrationProposals",
                column: "QuestionId",
                unique: true,
                filter: "\"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionDifficultyStats",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "QuestionRecalibrationProposals",
                schema: "learning");

            migrationBuilder.DropColumn(
                name: "FlagReason",
                schema: "learning",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "QualityState",
                schema: "learning",
                table: "QuizQuestions");
        }
    }
}
