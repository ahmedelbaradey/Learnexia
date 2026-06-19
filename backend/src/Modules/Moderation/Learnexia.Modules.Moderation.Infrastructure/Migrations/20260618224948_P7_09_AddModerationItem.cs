using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Moderation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7_09_AddModerationItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModerationItems",
                schema: "moderation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ContentReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SafetyVerdict = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    TaskKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Grade = table.Column<int>(type: "integer", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewDecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_ModerationItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationItems_DetectedAt",
                schema: "moderation",
                table: "ModerationItems",
                column: "DetectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationItems_Source",
                schema: "moderation",
                table: "ModerationItems",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationItems_SourceEventId_Unique",
                schema: "moderation",
                table: "ModerationItems",
                column: "SourceEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationItems_Status_DetectedAt",
                schema: "moderation",
                table: "ModerationItems",
                columns: new[] { "Status", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationItems_SubjectCode_Grade",
                schema: "moderation",
                table: "ModerationItems",
                columns: new[] { "SubjectCode", "Grade" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModerationItems",
                schema: "moderation");
        }
    }
}
