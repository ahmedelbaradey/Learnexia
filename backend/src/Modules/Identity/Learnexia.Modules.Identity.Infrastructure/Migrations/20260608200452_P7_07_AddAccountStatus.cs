using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7_07_AddAccountStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountStatus",
                schema: "identity",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Governance lifecycle state (0=Active, 1=Suspended, 2=Deleted). Distinct from IsActive (mechanical gate) and LockoutEnd (auto-lockout P1-13). DB DEFAULT 0 backfills all existing rows to Active.");

            migrationBuilder.AddColumn<string>(
                name: "LastStatusReason",
                schema: "identity",
                table: "AspNetUsers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Admin-supplied reason for the most recent status change (suspend/delete). Max 500 chars.");

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedAtUtc",
                schema: "identity",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true,
                comment: "UTC timestamp of the last status change. Persist as DateTime.UtcNow (Npgsql EnableLegacyTimestampBehavior=true — apply .ToUniversalTime() at mapping boundaries).");

            migrationBuilder.AddColumn<int>(
                name: "StatusChangedBy",
                schema: "identity",
                table: "AspNetUsers",
                type: "integer",
                nullable: true,
                comment: "UserId of the admin who performed the last status change. Plain int — no FK.");

            migrationBuilder.CreateIndex(
                name: "IX_Users_AccountStatus_Performance",
                schema: "identity",
                table: "AspNetUsers",
                column: "AccountStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_AccountStatus_Performance",
                schema: "identity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AccountStatus",
                schema: "identity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastStatusReason",
                schema: "identity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                schema: "identity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StatusChangedBy",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
