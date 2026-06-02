using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReengagementInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                schema: "notifications",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValueSql: "6");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "notifications",
                table: "Notifications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValueSql: "'SYSTEM'");

            migrationBuilder.AddColumn<string>(
                name: "Data",
                schema: "notifications",
                table: "Notifications",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<int>(
                name: "DeliveredChannels",
                schema: "notifications",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAtUtc",
                schema: "notifications",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChildReengagementPreferences",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentId = table.Column<int>(type: "integer", nullable: false),
                    ChildId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Push = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InApp = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    QuietHoursStartLocal = table.Column<TimeOnly>(type: "time", nullable: false),
                    QuietHoursEndLocal = table.Column<TimeOnly>(type: "time", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_ChildReengagementPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDeviceTokens",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ExpoPushToken = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_UserDeviceTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ChildReengagementPreferences_ParentId_ChildId_Category",
                schema: "notifications",
                table: "ChildReengagementPreferences",
                columns: new[] { "ParentId", "ChildId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDeviceTokens_UserId",
                schema: "notifications",
                table: "UserDeviceTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_UserDeviceTokens_UserId_ExpoPushToken",
                schema: "notifications",
                table: "UserDeviceTokens",
                columns: new[] { "UserId", "ExpoPushToken" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChildReengagementPreferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "UserDeviceTokens",
                schema: "notifications");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Data",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeliveredChannels",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SentAtUtc",
                schema: "notifications",
                table: "Notifications");
        }
    }
}
