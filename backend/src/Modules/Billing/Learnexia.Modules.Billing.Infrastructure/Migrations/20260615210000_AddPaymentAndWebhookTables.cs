using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <summary>
    /// P10-06: Adds <c>billing.Payments</c> and <c>billing.WebhookEvents</c> tables.
    ///
    /// Serialization: runs AFTER <c>BillingPlansSubscriptions</c> (P10-05).
    /// Serialization: must run BEFORE <c>AddDunningAndRefundFields</c> (P10-09).
    ///
    /// Security notes:
    /// - No card / PAN / CVV columns: web-hosted checkout only.
    /// - <see cref="WebhookEvents.ProviderEventId"/> has a unique index — the idempotency guard.
    /// - <see cref="Payments.IdempotencyKey"/> has a unique index — prevents duplicate payments.
    /// </summary>
    public partial class AddPaymentAndWebhookTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Payments table ───────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: true),
                    ParentUserId = table.Column<int>(type: "integer", nullable: false),
                    ProviderPaymentRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EGP"),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Kind = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TargetChildId = table.Column<int>(type: "integer", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
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
                    table.PrimaryKey("PK_Payments", x => x.Id);
                });

            // Unique: one payment row per idempotency key (prevents duplicate payments).
            migrationBuilder.CreateIndex(
                name: "UX_Payments_IdempotencyKey",
                schema: "billing",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true);

            // Reconciliation and history queries.
            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderPaymentRef",
                schema: "billing",
                table: "Payments",
                column: "ProviderPaymentRef");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ParentUserId_Status",
                schema: "billing",
                table: "Payments",
                columns: new[] { "ParentUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                schema: "billing",
                table: "Payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SubscriptionId",
                schema: "billing",
                table: "Payments",
                column: "SubscriptionId");

            // ── WebhookEvents table ──────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "WebhookEvents",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderEventId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("PK_WebhookEvents", x => x.Id);
                });

            // THE idempotency guard — unique ProviderEventId prevents double-processing replays.
            migrationBuilder.CreateIndex(
                name: "UX_WebhookEvents_ProviderEventId",
                schema: "billing",
                table: "WebhookEvents",
                column: "ProviderEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_EventType",
                schema: "billing",
                table: "WebhookEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_Succeeded",
                schema: "billing",
                table: "WebhookEvents",
                column: "Succeeded");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookEvents",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "billing");
        }
    }
}
