using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatGraceAndLockState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeatGraceReason",
                schema: "billing",
                table: "Subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SeatGraceStartedAt",
                schema: "billing",
                table: "Subscriptions",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "billing",
                table: "SeatReservations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovalScheduledAt",
                schema: "billing",
                table: "SeatReservations",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatState",
                schema: "billing",
                table: "SeatReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SeatLedgerEntries",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    ParentUserId = table.Column<int>(type: "integer", nullable: false),
                    ChildId = table.Column<int>(type: "integer", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    DeletedBy = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatLedgerEntries_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "billing",
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_SeatReservations_IdempotencyKey",
                schema: "billing",
                table: "SeatReservations",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeatLedgerEntries_OccurredAt",
                schema: "billing",
                table: "SeatLedgerEntries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_SeatLedgerEntries_SubscriptionId",
                schema: "billing",
                table: "SeatLedgerEntries",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatLedgerEntries_SubscriptionId_ChildId",
                schema: "billing",
                table: "SeatLedgerEntries",
                columns: new[] { "SubscriptionId", "ChildId" });

            migrationBuilder.CreateIndex(
                name: "UX_SeatLedgerEntries_IdempotencyKey",
                schema: "billing",
                table: "SeatLedgerEntries",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeatLedgerEntries",
                schema: "billing");

            migrationBuilder.DropIndex(
                name: "UX_SeatReservations_IdempotencyKey",
                schema: "billing",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "SeatGraceReason",
                schema: "billing",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "SeatGraceStartedAt",
                schema: "billing",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "billing",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "RemovalScheduledAt",
                schema: "billing",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "SeatState",
                schema: "billing",
                table: "SeatReservations");
        }
    }
}
