using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ICreditAccountMigrationService"/> (P10-13-BE-9).
///
/// <para><strong>CLEAN CUTOVER (OQ-A — pre-launch, no real data):</strong> provisions the new
/// wallet/allocation model for any present test/seed data, then asserts no orphan
/// <c>CreditAccount</c> rows remain. Does NOT reconcile money against live balances
/// (pre-launch invariant; lead override documented in the Execution Plan).</para>
///
/// <para><strong>Idempotent + rerun-safe:</strong> skips parents whose <c>FamilyEnergyAccount</c>
/// already exists. Safe to re-run on every startup or after a partial failure.</para>
///
/// <para><strong>Raw-SQL reads:</strong> The <c>CreditAccount</c> EF entity and table were
/// dropped in migration <c>DropLegacyCreditAccounts</c>. On fresh databases the table is absent
/// and this service returns immediately (zero rows path). On databases still undergoing the
/// migration chain the table is read via raw SQL to avoid any EF entity dependency.</para>
///
/// <para>Per-family steps:
/// <list type="number">
///   <item>Find parent id for each child via <c>IParentChildQuery.FindParentForChildAsync</c>.</item>
///   <item>Create one <c>FamilyEnergyAccount</c> per parent (skip if already exists).</item>
///   <item>Roll each child's <c>CreditAccount.PurchasedBalance</c> → shared family <c>PurchasedBalance</c>.</item>
///   <item>Map each child's <c>CreditAccount.GrantedBalance</c> → that child's current-cycle
///         <c>ChildEnergyAllocation</c> (CycleStart = today 1st; CycleEnd = month end).</item>
///   <item><strong>DELETE</strong> each migrated child's source <c>CreditAccount</c> row inside the
///         SAME transaction (DEFECT-CUTOVER-01 fix). After a successful family migration the source
///         rows are gone, so any remaining rows are truly-UNRESOLVED orphans (children whose parent
///         <c>FindParentForChildAsync</c> could not resolve).</item>
///   <item>Sweep (log + DELETE) any truly-UNRESOLVED orphan <c>CreditAccount</c> rows — pre-launch
///         dead data with no wallet to migrate into (OQ-A). NON-fatal: a perpetual startup throw
///         would just trade DEFECT-CUTOVER-01 for another startup crash and break rerun-safety.</item>
/// </list>
/// </para>
/// </summary>
public sealed class CreditAccountMigrationService : ICreditAccountMigrationService
{
    // ── Raw-SQL DTO used only inside this migration service ───────────────────────
    // CreditAccount entity has been removed from the EF model (DropLegacyCreditAccounts migration).
    // We read the legacy table directly via raw SQL while it still exists on older DBs,
    // or return empty on fresh databases where the table is already gone.
    private sealed record LegacyCreditAccountRow(
        int Id,
        int ChildId,
        int GrantedBalance,
        int PurchasedBalance,
        int DailyUsed,
        string? DailyUsedDateLocal,
        string ChildTimeZoneId);

    private readonly BillingDbContext _db;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public CreditAccountMigrationService(
        BillingDbContext db,
        IParentChildQuery parentChildQuery,
        ICurrentUserService currentUser,
        ILoggerManager logger)
    {
        _db               = db;
        _parentChildQuery = parentChildQuery;
        _currentUser      = currentUser;
        _logger           = logger;
    }

    /// <inheritdoc/>
    public async Task<MigrationResult> MigrateAsync(CancellationToken ct = default)
    {
        _logger.LogInfo("CreditAccountMigrationService: starting clean-cutover migration (OQ-A, pre-launch).");

        var migrated = 0;
        var skipped  = 0;
        var failed   = 0;
        var errors   = new List<string>();

        // Read all CreditAccount rows via raw SQL.
        // On fresh databases the table no longer exists (dropped by DropLegacyCreditAccounts);
        // in that case we catch the exception and treat it as zero rows — nothing to migrate.
        List<LegacyCreditAccountRow> allAccounts;
        try
        {
            allAccounts = await ReadAllCreditAccountsRawAsync(ct);
        }
        catch (Exception ex)
        {
            // The table may not exist on this database (already dropped or fresh install after
            // DropLegacyCreditAccounts). Treat as "nothing to migrate" and return immediately.
            _logger.LogInfo(
                $"CreditAccountMigrationService: billing.CreditAccounts table not accessible " +
                $"({ex.GetType().Name}: {ex.Message}) — treating as empty, nothing to migrate.");
            return new MigrationResult(0, 0, 0, errors);
        }

        if (allAccounts.Count == 0)
        {
            _logger.LogInfo("CreditAccountMigrationService: no CreditAccount rows found — nothing to migrate.");
            return new MigrationResult(0, 0, 0, errors);
        }

        // Resolve parent for each child.
        var childToParent  = new Dictionary<int, int>();
        var orphanChildIds = new List<int>();
        foreach (var account in allAccounts)
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(account.ChildId, ct);
            if (parentId.HasValue)
                childToParent[account.ChildId] = parentId.Value;
            else
            {
                orphanChildIds.Add(account.ChildId);
                _logger.LogInfo($"CreditAccountMigrationService: no parent found for childId={account.ChildId} — orphan child.");
            }
        }

