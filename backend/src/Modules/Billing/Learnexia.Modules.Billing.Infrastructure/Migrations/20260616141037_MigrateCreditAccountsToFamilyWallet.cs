using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateCreditAccountsToFamilyWallet : Migration
    {
        // ── SCHEMA-ONLY migration (AC13-8, SECURITY HIGH-2 fix) ──────────────────
        //
        // DATA PROVISIONING HAS BEEN REMOVED FROM THIS MIGRATION.
        //
        // The original implementation used CreditAccount.CreatedBy as a ParentUserId proxy,
        // which is WRONG — CreatedBy is an audit/actor column, not the parent-child FK.
        // Correct parent resolution requires IParentChildQuery.FindParentForChildAsync (C#).
        //
        // All data provisioning is performed ONCE, idempotently, at startup by
        // CreditAccountMigrationService.MigrateAsync(), which is wired in
        // BillingModule.InitializeAsync() AFTER db.Database.MigrateAsync().
        //
        // This migration now only records the schema change in the __EFMigrationsHistory
        // table so the migration history is consistent; no SQL data writes are performed.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No data provisioning here. See CreditAccountMigrationService for the correct
            // parent-aware cutover logic (IParentChildQuery → FindParentForChildAsync).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data migrations are not reversible without re-deriving the original
            // CreditAccount state. Down() is a no-op — do not attempt data reversal.
            // Structural reversal (dropping the wallet tables) is handled by
            // AddFamilyEnergyWallet.Down().
        }
    }
}
