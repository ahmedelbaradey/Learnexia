using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Ai.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7_11_AddAiUsageLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageLogs",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaskKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(12,6)", nullable: false),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    WasCacheHit = table.Column<bool>(type: "boolean", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_ModelId",
                schema: "ai",
                table: "AiUsageLogs",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_OccurredAtUtc",
                schema: "ai",
                table: "AiUsageLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLogs_TaskKind",
                schema: "ai",
                table: "AiUsageLogs",
                column: "TaskKind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageLogs",
                schema: "ai");
        }
    }
}
