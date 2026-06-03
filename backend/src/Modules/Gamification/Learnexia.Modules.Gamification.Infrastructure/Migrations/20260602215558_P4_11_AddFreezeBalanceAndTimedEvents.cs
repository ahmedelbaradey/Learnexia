using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Gamification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P4_11_AddFreezeBalanceAndTimedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreezeBalance",
                schema: "gamification",
                table: "StudentXpProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TimedEvents",
                schema: "gamification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DescriptionEn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DescriptionAr = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Multiplier = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_TimedEvents", x => x.Id);
                    table.CheckConstraint("CK_TimedEvents_DateRange", "\"StartUtc\" < \"EndUtc\"");
                    table.CheckConstraint("CK_TimedEvents_Multiplier", "\"Multiplier\" >= 1.0 AND \"Multiplier\" <= 5.0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudentXpProfiles_FreezeBalance",
                schema: "gamification",
                table: "StudentXpProfiles",
                sql: "\"FreezeBalance\" >= 0 AND \"FreezeBalance\" <= 2");

            migrationBuilder.CreateIndex(
                name: "IX_TimedEvents_IsActive_StartUtc",
                schema: "gamification",
                table: "TimedEvents",
                columns: new[] { "IsActive", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TimedEvents_StartUtc_EndUtc",
                schema: "gamification",
                table: "TimedEvents",
                columns: new[] { "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_TimedEvents_Code",
                schema: "gamification",
                table: "TimedEvents",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimedEvents",
                schema: "gamification");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StudentXpProfiles_FreezeBalance",
                schema: "gamification",
                table: "StudentXpProfiles");

            migrationBuilder.DropColumn(
                name: "FreezeBalance",
                schema: "gamification",
                table: "StudentXpProfiles");
        }
    }
}
