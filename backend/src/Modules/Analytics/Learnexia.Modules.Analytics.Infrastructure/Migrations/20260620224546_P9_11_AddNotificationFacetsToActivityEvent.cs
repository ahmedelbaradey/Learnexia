using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Analytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P9_11_AddNotificationFacetsToActivityEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotificationCategory",
                schema: "analytics",
                table: "ActivityEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NotificationChannels",
                schema: "analytics",
                table: "ActivityEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationCode",
                schema: "analytics",
                table: "ActivityEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuppressionReason",
                schema: "analytics",
                table: "ActivityEvents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_EventType_OccurredAtUtc",
                schema: "analytics",
                table: "ActivityEvents",
                columns: new[] { "EventType", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityEvents_EventType_OccurredAtUtc",
                schema: "analytics",
                table: "ActivityEvents");

            migrationBuilder.DropColumn(
                name: "NotificationCategory",
                schema: "analytics",
                table: "ActivityEvents");

            migrationBuilder.DropColumn(
                name: "NotificationChannels",
                schema: "analytics",
                table: "ActivityEvents");

            migrationBuilder.DropColumn(
                name: "NotificationCode",
                schema: "analytics",
                table: "ActivityEvents");

            migrationBuilder.DropColumn(
                name: "SuppressionReason",
                schema: "analytics",
                table: "ActivityEvents");
        }
    }
}
