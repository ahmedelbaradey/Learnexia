using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyCreditAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditTransactions_CreditAccounts_CreditAccountId",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropTable(
                name: "CreditAccounts",
                schema: "billing");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditAccounts",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChildId = table.Column<int>(type: "integer", nullable: false),
                    ChildTimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Africa/Cairo"),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    DailyUsed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DailyUsedDateLocal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    DeletedBy = table.Column<int>(type: "integer", nullable: true),
                    GrantExpiresAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    GrantedBalance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true),
                    PurchasedBalance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditAccounts", x => x.Id);
                    table.CheckConstraint("CK_CreditAccounts_GrantedBalance_NonNegative", "\"GrantedBalance\" >= 0");
                    table.CheckConstraint("CK_CreditAccounts_PurchasedBalance_NonNegative", "\"PurchasedBalance\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditAccounts_ChildId",
                schema: "billing",
                table: "CreditAccounts",
                column: "ChildId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditTransactions_CreditAccounts_CreditAccountId",
                schema: "billing",
                table: "CreditTransactions",
                column: "CreditAccountId",
                principalSchema: "billing",
                principalTable: "CreditAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
