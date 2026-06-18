using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParentPauseStateToChildSeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentPauseState",
                schema: "billing",
                table: "SeatReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ParentPauseStateChangedAt",
                schema: "billing",
                table: "SeatReservations",
                type: "timestamptz",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentPauseState",
                schema: "billing",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "ParentPauseStateChangedAt",
                schema: "billing",
                table: "SeatReservations");
        }
    }
}