        // Group children by parent.
        var parentGroups = childToParent
            .GroupBy(kv => kv.Value, kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Cycle bounds for current month.
        var nowUtc     = DateTime.UtcNow;
        var cycleStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);

        foreach (var (parentId, childIds) in parentGroups)
        {
            try
            {
                var result = await MigrateFamilyAsync(parentId, childIds, allAccounts, cycleStart, cycleEnd, ct);
                if (result) migrated++;
                else skipped++;
            }
            catch (Exception ex)
            {
                failed++;
                var msg = $"CreditAccountMigrationService: failed for parentId={parentId}: {ex.Message}";
                errors.Add(msg);
                _logger.LogError(ex, msg);
            }
        }

        // Sweep truly-UNRESOLVED orphan CreditAccount rows — children whose parent
        // FindParentForChildAsync could not resolve. Per OQ-A this is pre-launch dead data with NO
        // family wallet to migrate into, so it is logged and DELETED here (it cannot become a wallet
        // balance and must not remain as a split per-child energy path — AC13-8). This is the LAST
        // per-child CreditAccount energy data; after the sweep the table is empty.
        //
        // Deliberately NON-fatal: re-introducing a hard startup throw here would just trade
        // DEFECT-CUTOVER-01 (migration crashes startup) for a different perpetual startup crash and
        // break the "idempotent + rerun-safe" guarantee. We surface orphans loudly via a warning.
        if (orphanChildIds.Count > 0)
        {
            await using var orphanTx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Raw SQL DELETE for orphan rows.
                var childIdsCsv = string.Join(",", orphanChildIds);
                var deleted = await _db.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM billing.\"CreditAccounts\" WHERE \"ChildId\" = ANY(ARRAY[{childIdsCsv}]::int[])", ct);

                await orphanTx.CommitAsync(ct);

