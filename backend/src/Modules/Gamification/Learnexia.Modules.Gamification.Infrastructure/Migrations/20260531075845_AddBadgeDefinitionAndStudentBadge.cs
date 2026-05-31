// Apply: dotnet ef database update --context GamificationDbContext
//        --project backend/src/Modules/Gamification/Learnexia.Modules.Gamification.Infrastructure
//        --startup-project backend/src/Host/Learnexia.Host
// P4-05: Adds BadgeDefinitions catalog table + StudentBadges ledger.
// BadgeDefinition rows populated by BadgeSeeder (not here). No backfill on StudentBadges.

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Gamification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgeDefinitionAndStudentBadge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BadgeDefinitions",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    IconKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rarity = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: true),
                    RewardXp = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_BadgeDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentBadges",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentXpProfileId = table.Column<int>(type: "integer", nullable: false),
                    BadgeDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    OriginEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginEventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AwardedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentBadges_BadgeDefinitions_BadgeDefinitionId",
                        column: x => x.BadgeDefinitionId,
                        principalSchema: "gamification",
                        principalTable: "BadgeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentBadges_StudentXpProfiles_StudentXpProfileId",
                        column: x => x.StudentXpProfileId,
                        principalSchema: "gamification",
                        principalTable: "StudentXpProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_BadgeDefinitions_Code",
                schema: "gamification",
                table: "BadgeDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentBadges_BadgeDefinitionId",
                schema: "gamification",
                table: "StudentBadges",
                column: "BadgeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBadges_StudentXpProfileId",
                schema: "gamification",
                table: "StudentBadges",
                column: "StudentXpProfileId");

            migrationBuilder.CreateIndex(
                name: "UX_StudentBadges_StudentXpProfileId_BadgeDefinitionId",
                schema: "gamification",
                table: "StudentBadges",
                columns: new[] { "StudentXpProfileId", "BadgeDefinitionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentBadges",
                schema: "gamification");

            migrationBuilder.DropTable(
                name: "BadgeDefinitions",
                schema: "gamification");
        }
    }
}
