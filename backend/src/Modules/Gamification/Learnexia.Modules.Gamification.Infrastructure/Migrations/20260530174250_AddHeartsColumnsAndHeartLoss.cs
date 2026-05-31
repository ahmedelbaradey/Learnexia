using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Gamification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHeartsColumnsAndHeartLoss : Migration
    {
        // Apply: dotnet ef database update --context GamificationDbContext
        //        --project backend/src/Modules/Gamification/Learnexia.Modules.Gamification.Infrastructure
        //        --startup-project backend/src/Host/Learnexia.Host

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Hearts",
                schema: "gamification",
                table: "StudentXpProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartRefillAtUtc",
                schema: "gamification",
                table: "StudentXpProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HeartLosses",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentXpProfileId = table.Column<int>(type: "integer", nullable: false),
                    OriginEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    HeartsAfter = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_HeartLosses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeartLosses_StudentXpProfiles_StudentXpProfileId",
                        column: x => x.StudentXpProfileId,
                        principalSchema: "gamification",
                        principalTable: "StudentXpProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeartLosses_StudentXpProfileId",
                schema: "gamification",
                table: "HeartLosses",
                column: "StudentXpProfileId");

            migrationBuilder.CreateIndex(
                name: "UX_HeartLosses_OriginEventId",
                schema: "gamification",
                table: "HeartLosses",
                column: "OriginEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeartLosses",
                schema: "gamification");

            migrationBuilder.DropColumn(
                name: "Hearts",
                schema: "gamification",
                table: "StudentXpProfiles");

            migrationBuilder.DropColumn(
                name: "LastHeartRefillAtUtc",
                schema: "gamification",
                table: "StudentXpProfiles");
        }
    }
}