                _logger.LogWarn(
                    $"CreditAccountMigrationService: deleted {deleted} ORPHAN CreditAccount row(s) " +
                    $"(no resolvable parent wallet — pre-launch dead data). childIds=[{string.Join(",", orphanChildIds)}].");
            }
            catch
            {
                await orphanTx.RollbackAsync(ct);
                throw;
            }
        }

        // Single-source-of-truth invariant: the per-child CreditAccount energy table is now empty.
        // Read remaining count via raw SQL (entity is no longer in EF model).
        var remainingCount = await CountCreditAccountsRawAsync(ct);
        if (remainingCount > 0)
            _logger.LogWarn(
                $"CreditAccountMigrationService: {remainingCount} CreditAccount row(s) still remain after migration + orphan sweep " +
                "(unexpected — concurrent writer?). The wallet remains the single source of truth for energy.");

        _logger.LogInfo($"CreditAccountMigrationService: complete — migrated={migrated}, skipped={skipped}, failed={failed}, orphansDeleted={orphanChildIds.Count}.");
        return new MigrationResult(migrated, skipped, failed, errors);
    }

    // ── Per-family migration ──────────────────────────────────────────────────────

    /// <summary>Returns true = migrated; false = skipped (already done).</summary>
    private async Task<bool> MigrateFamilyAsync(
        int parentId,
        List<int> childIds,
        IReadOnlyList<LegacyCreditAccountRow> allAccounts,
        DateTime cycleStart,
        DateTime cycleEnd,
        CancellationToken ct)
    {
        // Idempotency: skip if wallet already exists.
        var walletExists = await _db.FamilyEnergyAccounts
            .AnyAsync(w => w.ParentUserId == parentId, ct);

        if (walletExists)
        {
            _logger.LogInfo($"CreditAccountMigrationService: parentId={parentId} already migrated — skipping.");
            return false;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Create the family wallet.
            var wallet = FamilyEnergyAccount.CreateEmpty(parentId);
            _db.FamilyEnergyAccounts.Add(wallet);
            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

            // Accumulate purchased balance from all children → shared family reserve.
            var totalPurchased   = 0;
            var childAllocations = new List<(int ChildId, int GrantedBalance)>();

            foreach (var childId in childIds)
            {
                var account = allAccounts.FirstOrDefault(a => a.ChildId == childId);
                if (account is null) continue;

                totalPurchased += account.PurchasedBalance;
                if (account.GrantedBalance > 0)
                    childAllocations.Add((childId, account.GrantedBalance));
            }

            // Provision family purchased balance.
            // ApplyPurchase already increments PurchasedBalance on the entity AND returns the ledger row.
            // Do NOT add a bare PurchasedBalance += here — that would double-credit (BE-TC-CUTOVER-02).
            if (totalPurchased > 0)
            {
                var purchaseMigrationKey = $"migration-purchased:{parentId}";
                var purchaseTx = wallet.ApplyPurchase(totalPurchased, purchaseMigrationKey);
                purchaseTx.FamilyEnergyAccountId = wallet.Id;
                _db.CreditTransactions.Add(purchaseTx);
                await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            }

            // Provision ChildEnergyAllocation rows from existing GrantedBalance.
            foreach (var (childId, grantedBalance) in childAllocations)
            {
                var allocation = new ChildEnergyAllocation
                {
                    FamilyEnergyAccountId = wallet.Id,
                    ChildId               = childId,
                    CycleStartUtc         = cycleStart,
                    CycleEndUtc           = cycleEnd,
                    AllocatedAmount       = grantedBalance,
                    SpentAmount           = 0,
                };
                _db.ChildEnergyAllocations.Add(allocation);
                await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

                var allocKey = $"migration-alloc:{parentId}:{childId}";
                var allocTx  = allocation.Allocate(grantedBalance, allocKey);
                allocTx.FamilyEnergyAccountId = wallet.Id;
                _db.CreditTransactions.Add(allocTx);
            }

            // Provision ChildDailyUsage rows for all children (carry forward the counter).
            foreach (var childId in childIds)
            {
                var account = allAccounts.FirstOrDefault(a => a.ChildId == childId);
                if (account is null) continue;

                var usageExists = await _db.ChildDailyUsages.AnyAsync(d => d.ChildId == childId, ct);
                if (usageExists) continue;

                var usage = new ChildDailyUsage
                {
                    ChildId            = childId,
                    DailyUsed          = account.DailyUsed,
                    DailyUsedDateLocal = account.DailyUsedDateLocal,
                    ChildTimeZoneId    = account.ChildTimeZoneId ?? "Africa/Cairo",
                };
                _db.ChildDailyUsages.Add(usage);
            }

            // DELETE the migrated source CreditAccount rows inside the SAME transaction
            // (DEFECT-CUTOVER-01 fix). The new wallet/allocation rows are the single source of truth
            // from here on. Leaving the source rows behind would (a) trip the global orphan-guard below
            // even though the family migrated cleanly, and (b) leave a split economy. We delete by
            // ChildId for the children this family owns. Idempotent + rerun-safe: a re-run finds the
            // wallet already present (walletExists short-circuits) and never reaches this path again.
            //
            // Raw SQL DELETE (CreditAccount entity removed from EF model — DropLegacyCreditAccounts).
            if (childIds.Count > 0)
            {
                var childIdsCsv = string.Join(",", childIds);
                var deletedCount = await _db.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM billing.\"CreditAccounts\" WHERE \"ChildId\" = ANY(ARRAY[{childIdsCsv}]::int[])", ct);

                _logger.LogInfo($"CreditAccountMigrationService: migrated parentId={parentId}, " +
                                $"children={childIds.Count}, totalPurchased={totalPurchased}, " +
                                $"deletedSourceAccounts={deletedCount}.");
            }

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── Raw SQL helpers (CreditAccount entity removed from EF model) ──────────────

    /// <summary>
    /// Reads all rows from <c>billing.CreditAccounts</c> via raw SQL.
    /// Throws if the table does not exist (caller converts to empty/no-op path).
    /// </summary>
    private async Task<List<LegacyCreditAccountRow>> ReadAllCreditAccountsRawAsync(CancellationToken ct)
    {
        var results = new List<LegacyCreditAccountRow>();
        var conn    = _db.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT "Id", "ChildId", "GrantedBalance", "PurchasedBalance",
                   "DailyUsed", "DailyUsedDateLocal", "ChildTimeZoneId"
            FROM billing."CreditAccounts"
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new LegacyCreditAccountRow(
                Id:                 reader.GetInt32(0),
                ChildId:            reader.GetInt32(1),
                GrantedBalance:     reader.GetInt32(2),
                PurchasedBalance:   reader.GetInt32(3),
                DailyUsed:          reader.GetInt32(4),
                DailyUsedDateLocal: reader.IsDBNull(5) ? null : reader.GetString(5),
                ChildTimeZoneId:    reader.IsDBNull(6) ? "Africa/Cairo" : reader.GetString(6)));
        }

        return results;
    }

    /// <summary>
    /// Returns the count of remaining rows in <c>billing.CreditAccounts</c>, or 0 if the table
    /// no longer exists.
    /// </summary>
    private async Task<long> CountCreditAccountsRawAsync(CancellationToken ct)
    {
        try
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """SELECT COUNT(*) FROM billing."CreditAccounts" """;
            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result);
        }
        catch
        {
            // Table gone — fresh database running after DropLegacyCreditAccounts.
            return 0;
        }
    }
}
