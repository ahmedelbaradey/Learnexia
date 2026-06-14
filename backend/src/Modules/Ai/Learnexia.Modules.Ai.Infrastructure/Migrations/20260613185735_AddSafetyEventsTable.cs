using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Ai.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSafetyEventsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.CreateTable(
                name: "SafetyEvents",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    TaskKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FailedChecks = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'"),
                    ReasonCodes = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'"),
                    ActionTaken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_ActionTaken",
                schema: "ai",
                table: "SafetyEvents",
                column: "ActionTaken");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_OccurredAtUtc",
                schema: "ai",
                table: "SafetyEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_StudentId",
                schema: "ai",
                table: "SafetyEvents",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SafetyEvents",
                schema: "ai");
        }
    }
}
