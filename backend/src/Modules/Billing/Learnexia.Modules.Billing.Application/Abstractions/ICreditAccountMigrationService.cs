namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for the clean-cutover migration off the per-child
/// <c>CreditAccount</c> model to the family <c>FamilyEnergyAccount</c> wallet (P10-13-BE-9).
///
/// <para><strong>Option C contract:</strong> Application-layer callers inject THIS interface.
/// All EF / migration logic lives in the Infrastructure implementation.</para>
///
/// <para>This is a one-shot idempotent migration triggered on startup (or by the db-migration
/// agent's EF migration). The logic is in the service, not in the EF migration itself.</para>
/// </summary>
public interface ICreditAccountMigrationService
{
    /// <summary>
    /// Provisions the new wallet model from any existing <c>CreditAccount</c> rows
    /// (idempotent: skip already-migrated families). Asserts no orphan <c>CreditAccount</c>
    /// rows remain after cutover.
    ///
    /// <para><strong>CLEAN CUTOVER (OQ-A):</strong> the system is pre-launch with no real
    /// production data. The migration provisions the new model for any test/seed data present,
    /// then retires the old write-paths. It does NOT reconcile money against live balances.</para>
    ///
    /// <para>Steps per parent:
    /// <list type="number">
    ///   <item>Create one <c>FamilyEnergyAccount</c> (skip if already exists).</item>
    ///   <item>Roll each child's <c>CreditAccount.PurchasedBalance</c> → shared family <c>PurchasedBalance</c>.</item>
    ///   <item>Map each child's <c>CreditAccount.GrantedBalance</c> → current-cycle <c>ChildEnergyAllocation</c>.</item>
    ///   <item>Assert no orphan <c>CreditAccount</c> rows remain.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A summary of families migrated, skipped, and any errors.</returns>
    Task<MigrationResult> MigrateAsync(CancellationToken ct = default);
}

/// <summary>Summary returned by <see cref="ICreditAccountMigrationService.MigrateAsync"/>.</summary>
public sealed record MigrationResult(int Migrated, int Skipped, int Failed, IReadOnlyList<string> Errors);
