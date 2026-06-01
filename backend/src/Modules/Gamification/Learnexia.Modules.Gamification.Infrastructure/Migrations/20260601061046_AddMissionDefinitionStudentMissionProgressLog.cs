using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Gamification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionDefinitionStudentMissionProgressLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MissionDefinitions",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IconKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TitleKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Cadence = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    RewardXp = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_MissionDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentMissions",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentXpProfileId = table.Column<int>(type: "integer", nullable: false),
                    MissionDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    MissionType = table.Column<int>(type: "integer", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentMissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentMissions_MissionDefinitions_MissionDefinitionId",
                        column: x => x.MissionDefinitionId,
                        principalSchema: "gamification",
                        principalTable: "MissionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentMissions_StudentXpProfiles_StudentXpProfileId",
                        column: x => x.StudentXpProfileId,
                        principalSchema: "gamification",
                        principalTable: "StudentXpProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionProgressLogs",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentMissionId = table.Column<int>(type: "integer", nullable: false),
                    OriginEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginEventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ProgressDelta = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionProgressLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionProgressLogs_StudentMissions_StudentMissionId",
                        column: x => x.StudentMissionId,
                        principalSchema: "gamification",
                        principalTable: "StudentMissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MissionDefinitions_Code",
                schema: "gamification",
                table: "MissionDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionProgressLogs_StudentMissionId",
                schema: "gamification",
                table: "MissionProgressLogs",
                column: "StudentMissionId");

            migrationBuilder.CreateIndex(
                name: "UX_MissionProgressLogs_StudentMissionId_OriginEventId",
                schema: "gamification",
                table: "MissionProgressLogs",
                columns: new[] { "StudentMissionId", "OriginEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentMissions_MissionDefinitionId",
                schema: "gamification",
                table: "StudentMissions",
                column: "MissionDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMissions_Status_PeriodEndUtc_MissionType",
                schema: "gamification",
                table: "StudentMissions",
                columns: new[] { "Status", "PeriodEndUtc", "MissionType" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentMissions_StudentXpProfileId_Status_PeriodEndUtc",
                schema: "gamification",
                table: "StudentMissions",
                columns: new[] { "StudentXpProfileId", "Status", "PeriodEndUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_StudentMissions_StudentXpProfileId_MissionDefinitionId_PeriodKey",
                schema: "gamification",
                table: "StudentMissions",
                columns: new[] { "StudentXpProfileId", "MissionDefinitionId", "PeriodKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissionProgressLogs",
                schema: "gamification");

            migrationBuilder.DropTable(
                name: "StudentMissions",
                schema: "gamification");

            migrationBuilder.DropTable(
                name: "MissionDefinitions",
                schema: "gamification");
        }
    }
}
