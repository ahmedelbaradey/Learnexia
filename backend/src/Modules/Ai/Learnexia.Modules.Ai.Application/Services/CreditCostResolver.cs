using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Ai.Application.Services;

/// <summary>
/// Server-side, client-blind resolver that maps each AI Helper intent to its energy cost
/// (in credits) by reading <see cref="IGlobalSettingsProvider"/> with the seeded
/// <c>ai_cost.*</c> string keys.
///
/// <para><strong>Security invariant (P10-03-BE-1):</strong> the client NEVER supplies the cost.
/// Cost is always derived server-side from the intent enum + the global-settings store.
/// Any attempt to pass a cost in a request body is silently ignored.</para>
///
/// <para><strong>Key → default mapping (P10-03 locked economy):</strong>
/// <list type="table">
///   <item><term><c>ai_cost.hint</c></term><description>1 credit (Hint intent)</description></item>
///   <item><term><c>ai_cost.explain_mistake</c></term><description>2 credits (WhyWrong intent)</description></item>
///   <item><term><c>ai_cost.deep_explanation</c></term><description>3 credits (Explain + Simplify intents)</description></item>
///   <item><term><c>ai_cost.practice_generation</c></term><description>5 credits (SimilarExample intent)</description></item>
/// </list>
/// </para>
///
/// <para>Defaults are derived from the plan (<c>BootstrapDefaultGlobalSettingsProvider</c>
/// until P10-12's DB-backed store is warm).</para>
///
/// <para><strong>Module isolation:</strong> this class lives in <c>Ai.Application</c> and references
/// only <c>Shared.Kernel</c> and <c>Shared.Contracts</c> — no <c>Billing.*</c> project dependency
/// (rule 1). The key string constants are inlined here from the locked economy model to avoid
/// introducing a dependency on <c>Billing.Domain</c>.</para>
///
/// <para><strong>HardStopEnabled:</strong> reads <c>Billing:HardStopEnabled</c> from
/// <see cref="IConfiguration"/> (not a managed GlobalSetting — it is a deployment flag).
/// AI handlers use <see cref="IsHardStopEnabled"/> to gate the daily-cap block.</para>
/// </summary>
public sealed class CreditCostResolver
{
    // ── Locked ai_cost.* key strings (mirror GlobalSettingKeys in Billing.Domain — no cross-ref) ──
    // These MUST match the keys seeded in GlobalSettingsSeeder / GlobalSettingKeys.cs exactly.
    private const string KeyHint               = "ai_cost.hint";
    private const string KeyExplainMistake     = "ai_cost.explain_mistake";
    private const string KeyDeepExplanation    = "ai_cost.deep_explanation";
    private const string KeyPracticeGeneration = "ai_cost.practice_generation";
    // P3-14 Lexi recommendation narration (Practice tier — locked 2026-06-18).
    private const string KeyRecommendation     = "ai_cost.recommendation";

    // ── Locked defaults (P10-03 locked economy model) ────────────────────────────
    private const int DefaultHint               = 1;
    private const int DefaultExplainMistake     = 2;
    private const int DefaultDeepExplanation    = 3;
    private const int DefaultPracticeGeneration = 5;
    // P3-14: Recommendation narration is Practice-tier (cost = 5).
    private const int DefaultRecommendation     = 5;

    private readonly IGlobalSettingsProvider _settings;
    private readonly bool _hardStopEnabled;

    public CreditCostResolver(IGlobalSettingsProvider settings, IConfiguration configuration)
    {
        _settings = settings;
        // Use the indexer (not GetValue<T>) to avoid IConfigurationSection.GetChildren()
        // resolution which causes NullReferenceException under simple mock stubs in unit tests.
        var raw = configuration["Billing:HardStopEnabled"];
        _hardStopEnabled = string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the energy cost (in credits) for the given <paramref name="intent"/>.
    /// The cost is resolved server-side from <see cref="IGlobalSettingsProvider"/> and must
    /// never come from client input.
    /// </summary>
    public int ResolveCost(HelperIntent intent) => intent switch
    {
        HelperIntent.Hint            => _settings.GetInt(KeyHint,               DefaultHint),
        HelperIntent.WhyWrong        => _settings.GetInt(KeyExplainMistake,     DefaultExplainMistake),
        HelperIntent.Explain         => _settings.GetInt(KeyDeepExplanation,    DefaultDeepExplanation),
        HelperIntent.SimilarExample  => _settings.GetInt(KeyPracticeGeneration, DefaultPracticeGeneration),
        // P3-14: Recommendation narration (Practice tier).
        HelperIntent.Recommendation  => _settings.GetInt(KeyRecommendation,     DefaultRecommendation),
        _                            => _settings.GetInt(KeyDeepExplanation,    DefaultDeepExplanation),
    };

    /// <summary>
    /// Returns the reason code string for the given intent.
    /// Passed to <see cref="Learnexia.Shared.Contracts.Billing.ICreditSpendService.TryDebitAsync"/>
    /// as the <c>reasonCode</c> parameter, which the Billing module parses into a
    /// <c>CreditReasonCode</c> enum value (enum name must match exactly).
    /// </summary>
    public static string ResolveReasonCode(HelperIntent intent) => intent switch
    {
        HelperIntent.Hint            => "AiHint",
        HelperIntent.WhyWrong        => "AiWhyWrong",
        HelperIntent.Explain         => "AiDeepExplanation",
        HelperIntent.SimilarExample  => "AiPracticeGeneration",
        // P3-14: matches CreditReasonCode.AiRecommendation = 14 enum name exactly.
        HelperIntent.Recommendation  => "AiRecommendation",
        _                            => "AiDeepExplanation",
    };

    /// <summary>
    /// <c>true</c> when <c>Billing:HardStopEnabled</c> is set to <c>true</c> in configuration.
    /// When <c>true</c>, reaching the daily cap BLOCKS the request (hard-stop).
    /// When <c>false</c> (default), the daily cap is soft — warns but allows.
    /// </summary>
    public bool IsHardStopEnabled => _hardStopEnabled;
}
