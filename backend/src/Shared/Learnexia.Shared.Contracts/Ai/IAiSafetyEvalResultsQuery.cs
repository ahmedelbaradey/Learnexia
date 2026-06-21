namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Cross-module read seam for the P6-02 AI-safety eval run results.
/// Ai.Infrastructure implements; admin dashboard consumers inject.
///
/// <para>Returns the latest committed offline eval run result from the embedded
/// <c>safety-eval-results.json</c> artifact produced by the <c>Ai.EvalTests</c> harness.
/// No DB — the artifact is embedded in <c>Ai.Infrastructure</c> as a resource and read
/// at runtime (Option 1 from the brief §C decision: committed artifact + read-seam, no migration).</para>
///
/// <para>Registered in Ai.Infrastructure DI as Scoped (mirrors <see cref="IPlatformAiSafetyStatsQuery"/>).</para>
///
/// <para>Sentinel-safe: an absent or unreadable artifact returns a zeroed/bootstrap result —
/// never null, never throws.</para>
/// </summary>
public interface IAiSafetyEvalResultsQuery
{
    /// <summary>
    /// Returns the latest offline eval run result.
    /// Returns a sentinel (zeroed/bootstrap) result when no artifact is present.
    /// Never null, never throws.
    /// </summary>
    Task<SafetyEvalRunResult> GetLatestAsync(CancellationToken ct = default);
}

/// <summary>
/// The latest AI-safety eval run result.
/// Produced by the <c>Ai.EvalTests</c> harness and surfaced via <see cref="IAiSafetyEvalResultsQuery"/>.
/// </summary>
/// <param name="RunId">Unique identifier for this eval run (set at harness run time).</param>
/// <param name="RanAtUtc">UTC timestamp when the harness wrote the artifact. Use <c>DateTime.UtcNow</c> — never <c>DateTime.Now</c>.</param>
/// <param name="TotalCases">Total number of eval cases in the run.</param>
/// <param name="PassedCases">Number of cases that passed.</param>
/// <param name="FailedCases">Number of cases that failed.</param>
/// <param name="PassRate">Pass rate as a percentage (0–100).</param>
/// <param name="FailRate">Fail rate as a percentage (0–100).</param>
/// <param name="ThresholdPercent">Configured minimum pass-rate threshold (%).</param>
/// <param name="Breached">True when <see cref="PassRate"/> fell below <see cref="ThresholdPercent"/>.</param>
/// <param name="Tier">Eval tier (e.g. "EvalOffline"; "EvalLive" for the devops launch-gate run).</param>
/// <param name="Note">Human-readable note (breach detail or success message).</param>
/// <param name="ByCheck">Per-check breakdown (check name → passed/total).</param>
/// <param name="BySubject">Per-subject breakdown (subject name → passed/total).</param>
/// <param name="ByLanguage">Per-language breakdown (language code → passed/total).</param>
public record SafetyEvalRunResult(
    Guid RunId,
    DateTime RanAtUtc,
    int TotalCases,
    int PassedCases,
    int FailedCases,
    double PassRate,
    double FailRate,
    double ThresholdPercent,
    bool Breached,
    string Tier,
    string Note,
    IReadOnlyDictionary<string, EvalCheckBreakdown> ByCheck,
    IReadOnlyDictionary<string, EvalCheckBreakdown> BySubject,
    IReadOnlyDictionary<string, EvalCheckBreakdown> ByLanguage)
{
    /// <summary>Sentinel/bootstrap result returned when no artifact is present.</summary>
    public static SafetyEvalRunResult Bootstrap() => new(
        RunId:           Guid.Empty,
        RanAtUtc:        DateTime.MinValue,
        TotalCases:      0,
        PassedCases:     0,
        FailedCases:     0,
        PassRate:        0.0,
        FailRate:        100.0,
        ThresholdPercent: 100.0,
        Breached:        true,
        Tier:            "EvalOffline",
        Note:            "No eval run artifact found. Run: dotnet test --filter Category=EvalOffline",
        ByCheck:         new Dictionary<string, EvalCheckBreakdown>(),
        BySubject:       new Dictionary<string, EvalCheckBreakdown>(),
        ByLanguage:      new Dictionary<string, EvalCheckBreakdown>());
}

/// <summary>Per-dimension (check / subject / language) pass/total breakdown entry.</summary>
/// <param name="Passed">Cases that passed in this dimension.</param>
/// <param name="Total">Total cases in this dimension.</param>
/// <param name="PassRate">Pass rate as a percentage (0–100).</param>
public record EvalCheckBreakdown(int Passed, int Total, double PassRate);
