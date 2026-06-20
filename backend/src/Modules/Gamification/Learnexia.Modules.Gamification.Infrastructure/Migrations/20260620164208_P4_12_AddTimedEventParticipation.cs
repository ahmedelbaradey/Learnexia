using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Gamification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P4_12_AddTimedEventParticipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParticipationTarget",
                schema: "gamification",
                table: "TimedEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TimedEventParticipations",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimedEventId = table.Column<int>(type: "integer", nullable: false),
                    StudentXpProfileId = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    JoinedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimedEventParticipations", x => x.Id);
                    table.CheckConstraint("CK_TimedEventParticipations_Progress", "\"Progress\" >= 0 AND \"Progress\" <= \"Target\"");
                    table.ForeignKey(
                        name: "FK_TimedEventParticipations_StudentXpProfiles_StudentXpProfile~",
                        column: x => x.StudentXpProfileId,
                        principalSchema: "gamification",
                        principalTable: "StudentXpProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimedEventParticipations_TimedEvents_TimedEventId",
                        column: x => x.TimedEventId,
                        principalSchema: "gamification",
                        principalTable: "TimedEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimedEvents_ParticipationTarget",
                schema: "gamification",
                table: "TimedEvents",
                sql: "\"ParticipationTarget\" IS NULL OR \"ParticipationTarget\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_TimedEventParticipations_Status_EventEndUtc",
                schema: "gamification",
                table: "TimedEventParticipations",
                columns: new[] { "Status", "EventEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TimedEventParticipations_StudentXpProfileId",
                schema: "gamification",
                table: "TimedEventParticipations",
                column: "StudentXpProfileId");

            migrationBuilder.CreateIndex(
                name: "UX_TimedEventParticipations_TimedEventId_StudentXpProfileId",
                schema: "gamification",
                table: "TimedEventParticipations",
                columns: new[] { "TimedEventId", "StudentXpProfileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimedEventParticipations",
                schema: "gamification");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TimedEvents_ParticipationTarget",
                schema: "gamification",
                table: "TimedEvents");

            migrationBuilder.DropColumn(
                name: "ParticipationTarget",
                schema: "gamification",
                table: "TimedEvents");
        }
    }
}
