using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Seeders;

/// <summary>
/// Seeds the 17 managed <c>GlobalSetting</c> rows with bootstrap defaults.
///
/// <para><strong>Seed-if-absent:</strong> existing rows (admin-edited values) are never overwritten.
/// Only rows missing from the table are inserted. This makes the seeder safe to run on every
/// startup (called from <c>BillingModule.InitializeAsync</c>) and idempotent on re-deploy.</para>
///
/// <para>Defaults match the Phase-10 economy spec and the live Ai handler callers:
/// cache confidence=0.85 matches <c>GetHintCommandHandler</c> etc.; practicePoolSize=5
/// matches <c>SimilarExampleCommandHandler</c>; whyWrongVariantCap=50 matches
/// <c>AiResponseCacheRepository</c>.</para>
/// </summary>
public static class GlobalSettingsSeeder
{
    private static readonly (string Key, string Value, GlobalSettingType Type)[] Defaults =
    [
        // Energy grant
        (GlobalSettingKeys.FreeMonthlyCredits,           "100",    GlobalSettingType.Int),
        (GlobalSettingKeys.PremiumMonthlyCredits,        "5000",   GlobalSettingType.Int),
        // Daily soft cap
        (GlobalSettingKeys.FreeDailyCap,                 "10",     GlobalSettingType.Int),
        (GlobalSettingKeys.PremiumDailyCap,              "250",    GlobalSettingType.Int),
        // Pack
        (GlobalSettingKeys.PackPriceEgp,                 "50.00",  GlobalSettingType.Decimal),
        (GlobalSettingKeys.PackSize,                     "1000",   GlobalSettingType.Int),
        // AI action costs (server-resolved — NEVER sent to client)
        (GlobalSettingKeys.AiCostHint,                   "1",      GlobalSettingType.Int),
        (GlobalSettingKeys.AiCostExplainMistake,         "2",      GlobalSettingType.Int),
        (GlobalSettingKeys.AiCostDeepExplanation,        "3",      GlobalSettingType.Int),
        (GlobalSettingKeys.AiCostPracticeGeneration,     "5",      GlobalSettingType.Int),
        // AI cache thresholds — values MUST match live Ai handler defaults exactly
        (GlobalSettingKeys.AiCacheAutoApprovalConfidence,"0.85",   GlobalSettingType.Decimal),
        (GlobalSettingKeys.AiCacheWhyWrongVariantCap,    "50",     GlobalSettingType.Int),
        (GlobalSettingKeys.AiCachePracticePoolSize,      "5",      GlobalSettingType.Int),
        // Subscription pricing
        (GlobalSettingKeys.SubscriptionMonthlyPriceEgp,  "199.00", GlobalSettingType.Decimal),
        (GlobalSettingKeys.SubscriptionAnnualPriceEgp,   "1990.00",GlobalSettingType.Decimal),
        // Payment / FX (placeholder defaults pending OQ-PAY-4/OQ-7/OQ-8 confirmation)
        (GlobalSettingKeys.PaymentProviderFeesPercent,   "2.75",   GlobalSettingType.Decimal),
        (GlobalSettingKeys.FxUsdExchangeRateBuffer,      "1.10",   GlobalSettingType.Decimal),
        // P10-14 Seat configuration (OQ-C / OQ-D — locked 2026-06-16)
        (GlobalSettingKeys.SeatsIncludedFree,            "1",      GlobalSettingType.Int),
        (GlobalSettingKeys.SeatsIncludedPremium,         "3",      GlobalSettingType.Int),
        (GlobalSettingKeys.SeatsMax,                     "5",      GlobalSettingType.Int),
        (GlobalSettingKeys.SeatsExtraPriceEgp,           "169.00", GlobalSettingType.Decimal),
        // P10-15-BE-2: Seat grace window (payment-failure only; default 7 days)
        (GlobalSettingKeys.SeatsGraceDays,               "7",      GlobalSettingType.Int),
    ];

    public static async Task SeedAsync(BillingDbContext db, ILoggerManager logger, CancellationToken ct = default)
    {
        var existingKeys = await db.GlobalSettings
            .AsNoTracking()
            .Select(s => s.Key)
            .ToHashSetAsync(StringComparer.Ordinal, ct);

        var toInsert = Defaults
            .Where(d => !existingKeys.Contains(d.Key))
            .Select(d => GlobalSetting.Create(
                key:       d.Key,
                value:     d.Value,
                type:      d.Type,
                updatedBy: "system",
                updatedAt: DateTime.UtcNow))
            .ToList();

        if (toInsert.Count == 0)
        {
            logger.LogInfo("GlobalSettingsSeeder: all managed keys already present — skipping seed.");
            return;
        }

        await db.GlobalSettings.AddRangeAsync(toInsert, ct);
        await db.SaveChangesAsync(0);
        logger.LogInfo($"GlobalSettingsSeeder: seeded {toInsert.Count} missing settings row(s).");
    }
}
