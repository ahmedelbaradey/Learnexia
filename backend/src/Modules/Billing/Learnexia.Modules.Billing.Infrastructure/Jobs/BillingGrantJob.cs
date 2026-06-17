using Hangfire;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Billing.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs at 01:00 UTC on the 1st of every month (<c>"0 1 1 * *"</c>).
///
/// <para><strong>P10-13 rewrite (per-family):</strong> the old per-child model is replaced.
/// For every PARENT that has at least one active child, the job:
/// <list type="number">
///   <item>Resolves the active seat info via <see cref="ISeatQuery.GetActiveSeatsAsync"/>
///         (temp impl in P10-13: all linked children = active seats).</item>
///   <item>Reads the per-seat energy grant from <see cref="IGlobalSettingsProvider"/>:
///         <c>credits.free_monthly</c> / <c>credits.premium_monthly</c> — now interpreted
///         <strong>PER SEAT</strong> (OQ-L). Grant = <c>PlanEnergyPerSeat × ActivePaidSeats</c>.</item>
///   <item>Calls <see cref="IFamilyEnergyAllocationService.AllocateSubscriptionGrantAsync"/> once
///         per family — deposits to the wallet and equal-splits across active-seat children.</item>
/// </list>
/// NO per-child grant path remains. The old <c>CreditAccount.ApplyGrant/ExpireGrant</c> is not called.
/// </para>
///
/// <para><strong>Idempotency:</strong> <see cref="IFamilyEnergyAllocationService"/> is idempotent
/// on <c>(parentUserId, cycleStart)</c>. Re-running for the same period never double-grants.</para>
///
/// <para><strong>Safety:</strong>
/// <list type="bullet">
///   <item><c>[DisableConcurrentExecution]</c> prevents overlapping runs.</item>
///   <item>Per-family fail-soft: an exception on one family is caught, logged, and the loop continues.</item>
///   <item>Paged 500-row batches bound memory usage.</item>
/// </list>
/// </para>
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
        _settings     = settings;
        _clock        = clock;
        _logger       = logger;
    }

    /// <summary>Executes the monthly per-family grant sweep.</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        var nowUtc = _clock.UtcNow;

        // Cycle boundaries.
        var cycleStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        var cycleId    = nowUtc.ToString("yyyyMM");

        // Read per-seat energy amounts from GlobalSettings (OQ-L: reuse existing keys, now per-seat).
        var freePerSeat    = _settings.GetInt(GlobalSettingKeys.FreeMonthlyCredits, 100);
        var premiumPerSeat = _settings.GetInt(GlobalSettingKeys.PremiumMonthlyCredits, 5000);

        _logger.LogInfo($"BillingGrantJob (P10-13 per-family): starting cycle={cycleId}, " +
                        $"freePerSeat={freePerSeat}, premiumPerSeat={premiumPerSeat}.");

        // Build the set of unique parents by finding each child's parent.
        // We use IBillingSubscriptionContract to list active children (existing seam),
        // then reverse-lookup each child's parent via IParentChildQuery.
        IReadOnlyList<ActiveChildPlanDto> allChildren;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var subscriptionContract = scope.ServiceProvider.GetRequiredService<IBillingSubscriptionContract>();
            allChildren = await subscriptionContract.GetActiveChildrenWithTierAsync(ct);
        }

        if (allChildren.Count == 0)
        {
            _logger.LogInfo("BillingGrantJob: no active children found — nothing to do.");
            return;
        }

        // Group children by parent (reverse-lookup via IParentChildQuery.FindParentForChildAsync).
        var parentToChildTier = new Dictionary<int, (PlanTier Tier, int ChildCount)>();
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var parentChildQuery = scope.ServiceProvider.GetRequiredService<IParentChildQuery>();
            foreach (var child in allChildren)
            {
                var parentId = await parentChildQuery.FindParentForChildAsync(child.ChildId, ct);
                if (parentId is null) continue;
                if (!parentToChildTier.ContainsKey(parentId.Value))
                    parentToChildTier[parentId.Value] = (child.PlanTier, 0);
                var existing = parentToChildTier[parentId.Value];
                parentToChildTier[parentId.Value] = (existing.Tier, existing.ChildCount + 1);
            }
        }

        var parentIds  = parentToChildTier.Keys.ToList();
        int granted = 0, skipped = 0, failed = 0;
        var pageIndex = 0;

        while (true)
        {
            var page = parentIds
                .Skip(pageIndex * PageSize)
                .Take(PageSize)
                .ToList();

            if (page.Count == 0) break;

            foreach (var parentId in page)
            {
                try
                {
                    var (tier, _) = parentToChildTier[parentId];
                    var perSeat   = tier == PlanTier.Premium ? premiumPerSeat : freePerSeat;

                    var result = await ProcessFamilyAsync(parentId, perSeat, cycleStart, cycleEnd, cycleId, ct);
                    if (result == 1) granted++;
                    else skipped++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, $"BillingGrantJob: failed for parentId={parentId}.");
                }
            }

            pageIndex++;
            if (page.Count < PageSize) break;
        }

        _logger.LogInfo($"BillingGrantJob: complete — granted={granted}, skipped={skipped}, failed={failed}, cycle={cycleId}.");
    }

    // ── Per-family processing ─────────────────────────────────────────────────────

    /// <summary>Returns 1 = granted (or no-children), 0 = skipped (already done).</summary>
    private async Task<int> ProcessFamilyAsync(
        int parentId,
        int perSeatAmount,
        DateTime cycleStart,
        DateTime cycleEnd,
        string cycleId,
        CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var seatQuery         = scope.ServiceProvider.GetRequiredService<ISeatQuery>();
        var allocationService = scope.ServiceProvider.GetRequiredService<IFamilyEnergyAllocationService>();

        var seatInfo = await seatQuery.GetActiveSeatsAsync(parentId, ct);
        if (seatInfo.ActivePaidSeats == 0)
        {
            _logger.LogInfo($"BillingGrantJob: no active seats for parentId={parentId} — skipping.");
            return 1; // not a failure
        }

        var grantAmount    = perSeatAmount * seatInfo.ActivePaidSeats;
        var idempotencyKey = $"family-grant:{parentId}:{cycleId}";

        var result = await allocationService.AllocateSubscriptionGrantAsync(
            parentUserId  : parentId,
            grantAmount   : grantAmount,
            cycleStart    : cycleStart,
            cycleEnd      : cycleEnd,
            idempotencyKey: idempotencyKey,
            ct            : ct);

        if (result.WasDuplicate)
        {
            _logger.LogInfo($"BillingGrantJob: already allocated for parentId={parentId}, cycle={cycleId} — skipping.");
            return 0;
        }

        _logger.LogInfo($"BillingGrantJob: allocated grant={grantAmount} " +
                        $"(perSeat={perSeatAmount}×seats={seatInfo.ActivePaidSeats}) " +
                        $"to {result.ChildCount} children for parentId={parentId}, cycle={cycleId}.");
        return 1;
    }
}
