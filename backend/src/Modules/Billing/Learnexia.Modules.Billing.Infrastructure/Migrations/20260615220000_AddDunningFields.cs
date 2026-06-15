using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDunningFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedAttemptCount",
                schema: "billing",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "GraceEndsAt",
                schema: "billing",
                table: "Subscriptions",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                schema: "billing",
                table: "Subscriptions",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Status_NextRetryAt",
                schema: "billing",
                table: "Subscriptions",
                columns: new[] { "Status", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_Status_NextRetryAt",
                schema: "billing",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "FailedAttemptCount",
                schema: "billing",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "GraceEndsAt",
                schema: "billing",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                schema: "billing",
                table: "Subscriptions");
        }
    }
}
