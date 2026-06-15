using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "CreditAccounts",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChildId = table.Column<int>(type: "integer", nullable: false),
                    GrantedBalance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PurchasedBalance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    GrantExpiresAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    DailyUsed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DailyUsedDateLocal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ChildTimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Africa/Cairo"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
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
                    table.PrimaryKey("PK_CreditAccounts", x => x.Id);
                    table.CheckConstraint("CK_CreditAccounts_GrantedBalance_NonNegative", "\"GrantedBalance\" >= 0");
                    table.CheckConstraint("CK_CreditAccounts_PurchasedBalance_NonNegative", "\"PurchasedBalance\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "CreditTransactions",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditAccountId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Pool = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    ReasonCode = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResultingGrantedBalance = table.Column<int>(type: "integer", nullable: false),
                    ResultingPurchasedBalance = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RelatedActionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RelatedPaymentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_CreditTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_CreditAccounts_CreditAccountId",
                        column: x => x.CreditAccountId,
                        principalSchema: "billing",
                        principalTable: "CreditAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditAccounts_ChildId",
                schema: "billing",
                table: "CreditAccounts",
                column: "ChildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_AccountId_OccurredAtUtc",
                schema: "billing",
                table: "CreditTransactions",
                columns: new[] { "CreditAccountId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_Type",
                schema: "billing",
                table: "CreditTransactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "UX_CreditTransactions_IdempotencyKey",
                schema: "billing",
                table: "CreditTransactions",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditTransactions",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CreditAccounts",
                schema: "billing");
        }
    }
}
