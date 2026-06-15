using FluentValidation;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.GlobalSettings.Commands.UpdateGlobalSetting;

/// <summary>
/// FluentValidation validator for <see cref="UpdateGlobalSettingCommand"/>.
/// Auto-discovered by <c>ValidationBehavior</c> (command is <c>ICommand&lt;T&gt;</c>).
///
/// <para>Rules:
/// <list type="bullet">
///   <item>Key must be in <see cref="GlobalSettingKeys.ManagedKeys"/> allowlist.</item>
///   <item>NewValue must not be null/empty.</item>
///   <item>NewValue must be parseable as the key's declared type.</item>
///   <item>Per-key range rules (non-negative costs, confidence ∈ [0,1], etc.).</item>
///   <item>Cross-key: <c>free_daily_cap ≤ free_monthly</c> and <c>premium_daily_cap ≤ premium_monthly</c>
///         (reads the related key direct-from-DB to avoid stale-cache during batch updates).</item>
/// </list>
/// </para>
/// </summary>
public sealed class UpdateGlobalSettingValidator : AbstractValidator<UpdateGlobalSettingCommand>
{
    private readonly IBillingDbContext _db;

    public UpdateGlobalSettingValidator(IBillingDbContext db)
    {
        _db = db;

        RuleFor(x => x.Key)
            .NotEmpty()
            .WithMessage(SharedResourcesKey.GlobalSettingKeyRequired)
            .Must(k => GlobalSettingKeys.ManagedKeys.Contains(k))
            .WithMessage(SharedResourcesKey.GlobalSettingKeyNotAllowed);

        RuleFor(x => x.NewValue)
            .NotEmpty()
            .WithMessage(SharedResourcesKey.GlobalSettingValueRequired);

        // Type-parse + range rules — only when the key is in the allowlist (short-circuit otherwise).
        RuleFor(x => x)
            .Must(cmd => IsValueParseable(cmd.Key, cmd.NewValue))
            .When(cmd => GlobalSettingKeys.ManagedKeys.Contains(cmd.Key) && !string.IsNullOrEmpty(cmd.NewValue))
            .WithMessage(SharedResourcesKey.GlobalSettingValueTypeMismatch)
            .OverridePropertyName("NewValue");

        RuleFor(x => x)
            .Must(cmd => IsValueInRange(cmd.Key, cmd.NewValue))
            .When(cmd => GlobalSettingKeys.ManagedKeys.Contains(cmd.Key) && !string.IsNullOrEmpty(cmd.NewValue))
            .WithMessage(SharedResourcesKey.GlobalSettingValueOutOfRange)
            .OverridePropertyName("NewValue");

        // Cross-key: free_daily_cap ≤ free_monthly (DB direct read to avoid stale cache).
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                if (!int.TryParse(cmd.NewValue, out var newCap)) return false;
                var monthlyRow = await _db.GlobalSettings.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == GlobalSettingKeys.FreeMonthlyCredits, ct);
                if (monthlyRow is null || !int.TryParse(monthlyRow.Value, out var monthly)) return true;
                return newCap <= monthly;
            })
            .When(cmd => cmd.Key == GlobalSettingKeys.FreeDailyCap && !string.IsNullOrEmpty(cmd.NewValue))
            .WithMessage(SharedResourcesKey.GlobalSettingDailyCapExceedsMonthly)
            .OverridePropertyName("NewValue");

        // Cross-key: premium_daily_cap ≤ premium_monthly.
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
            {
                if (!int.TryParse(cmd.NewValue, out var newCap)) return false;
                var monthlyRow = await _db.GlobalSettings.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == GlobalSettingKeys.PremiumMonthlyCredits, ct);
                if (monthlyRow is null || !int.TryParse(monthlyRow.Value, out var monthly)) return true;
                return newCap <= monthly;
            })
            .When(cmd => cmd.Key == GlobalSettingKeys.PremiumDailyCap && !string.IsNullOrEmpty(cmd.NewValue))
            .WithMessage(SharedResourcesKey.GlobalSettingDailyCapExceedsMonthly)
            .OverridePropertyName("NewValue");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static bool IsValueParseable(string key, string value)
    {
        var type = GetDeclaredType(key);
        return type switch
        {
            GlobalSettingType.Int     => int.TryParse(value, out _),
            GlobalSettingType.Decimal => decimal.TryParse(value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _),
            GlobalSettingType.Bool    => bool.TryParse(value, out _),
            _                         => true, // String — always parseable.
        };
    }

    private static bool IsValueInRange(string key, string value)
    {
        switch (key)
        {
            case GlobalSettingKeys.AiCostHint:
            case GlobalSettingKeys.AiCostExplainMistake:
            case GlobalSettingKeys.AiCostDeepExplanation:
            case GlobalSettingKeys.AiCostPracticeGeneration:
            case GlobalSettingKeys.AiCacheWhyWrongVariantCap:
            case GlobalSettingKeys.AiCachePracticePoolSize:
                return int.TryParse(value, out var intVal) && intVal >= 0;

            case GlobalSettingKeys.FreeMonthlyCredits:
            case GlobalSettingKeys.PremiumMonthlyCredits:
            case GlobalSettingKeys.FreeDailyCap:
            case GlobalSettingKeys.PremiumDailyCap:
            case GlobalSettingKeys.PackSize:
                return int.TryParse(value, out var posInt) && posInt > 0;

            case GlobalSettingKeys.PackPriceEgp:
            case GlobalSettingKeys.SubscriptionMonthlyPriceEgp:
            case GlobalSettingKeys.SubscriptionAnnualPriceEgp:
            case GlobalSettingKeys.PaymentProviderFeesPercent:
            case GlobalSettingKeys.FxUsdExchangeRateBuffer:
                return decimal.TryParse(value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var decVal) && decVal >= 0m;

            case GlobalSettingKeys.AiCacheAutoApprovalConfidence:
                return decimal.TryParse(value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var conf) && conf >= 0m && conf <= 1m;

            default:
                return true;
        }
    }

    internal static GlobalSettingType GetDeclaredType(string key) => key switch
    {
        GlobalSettingKeys.FreeMonthlyCredits           => GlobalSettingType.Int,
        GlobalSettingKeys.PremiumMonthlyCredits        => GlobalSettingType.Int,
        GlobalSettingKeys.FreeDailyCap                 => GlobalSettingType.Int,
        GlobalSettingKeys.PremiumDailyCap              => GlobalSettingType.Int,
        GlobalSettingKeys.PackSize                     => GlobalSettingType.Int,
        GlobalSettingKeys.AiCostHint                   => GlobalSettingType.Int,
        GlobalSettingKeys.AiCostExplainMistake         => GlobalSettingType.Int,
        GlobalSettingKeys.AiCostDeepExplanation        => GlobalSettingType.Int,
        GlobalSettingKeys.AiCostPracticeGeneration     => GlobalSettingType.Int,
        GlobalSettingKeys.AiCacheWhyWrongVariantCap    => GlobalSettingType.Int,
        GlobalSettingKeys.AiCachePracticePoolSize      => GlobalSettingType.Int,
        GlobalSettingKeys.PackPriceEgp                 => GlobalSettingType.Decimal,
        GlobalSettingKeys.SubscriptionMonthlyPriceEgp  => GlobalSettingType.Decimal,
        GlobalSettingKeys.SubscriptionAnnualPriceEgp   => GlobalSettingType.Decimal,
        GlobalSettingKeys.PaymentProviderFeesPercent   => GlobalSettingType.Decimal,
        GlobalSettingKeys.FxUsdExchangeRateBuffer      => GlobalSettingType.Decimal,
        GlobalSettingKeys.AiCacheAutoApprovalConfidence => GlobalSettingType.Decimal,
        _                                               => GlobalSettingType.String,
    };
}
