using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerSplitColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromGranted",
                schema: "billing",
                table: "CreditTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FromPurchased",
                schema: "billing",
                table: "CreditTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromGranted",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropColumn(
                name: "FromPurchased",
                schema: "billing",
                table: "CreditTransactions");
        }
    }
}
