namespace Learnexia.Modules.Billing.Domain.Constants;

/// <summary>
/// String-key constants for every managed <c>GlobalSetting</c> row.
///
/// <para>All callers (handlers, validators, providers, consumers) MUST reference these constants
/// instead of bare string literals — enforces key consistency and compile-time discoverability.</para>
///
/// <para>Keys verified against live Ai handler usages (P10-12 DRIFT-1 fix — interface unchanged):</para>
/// <list type="bullet">
///   <item><c>ai.cache.autoApprovalConfidence</c> — <c>GetHintCommandHandler</c>, <c>ExplainConceptCommandHandler</c>,
///         <c>SimplifyExplanationCommandHandler</c>, <c>SimilarExampleCommandHandler</c></item>
///   <item><c>ai.cache.practicePoolSize</c> — <c>SimilarExampleCommandHandler</c></item>
///   <item><c>ai.cache.whyWrongVariantCap</c> — <c>AiResponseCacheRepository</c></item>
/// </list>
/// </summary>
public static class GlobalSettingKeys
{
    // ── Energy grant (monthly pool per tier) ─────────────────────────────────
    public const string FreeMonthlyCredits    = "credits.free_monthly";
    public const string PremiumMonthlyCredits = "credits.premium_monthly";

    // ── Daily soft cap ───────────────────────────────────────────────────────
    public const string FreeDailyCap    = "credits.free_daily_cap";
    public const string PremiumDailyCap = "credits.premium_daily_cap";

    // ── Energy pack (one-off purchase) ───────────────────────────────────────
    public const string PackPriceEgp = "credits.pack_price_egp";
    public const string PackSize     = "credits.pack_size";

    // ── Per-action AI costs (server-resolved — client never sees these) ──────
    /// <summary>Cost for the Hint intent. Default = 1.</summary>
    public const string AiCostHint           = "ai_cost.hint";
    /// <summary>Cost for the WhyWrong intent (<c>explain_mistake</c>). Default = 2.</summary>
    public const string AiCostExplainMistake = "ai_cost.explain_mistake";
    /// <summary>Cost for the Explain/Simplify intent (<c>deep_explanation</c>). Default = 3.</summary>
    public const string AiCostDeepExplanation = "ai_cost.deep_explanation";
    /// <summary>Cost for the SimilarExample intent (<c>practice_generation</c>). Default = 5.</summary>
    public const string AiCostPracticeGeneration = "ai_cost.practice_generation";

    // ── AI cache thresholds — MUST match the string literals used by the 4 live Ai handlers ──
    /// <summary>
    /// Used by all 4 Ai handlers: <c>_settings.GetDecimal("ai.cache.autoApprovalConfidence", 0.85m)</c>.
    /// Key string must remain <em>exactly</em> <c>"ai.cache.autoApprovalConfidence"</c>.
    /// </summary>
    public const string AiCacheAutoApprovalConfidence = "ai.cache.autoApprovalConfidence";
    /// <summary>
    /// Used by <c>AiResponseCacheRepository</c>: <c>_settings.GetInt("ai.cache.whyWrongVariantCap", 50)</c>.
    /// </summary>
    public const string AiCacheWhyWrongVariantCap     = "ai.cache.whyWrongVariantCap";
    /// <summary>
    /// Used by <c>SimilarExampleCommandHandler</c>: <c>_settings.GetInt("ai.cache.practicePoolSize", 5)</c>.
    /// </summary>
    public const string AiCachePracticePoolSize       = "ai.cache.practicePoolSize";

    // ── Subscription pricing ─────────────────────────────────────────────────
    public const string SubscriptionMonthlyPriceEgp = "subscription.monthly_price_egp";
    public const string SubscriptionAnnualPriceEgp  = "subscription.annual_price_egp";

    // ── Payment / FX ─────────────────────────────────────────────────────────
    public const string PaymentProviderFeesPercent = "payment.provider_fees";
    public const string FxUsdExchangeRateBuffer    = "fx.usd_exchange_rate_buffer";

    // ── Dunning retry policy ─────────────────────────────────────────────────────
    /// <summary>Maximum number of retry attempts before entering final grace/downgrade. Default = 3.</summary>
    public const string DunningMaxRetries    = "dunning.max_retries";
    /// <summary>Hours between dunning retry attempts. Default = 24.</summary>
    public const string DunningRetryIntervalHours = "dunning.retry_interval_hours";

    // ── Allowlist: all keys that are admin-editable (validator + update command use this) ──────
    public static readonly IReadOnlySet<string> ManagedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        FreeMonthlyCredits,
        PremiumMonthlyCredits,
        FreeDailyCap,
        PremiumDailyCap,
        PackPriceEgp,
        PackSize,
        AiCostHint,
        AiCostExplainMistake,
        AiCostDeepExplanation,
        AiCostPracticeGeneration,
        AiCacheAutoApprovalConfidence,
        AiCacheWhyWrongVariantCap,
        AiCachePracticePoolSize,
        SubscriptionMonthlyPriceEgp,
        SubscriptionAnnualPriceEgp,
        PaymentProviderFeesPercent,
        FxUsdExchangeRateBuffer,
        DunningMaxRetries,
        DunningRetryIntervalHours,
    };
}
