using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Parent.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeeklyReport",
                schema: "parent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChildId = table.Column<int>(type: "integer", nullable: false, comment: "Id of the child (student) this report belongs to. Plain int — no cross-module FK."),
                    WeekStartUtc = table.Column<DateTime>(type: "timestamptz", nullable: false, comment: "Monday 00:00 UTC of the report week. Combined with ChildId forms the unique per-week key."),
                    XpEarned = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "XP earned by the child during the report week."),
                    SkillsImproved = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Count of distinct skills whose mastery improved during the report week."),
                    WeakAreasJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'", comment: "JSON snapshot of the child's weak areas as of the end of the report week (jsonb)."),
                    RecommendationsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'", comment: "JSON array of recommendations generated for the child based on the week's weak areas (jsonb)."),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false, comment: "UTC timestamp when this report row was generated or last regenerated."),
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
                    table.PrimaryKey("PK_WeeklyReport", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReport_ChildId",
                schema: "parent",
                table: "WeeklyReport",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReport_ChildId_WeekStartUtc",
                schema: "parent",
                table: "WeeklyReport",
                columns: new[] { "ChildId", "WeekStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyReport",
                schema: "parent");
        }
    }
}
