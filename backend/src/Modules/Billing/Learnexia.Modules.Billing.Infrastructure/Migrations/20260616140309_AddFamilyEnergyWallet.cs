using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyEnergyWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditTransactions_CreditAccounts_CreditAccountId",
                schema: "billing",
                table: "CreditTransactions");

            // IX_Subscriptions_ParentUserId and UX_Subscriptions_ParentUserId_Active already
            // exist in the DB (created by BillingPlansSubscriptions). The prior model snapshot
            // was missing IX_Subscriptions_ParentUserId; no DDL change needed here.

            migrationBuilder.AlterColumn<int>(
                name: "CreditAccountId",
                schema: "billing",
                table: "CreditTransactions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ChildEnergyAllocationId",
                schema: "billing",
                table: "CreditTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                schema: "billing",
                table: "CreditTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FamilyEnergyAccountId",
                schema: "billing",
                table: "CreditTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceBucket",
                schema: "billing",
                table: "CreditTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChildDailyUsages",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChildId = table.Column<int>(type: "integer", nullable: false),
                    DailyUsed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DailyUsedDateLocal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ChildTimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Africa/Cairo"),
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
                    table.PrimaryKey("PK_ChildDailyUsages", x => x.Id);
                    table.CheckConstraint("CK_ChildDailyUsages_DailyUsed_NonNegative", "\"DailyUsed\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "FamilyEnergyAccounts",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentUserId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionBalance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SubscriptionExpiresAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    PurchasedBalance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_FamilyEnergyAccounts", x => x.Id);
                    table.CheckConstraint("CK_FamilyEnergyAccounts_PurchasedBalance_NonNegative", "\"PurchasedBalance\" >= 0");
                    table.CheckConstraint("CK_FamilyEnergyAccounts_SubscriptionBalance_NonNegative", "\"SubscriptionBalance\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ChildEnergyAllocations",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyEnergyAccountId = table.Column<int>(type: "integer", nullable: false),
                    ChildId = table.Column<int>(type: "integer", nullable: false),
                    CycleStartUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CycleEndUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    AllocatedAmount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SpentAmount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_ChildEnergyAllocations", x => x.Id);
                    table.CheckConstraint("CK_ChildEnergyAllocations_AllocatedAmount_NonNegative", "\"AllocatedAmount\" >= 0");
                    table.CheckConstraint("CK_ChildEnergyAllocations_SpentAmount_NonNegative", "\"SpentAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_ChildEnergyAllocations_FamilyEnergyAccounts_FamilyEnergyAcc~",
                        column: x => x.FamilyEnergyAccountId,
                        principalSchema: "billing",
                        principalTable: "FamilyEnergyAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_ChildEnergyAllocationId",
                schema: "billing",
                table: "CreditTransactions",
                column: "ChildEnergyAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_FamilyEnergyAccountId",
                schema: "billing",
                table: "CreditTransactions",
                column: "FamilyEnergyAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_ChildDailyUsages_ChildId",
                schema: "billing",
                table: "ChildDailyUsages",
                column: "ChildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChildEnergyAllocations_ChildId",
                schema: "billing",
                table: "ChildEnergyAllocations",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "UX_ChildEnergyAllocations_FamilyChildCycle",
                schema: "billing",
                table: "ChildEnergyAllocations",
                columns: new[] { "FamilyEnergyAccountId", "ChildId", "CycleStartUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FamilyEnergyAccounts_ParentUserId",
                schema: "billing",
                table: "FamilyEnergyAccounts",
                column: "ParentUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditTransactions_ChildEnergyAllocations_ChildEnergyAlloca~",
                schema: "billing",
                table: "CreditTransactions",
                column: "ChildEnergyAllocationId",
                principalSchema: "billing",
                principalTable: "ChildEnergyAllocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditTransactions_CreditAccounts_CreditAccountId",
                schema: "billing",
                table: "CreditTransactions",
                column: "CreditAccountId",
                principalSchema: "billing",
                principalTable: "CreditAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditTransactions_FamilyEnergyAccounts_FamilyEnergyAccount~",
                schema: "billing",
                table: "CreditTransactions",
                column: "FamilyEnergyAccountId",
                principalSchema: "billing",
                principalTable: "FamilyEnergyAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditTransactions_ChildEnergyAllocations_ChildEnergyAlloca~",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditTransactions_CreditAccounts_CreditAccountId",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditTransactions_FamilyEnergyAccounts_FamilyEnergyAccount~",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropTable(
                name: "ChildDailyUsages",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "ChildEnergyAllocations",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "FamilyEnergyAccounts",
                schema: "billing");

            migrationBuilder.DropIndex(
                name: "IX_CreditTransactions_ChildEnergyAllocationId",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CreditTransactions_FamilyEnergyAccountId",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropColumn(
                name: "ChildEnergyAllocationId",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropColumn(
                name: "FamilyEnergyAccountId",
                schema: "billing",
                table: "CreditTransactions");

            migrationBuilder.DropColumn(
                name: "SourceBucket",
                schema: "billing",
                table: "CreditTransactions");

            // IX_Subscriptions_ParentUserId stays — it was not added by this migration.

            migrationBuilder.AlterColumn<int>(
                name: "CreditAccountId",
                schema: "billing",
                table: "CreditTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditTransactions_CreditAccounts_CreditAccountId",
                schema: "billing",
                table: "CreditTransactions",
                column: "CreditAccountId",
                principalSchema: "billing",
                principalTable: "CreditAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
