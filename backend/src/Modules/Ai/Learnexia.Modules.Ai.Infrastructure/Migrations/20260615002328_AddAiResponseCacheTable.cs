using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Ai.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiResponseCacheTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiResponseCache",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CacheKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Response = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    SkillKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: true),
                    CurriculumVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReviewStatus = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    ApprovedBy = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    InvalidatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiResponseCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiResponseCache_CacheKey",
                schema: "ai",
                table: "AiResponseCache",
                column: "CacheKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiResponseCache_QuestionId",
                schema: "ai",
                table: "AiResponseCache",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiResponseCache_SkillKey_CurriculumVersion",
                schema: "ai",
                table: "AiResponseCache",
                columns: new[] { "SkillKey", "CurriculumVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_AiResponseCache_Type_ReviewStatus",
                schema: "ai",
                table: "AiResponseCache",
                columns: new[] { "Type", "ReviewStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiResponseCache",
                schema: "ai");
        }
    }
}
