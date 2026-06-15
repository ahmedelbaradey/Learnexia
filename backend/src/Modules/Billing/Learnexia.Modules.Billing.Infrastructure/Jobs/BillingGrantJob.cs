using Hangfire;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Billing.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs at 01:00 UTC on the 1st of every month (<c>"0 1 1 * *"</c>).
///
/// <para><strong>Purpose:</strong> for every active child, expire the prior cycle's unused granted
/// balance and apply the new monthly energy grant. The grant amount is tier-resolved from
/// <see cref="IGlobalSettingsProvider"/> (keys <c>credits.free_monthly</c> /
/// <c>credits.premium_monthly</c>) at run time so an admin can change the economy without a
/// redeployment.</para>
///
/// <para><strong>Idempotency:</strong> the per-child idempotency key scheme
/// (<c>grant:{childId}:{yyyyMM}</c> / <c>expire:{childId}:{priorYyyyMM}</c>) combined with the
/// <c>UX_CreditTransactions_IdempotencyKey</c> unique constraint makes every run a no-op for
/// children already processed. Re-running for the same period (e.g. after a Hangfire retry or
/// manual re-trigger) never double-grants.</para>
///
/// <para><strong>Safety:</strong>
/// <list type="bullet">
///   <item><c>[DisableConcurrentExecution]</c> prevents overlapping runs if a job is delayed.</item>
///   <item>Per-child fail-soft: an exception on one child is caught, logged, and the loop continues.</item>
///   <item>Paged 500-row batches bound memory usage.</item>
///   <item>Expire-before-grant in one per-child explicit transaction: unused granted energy is zeroed
///         then the new grant is added — purchased energy is never touched.</item>
/// </list>
/// </para>
///
/// <para>Mirrors <c>StreakSweepJob</c> in <c>Gamification.Infrastructure.Jobs</c>.</para>
/// </summary>
public sealed class BillingGrantJob
{
    private const int PageSize = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public BillingGrantJob(
        IServiceScopeFactory scopeFactory,
        IGlobalSettingsProvider settings,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Executes the monthly grant sweep.
    /// <c>[DisableConcurrentExecution]</c> prevents two instances from overlapping.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        var nowUtc = _clock.UtcNow;

        // Cycle id = yyyyMM of the month being granted.
        var cycleId = nowUtc.ToString("yyyyMM");
        // Prior cycle = one month back.
        var priorCycleId = nowUtc.AddMonths(-1).ToString("yyyyMM");
        // Cycle end = last instant of the current month in UTC.
        var cycleEndUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(1)
            .AddSeconds(-1);

        // Amounts from GlobalSettings (read once per run to be consistent across the batch).
        var freeAmount = _settings.GetInt(GlobalSettingKeys.FreeMonthlyCredits, 100);
        var premiumAmount = _settings.GetInt(GlobalSettingKeys.PremiumMonthlyCredits, 5000);

        int granted = 0, skipped = 0, failed = 0;
        int pageIndex = 0;

        _logger.LogInfo($"BillingGrantJob: starting cycle={cycleId}, priorCycle={priorCycleId}, " +
                        $"freeAmount={freeAmount}, premiumAmount={premiumAmount}.");

        while (true)
        {
            IReadOnlyList<ActiveChildPlanDto> page;

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var subscription = scope.ServiceProvider.GetRequiredService<IBillingSubscriptionContract>();
                // The stub returns all known children; when P10-05 lands this returns real tier info.
                var all = await subscription.GetActiveChildrenWithTierAsync(ct);

                page = all
                    .Skip(pageIndex * PageSize)
                    .Take(PageSize)
                    .ToList()
                    .AsReadOnly();
            }

            if (page.Count == 0)
                break;

            foreach (var child in page)
            {
                try
                {
                    var result = await ProcessChildAsync(
                        child,
                        cycleId,
                        priorCycleId,
                        cycleEndUtc,
                        child.PlanTier == PlanTier.Premium ? premiumAmount : freeAmount,
                        ct);

                    if (result == 1) granted++;
                    else skipped++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, $"BillingGrantJob: failed for childId={child.ChildId}.");
                }
            }

            pageIndex++;

            // If the page was smaller than the page size we've read everything.
            if (page.Count < PageSize)
                break;
        }

        _logger.LogInfo($"BillingGrantJob: complete — granted={granted}, skipped(already processed)={skipped}, failed={failed}, cycle={cycleId}.");
    }

    // ── Per-child processing ──────────────────────────────────────────────────────

    /// <summary>Returns 1 = granted, 0 = skipped (already done / unique-violation).</summary>
    private async Task<int> ProcessChildAsync(
        ActiveChildPlanDto child,
        string cycleId,
        string priorCycleId,
        DateTime cycleEndUtc,
        int amount,
        CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        // ── Idempotency pre-check (cheap) ─────────────────────────────────────────
        var grantKey = $"grant:{child.ChildId}:{cycleId}";

        if (await db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == grantKey, ct))
            return 0; // already processed this period

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        try
        {
            // Load or create the credit account.
            var account = await db.CreditAccounts
                .FirstOrDefaultAsync(a => a.ChildId == child.ChildId, ct);

            if (account is null)
            {
                account = CreditAccount.CreateEmpty(child.ChildId, child.ChildTimeZoneId);
                await db.CreditAccounts.AddAsync(account, ct);
                // Flush so account.Id is populated before we add the transaction.
                await db.SaveChangesAsync(0);
            }

            // ── Step 1: Expire prior cycle's granted balance (idempotent) ────────────
            var expireKey = $"expire:{child.ChildId}:{priorCycleId}";
            var alreadyExpired = await db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == expireKey, ct);

            if (!alreadyExpired && account.GrantedBalance > 0)
            {
                var expireTx = account.ExpireGrant(expireKey);
                await db.CreditTransactions.AddAsync(expireTx, ct);
            }

            // ── Step 2: Apply the new grant ───────────────────────────────────────────
            var reasonCode = child.PlanTier == PlanTier.Premium
                ? CreditReasonCode.MonthlyGrantPremium
                : CreditReasonCode.MonthlyGrantFree;

            var grantTx = account.ApplyGrant(amount, cycleEndUtc, reasonCode, grantKey);
            await db.CreditTransactions.AddAsync(grantTx, ct);

            await db.SaveChangesAsync(0);
            await tx.CommitAsync(ct);

            return 1; // successfully granted
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            // Racing concurrent run wrote the same idempotency key — treat as already done.
            await tx.RollbackAsync(ct);
            return 0; // skip / idempotent
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_CreditTransactions_IdempotencyKey",
               StringComparison.OrdinalIgnoreCase) == true;
}
