using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Gamification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitGamification : Migration
    {
        /// <inheritdoc />
        /// Apply: dotnet ef database update --context GamificationDbContext
        ///   --project src/Modules/Gamification/Learnexia.Modules.Gamification.Infrastructure
        ///   --startup-project src/Host/Learnexia.Host
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gamification");

            migrationBuilder.CreateTable(
                name: "StudentXpProfiles",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    TotalXp = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CurrentLevel = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    LastAwardAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_StudentXpProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XpAwards",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentXpProfileId = table.Column<int>(type: "integer", nullable: false),
                    OriginEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    XpAmount = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LessonId = table.Column<int>(type: "integer", nullable: true),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_XpAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpAwards_StudentXpProfiles_StudentXpProfileId",
                        column: x => x.StudentXpProfileId,
                        principalSchema: "gamification",
                        principalTable: "StudentXpProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_StudentXpProfiles_StudentId",
                schema: "gamification",
                table: "StudentXpProfiles",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XpAwards_StudentXpProfileId",
                schema: "gamification",
                table: "XpAwards",
                column: "StudentXpProfileId");

            migrationBuilder.CreateIndex(
                name: "UX_XpAwards_OriginEventId_Reason",
                schema: "gamification",
                table: "XpAwards",
                columns: new[] { "OriginEventId", "Reason" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XpAwards",
                schema: "gamification");

            migrationBuilder.DropTable(
                name: "StudentXpProfiles",
                schema: "gamification");
        }
    }
}
