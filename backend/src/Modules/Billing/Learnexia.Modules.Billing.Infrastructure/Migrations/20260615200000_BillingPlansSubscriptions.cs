using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BillingPlansSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Plans table ─────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Plans",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    DeletedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Plans_Code",
                schema: "billing",
                table: "Plans",
                column: "Code",
                unique: true);

            // Seed Free and Premium plan rows.
            migrationBuilder.InsertData(
                schema: "billing",
                table: "Plans",
                columns: new[] { "Id", "Code", "Name", "IsActive", "CreatedAt", "CreatedBy" },
                values: new object[,]
                {
                    { 1, 0, "Free",    true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0 },
                    { 2, 1, "Premium", true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0 },
                });

            // ── Subscriptions table ─────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Subscriptions",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentUserId = table.Column<int>(type: "integer", nullable: false),
                    PlanCode = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    BillingPeriod = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CurrentCycleStart = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CurrentCycleEnd = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    PendingPlanCode = table.Column<int>(type: "integer", nullable: true),
                    PendingBillingPeriod = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    DeletedBy = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            // Non-unique index for general lookups by parent.
            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_ParentUserId",
                schema: "billing",
                table: "Subscriptions",
                column: "ParentUserId");

            // Filtered unique index: only ONE Active subscription per parent (Status = 0 = Active).
            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_ParentUserId_Active",
                schema: "billing",
                table: "Subscriptions",
                column: "ParentUserId",
                unique: true,
                filter: "\"Status\" = 0");

            // Status index (grant-job tier-resolution scans Active rows).
            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Status",
                schema: "billing",
                table: "Subscriptions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subscriptions",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Plans",
                schema: "billing");
        }
    }
}
