namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Configuration for the Quiz Selection Engine (P3-11-BE-2). Bound from
/// <c>appsettings.json:QuizSelection:Engine</c>.
///
/// Placed in the Domain layer (alongside the pure engine it configures) so that
/// <see cref="QuizSelectionEngine"/> can reference it without violating the
/// Application → Domain dependency direction. Mirrors <see cref="AdaptivityOptions"/>.
///
/// SELECTION POLICY (Q2, P3-11 brief — approved weighted-mix):
///   Given a <c>target</c> difficulty, the engine serves approximately:
///     ~(1 − BlendRatio) fraction from the target bucket,
///     ~BlendRatio        fraction from adjacent difficulty buckets.
///   Per-difficulty ratios (HardRatio / MediumRatio / EasyRatio) tune the
///   within-bucket fraction when the target bucket is hard-dominant.
///   All values default to sensible v1 values; tunable via config without a redeploy.
///
/// Graceful degradation: if the target bucket is empty the engine falls back to the
/// full candidate pool — never returns empty when candidates exist, never throws.
/// </summary>
public sealed class QuizSelectionOptions
{
    /// <summary>Config section key — <c>"QuizSelection:Engine"</c>.</summary>
    public const string SectionName = "QuizSelection:Engine";

    /// <summary>
    /// Fraction of the selected set drawn from the Hard bucket when target = Hard.
    /// Default: 0.7 (70 % Hard / 30 % adjacent).
    /// </summary>
    public double HardRatio { get; set; } = 0.7;

    /// <summary>
    /// Fraction of the selected set drawn from the Medium bucket when target = Medium.
    /// Default: 0.6 (60 % Medium / 40 % adjacent, giving a balanced centre).
    /// </summary>
    public double MediumRatio { get; set; } = 0.6;

    /// <summary>
    /// Fraction of the selected set drawn from the Easy bucket when target = Easy.
    /// Default: 0.7 (70 % Easy / 30 % adjacent — reinforcement-skewed).
    /// </summary>
    public double EasyRatio { get; set; } = 0.7;

    /// <summary>
    /// Fraction of the selected set drawn from adjacent difficulty buckets (the "blend").
    /// Default: 0.3. Overridden implicitly by the per-difficulty ratios above — this value
    /// is the complement (1 − targetRatio) and is exposed explicitly for documentation and
    /// potential future use.
    /// </summary>
    public double BlendRatio { get; set; } = 0.3;
}
