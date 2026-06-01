using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Gamification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueAndLeagueMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentTier",
                schema: "gamification",
                table: "StudentXpProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Leagues",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GroupIndex = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueMemberships",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    StudentXpProfileId = table.Column<int>(type: "integer", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinOrder = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WeeklyXp = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FinalRank = table.Column<int>(type: "integer", nullable: true),
                    TierAfter = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueMemberships_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalSchema: "gamification",
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeagueMemberships_StudentXpProfiles_StudentXpProfileId",
                        column: x => x.StudentXpProfileId,
                        principalSchema: "gamification",
                        principalTable: "StudentXpProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueXpDeltaLogs",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueMembershipId = table.Column<int>(type: "integer", nullable: false),
                    OriginEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Delta = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueXpDeltaLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueXpDeltaLogs_LeagueMemberships_LeagueMembershipId",
                        column: x => x.LeagueMembershipId,
                        principalSchema: "gamification",
                        principalTable: "LeagueMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMemberships_LeagueId_WeeklyXp_JoinedAtUtc",
                schema: "gamification",
                table: "LeagueMemberships",
                columns: new[] { "LeagueId", "WeeklyXp", "JoinedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueMemberships_StudentXpProfileId_JoinedAtUtc",
                schema: "gamification",
                table: "LeagueMemberships",
                columns: new[] { "StudentXpProfileId", "JoinedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_LeagueMemberships_LeagueId_StudentXpProfileId",
                schema: "gamification",
                table: "LeagueMemberships",
                columns: new[] { "LeagueId", "StudentXpProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LeagueMemberships_PeriodKey_StudentXpProfileId",
                schema: "gamification",
                table: "LeagueMemberships",
                columns: new[] { "PeriodKey", "StudentXpProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_PeriodKey_Tier",
                schema: "gamification",
                table: "Leagues",
                columns: new[] { "PeriodKey", "Tier" });

            migrationBuilder.CreateIndex(
                name: "UX_Leagues_Tier_PeriodKey_GroupIndex",
                schema: "gamification",
                table: "Leagues",
                columns: new[] { "Tier", "PeriodKey", "GroupIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueXpDeltaLogs_LeagueMembershipId",
                schema: "gamification",
                table: "LeagueXpDeltaLogs",
                column: "LeagueMembershipId");

            migrationBuilder.CreateIndex(
                name: "UX_LeagueXpDeltaLogs_LeagueMembershipId_OriginEventId",
                schema: "gamification",
                table: "LeagueXpDeltaLogs",
                columns: new[] { "LeagueMembershipId", "OriginEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueXpDeltaLogs",
                schema: "gamification");

            migrationBuilder.DropTable(
                name: "LeagueMemberships",
                schema: "gamification");

            migrationBuilder.DropTable(
                name: "Leagues",
                schema: "gamification");

            migrationBuilder.DropColumn(
                name: "CurrentTier",
                schema: "gamification",
                table: "StudentXpProfiles");
        }
    }
}
