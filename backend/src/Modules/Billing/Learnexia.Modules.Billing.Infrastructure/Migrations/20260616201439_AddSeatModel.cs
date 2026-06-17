using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PurchasedExtraSeats",
                schema: "billing",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PendingExtraSeatRemovals",
                schema: "billing",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtraSeatCancelEffectiveAt",
                schema: "billing",
                table: "Subscriptions",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncludedSeats",
                schema: "billing",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SeatReservations",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    ChildId = table.Column<int>(type: "integer", nullable: false),
                    ParentUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReservedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
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
                    table.PrimaryKey("PK_SeatReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatReservations_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "billing",
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                column: "IncludedSeats",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "billing",
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                column: "IncludedSeats",
                value: 3);

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_ParentUserId",
                schema: "billing",
                table: "SeatReservations",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SubscriptionId",
                schema: "billing",
                table: "SeatReservations",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "UX_SeatReservations_SubscriptionId_ChildId_Active",
                schema: "billing",
                table: "SeatReservations",
                columns: new[] { "SubscriptionId", "ChildId" },
                unique: true,
                filter: "\"Status\" IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeatReservations",
                schema: "billing");

            migrationBuilder.DropColumn(
                name: "PurchasedExtraSeats",
                schema: "billing",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingExtraSeatRemovals",
                schema: "billing",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ExtraSeatCancelEffectiveAt",
                schema: "billing",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "IncludedSeats",
                schema: "billing",
                table: "Plans");
        }
    }
}
