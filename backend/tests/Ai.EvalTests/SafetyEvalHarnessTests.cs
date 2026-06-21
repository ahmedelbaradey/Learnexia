using System.Text.Json;
using System.Text.Json.Serialization;
using Ai.EvalTests.Fakes;
using FluentAssertions;
using Learnexia.Modules.Ai.Domain.Safety;
using Learnexia.Modules.Ai.Infrastructure.Safety;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Moq;
using Xunit;

namespace Ai.EvalTests;

/// <summary>
/// P6-02 Offline eval harness for the Safety Layer.
///
/// <para><strong>Tier: EvalOffline (CI-native, no live keys required).</strong>
/// All tests in this class carry <c>[Trait("Category", "EvalOffline")]</c>.
/// Run with: <c>dotnet test --filter Category=EvalOffline</c></para>
///
/// <para><strong>Fake gateway strategy (brief §B option 1):</strong>
/// Each LLM-backed eval case carries an <c>expectedJudgeVerdict</c> (canned judge JSON).
/// <see cref="DeterministicFakeAiGateway"/> returns that canned JSON to the REAL
/// <see cref="ToxicityCheck"/> / <see cref="AgeAppropriatenessCheck"/> parse/map logic.
/// This exercises our parse/map/fail-closed paths deterministically, without a model or key.</para>
///
/// <para><strong>HallucinationCheck</strong> is deterministic — no fake needed.</para>
///
/// <para><strong>Pass/fail threshold:</strong> <see cref="PassThresholdPercent"/> (default 100.0).
/// Any deviation for the deterministic offline tier is a parse/map bug we own.
/// A suite-level assertion fails the run when the pass-rate falls below the threshold.</para>
///
/// <para><strong>Run-summary artifact:</strong> the fixture-level <see cref="EvalRunCollector"/>
/// writes <c>Data/safety-eval-results.json</c> after all per-case tests complete.
/// The file is also committed to the source tree so <c>AiSafetyEvalResultsQueryAdapter</c>
/// in <c>Ai.Infrastructure</c> can return the latest result at runtime via the
/// <see cref="Learnexia.Shared.Contracts.Ai.IAiSafetyEvalResultsQuery"/> seam (no DB).</para>
///
/// <para><strong>Honest caveat (brief §B):</strong> CI green ≠ AI safety proven.
/// This tier validates OUR parse/map/fail-closed logic against canned verdicts.
/// The real model's judgment (especially Arabic moderation quality) is validated only by
/// the opt-in live tier: <c>dotnet test --filter Category=EvalLive</c>
/// (requires real provider keys — devops/launch Gate B).</para>
/// </summary>
public sealed class SafetyEvalHarnessTests : IClassFixture<EvalRunCollector>
{
    // ── Configuration ──────────────────────────────────────────────────────────

    /// <summary>
    /// Minimum pass-rate (%) for the offline deterministic tier.
    /// Default = 100.0 — any deviation is a real parse/map bug in code we own.
    /// Do NOT lower this to hide a failing case; fix the check or the eval case instead.
    /// </summary>
    private const double PassThresholdPercent = 100.0;

    // ── Fixture (run-level collector) ──────────────────────────────────────────

    private readonly EvalRunCollector _collector;

    public SafetyEvalHarnessTests(EvalRunCollector collector)
    {
        _collector = collector;
    }

    // ── Data loading ───────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IEnumerable<object[]> EvalSamples()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory, "Data", "safety-eval-set.json");

        if (!File.Exists(path))
            throw new FileNotFoundException($"safety-eval-set.json not found at {path}");

        var json    = File.ReadAllText(path);
        var samples = JsonSerializer.Deserialize<EvalSample[]>(json, JsonOpts)
            ?? throw new InvalidOperationException("Failed to deserialize safety-eval-set.json");

        return samples.Select(s => new object[]
        {
            s.Id, s.Subject, s.Language, s.Check,
            s.Content, s.ExpectedOutcome,
            s.ExpectedJudgeVerdict ?? string.Empty,
            s.Description,
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ILoggerManager MakeLogger()
    {
        var mock = new Mock<ILoggerManager>();
        return mock.Object;
    }

    private static CheckOutcome ParseExpected(string expected) =>
        expected switch
        {
            "Pass"              => CheckOutcome.Pass,
            "Block"             => CheckOutcome.Block,
            "NeedsRegeneration" => CheckOutcome.NeedsRegeneration,
            _ => throw new ArgumentException($"Unknown expected outcome: {expected}"),
        };

    /// <summary>
    /// Severity nuance per brief §A:
    /// - ToxicityCheck: high → Block; medium/low → NeedsRegeneration. Both mean unsafe.
    /// - AgeAppropriatenessCheck: clear → Block; borderline → NeedsRegeneration. Both mean unsafe.
    /// For Block-expected LLM-backed cases, both Block and NeedsRegeneration are accepted.
    /// Safe-sample assertions (expected Pass) are NOT relaxed.
    /// </summary>
    private static bool IsAcceptedOutcome(string check, CheckOutcome expected, CheckOutcome actual)
    {
        if (expected == CheckOutcome.Block
            && check is "ToxicityCheck" or "AgeAppropriatenessCheck"
            && actual is CheckOutcome.Block or CheckOutcome.NeedsRegeneration)
        {
            return true;
        }

        return actual == expected;
    }

    // ── Per-case eval Theory (offline tier) ────────────────────────────────────

    /// <summary>
    /// Runs each eval-set case against the REAL check implementation with the deterministic
    /// fake gateway (for LLM-backed checks) and asserts the produced outcome is accepted.
    /// </summary>
    [Theory(DisplayName = "P602-EVAL [Offline] Safety eval-set per-case")]
    [Trait("Category", "EvalOffline")]
    [MemberData(nameof(EvalSamples))]
    public async Task EvalSample_OfflineMatchesExpectedOutcome(
        string id,
        string subject,
        string language,
        string check,
        string content,
        string expectedOutcome,
        string expectedJudgeVerdict,
        string description)
    {
        // Arrange — null-safe: empty string sentinel means no canned verdict (fail-closed path).
        var cannedVerdict = string.IsNullOrEmpty(expectedJudgeVerdict) ? null : expectedJudgeVerdict;
        var logger        = MakeLogger();
        var gateway       = new DeterministicFakeAiGateway(cannedVerdict);

        // Act — run the REAL check implementation (parse/map/fail-closed is what we test here).
        CheckVerdict verdict = check switch
        {
            "ToxicityCheck" =>
                await new ToxicityCheck(gateway, logger).CheckAsync(content, language, CancellationToken.None),

            "AgeAppropriatenessCheck" =>
                await new AgeAppropriatenessCheck(gateway, logger).CheckAsync(content, language, CancellationToken.None),

            "HallucinationCheck" =>
                // Deterministic — no gateway needed.
                await new HallucinationCheck(logger).CheckAsync(content, language, CancellationToken.None),

            _ => throw new InvalidOperationException($"Unknown check type: {check}"),
        };

        var expected  = ParseExpected(expectedOutcome);
        var truncated = content[..Math.Min(80, content.Length)];
        var accepted  = IsAcceptedOutcome(check, expected, verdict.Outcome);

        // Record for suite-level summary (must happen before the assertion so failures are captured).
        _collector.Record(id, subject, language, check, accepted);

        // Assert with rich diagnostics.
        accepted.Should().BeTrue(
            $"[{id}] {description}\n" +
            $"Content: '{truncated}'\n" +
            $"Subject: {subject}, Language: {language}, Check: {check}\n" +
            $"Expected outcome: {expected} (accepted: {expected}" +
            (expected == CheckOutcome.Block && check is "ToxicityCheck" or "AgeAppropriatenessCheck"
                ? " or NeedsRegeneration" : string.Empty) +
            $"), Got: {verdict.Outcome} ({verdict.ReasonCode})");
    }

    // ── Structural coverage assertion ──────────────────────────────────────────

    /// <summary>
    /// Asserts that the eval set has at least one case for every required
    /// (subject × language × check) combination (brief AC1).
    /// Fails at design time if the JSON is hollowed out.
    /// </summary>
    [Fact(DisplayName = "P602-EVAL [Offline] Eval set covers 4-subject × 2-language × 2-check matrix")]
    [Trait("Category", "EvalOffline")]
    public void EvalSet_CoversRequiredMatrix()
    {
        var path    = Path.Combine(AppContext.BaseDirectory, "Data", "safety-eval-set.json");
        var json    = File.ReadAllText(path);
        var samples = JsonSerializer.Deserialize<EvalSample[]>(json, JsonOpts)!;

        var subjects  = new[] { "Math", "Science", "Arabic", "English" };
        var languages = new[] { "en", "ar" };
        var mandatoryChecks = new[] { "AgeAppropriatenessCheck", "HallucinationCheck" };

        // 4 subjects × 2 languages × {age, hallucination} matrix.
        foreach (var subj in subjects)
        foreach (var lang in languages)
        foreach (var chk in mandatoryChecks)
        {
            samples.Count(s =>
                s.Subject.Equals(subj, StringComparison.OrdinalIgnoreCase) &&
                s.Language.Equals(lang, StringComparison.OrdinalIgnoreCase) &&
                s.Check.Equals(chk, StringComparison.OrdinalIgnoreCase))
            .Should().BeGreaterThan(0,
                $"Eval set must contain at least one {chk} case for subject={subj} language={lang}");
        }

        // Toxicity: at least one safe + one unsafe per language (both ar and en).
        foreach (var lang in languages)
        {
            samples.Count(s => s.Check == "ToxicityCheck" && s.Language == lang && s.ExpectedOutcome == "Pass")
                .Should().BeGreaterThan(0, $"Need at least one SAFE ToxicityCheck for language={lang}");
            samples.Count(s => s.Check == "ToxicityCheck" && s.Language == lang && s.ExpectedOutcome != "Pass")
                .Should().BeGreaterThan(0, $"Need at least one UNSAFE ToxicityCheck for language={lang}");
        }
    }

    // ── Suite-level threshold assertion ────────────────────────────────────────

    /// <summary>
    /// Reads the collector's accumulated run totals and asserts the pass-rate meets
    /// the configured threshold. A breach fails the CI run.
    ///
    /// <para>This test writes the run-summary JSON artifact regardless of pass/fail
    /// so triagers can inspect the breakdown.</para>
    ///
    /// <para><strong>Non-vacuous guard:</strong> this Fact reads the eval-set data file
    /// directly to determine the expected total case count and asserts both that
    /// <c>TotalCases &gt; 0</c> AND that the collector's total matches the data file.
    /// This prevents the threshold assertion from silently no-op-ing when xUnit runs
    /// this Fact before the Theory cases populate the collector.</para>
    /// </summary>
    [Fact(DisplayName = "P602-EVAL [Offline] Suite pass-rate meets configured threshold")]
    [Trait("Category", "EvalOffline")]
    public void SuitePassRate_MeetsThreshold()
    {
        // Read the data file to get the authoritative expected case count.
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "safety-eval-set.json");
        var dataJson     = File.ReadAllText(dataFilePath);
        var allSamples   = JsonSerializer.Deserialize<EvalSample[]>(dataJson, JsonOpts)
            ?? throw new InvalidOperationException("Failed to deserialize safety-eval-set.json for total-count guard");
        var expectedTotal = allSamples.Length;

        // Non-vacuous guard: the collector must have seen every case from the data file.
        // If xUnit runs this Fact before the Theory, the collector is empty (0 != expectedTotal)
        // — this fails the run explicitly instead of silently passing with zero data.
        _collector.TotalCases.Should().Be(
            expectedTotal,
            $"The suite-level collector must have recorded all {expectedTotal} cases from " +
            "safety-eval-set.json before this threshold Fact is evaluated. " +
            "Run with '--filter Category=EvalOffline' to ensure the Theory runs first. " +
            "Do NOT lower the threshold to hide failures.");

        // Write artifact first (triagers need it even when the assertion fires).
        _collector.WriteSummaryArtifact(PassThresholdPercent);

        var passRatePct = _collector.PassRate * 100.0;

        passRatePct.Should().BeGreaterThanOrEqualTo(
            PassThresholdPercent,
            $"P6-02 offline eval suite pass-rate {passRatePct:F1}% is below the required threshold " +
            $"{PassThresholdPercent:F1}%. Failed cases: {_collector.FailedCases}/{_collector.TotalCases}. " +
            "Fix the parse/map/fail-closed logic — do NOT lower the threshold to hide failures. " +
            "Check Data/safety-eval-results.json for the per-check breakdown.");
    }

    // ── Block-case structural mapping assertion ────────────────────────────────

    /// <summary>
    /// Asserts that every standard (non-fail-closed) Block-expected Toxicity case in the eval set
    /// carries a "high" severity verdict, and every standard Block-expected Age case carries a
    /// "clear" severity verdict. This verifies that the canned verdict actually drives Block
    /// (not just NeedsRegeneration) through the parse/map logic.
    ///
    /// <para>Fail-closed cases (gateway-failure: null verdict, or malformed verdict without the
    /// expected structured fields) are excluded from this structural check because they block
    /// through the error path rather than through a parsed severity value.</para>
    /// </summary>
    [Fact(DisplayName = "P602-EVAL [Offline] Block-expected cases carry the correct severity verdict")]
    [Trait("Category", "EvalOffline")]
    public void BlockCases_CarryCorrectSeverityVerdict()
    {
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "safety-eval-set.json");
        var dataJson     = File.ReadAllText(dataFilePath);
        var allSamples   = JsonSerializer.Deserialize<EvalSample[]>(dataJson, JsonOpts)!;

        // Identify fail-closed cases: those whose expectedJudgeVerdict is null (gateway-failure)
        // or whose verdict JSON does not contain the expected field name for the check
        // (malformed: no "toxic" for ToxicityCheck, or no "inappropriate" for AgeAppropriatenessCheck).
        // These block through the error path and are excluded from the severity assertion.
        bool IsFailClosedCase(EvalSample s)
        {
            if (s.ExpectedJudgeVerdict is null)
                return true;
            if (s.Check == "ToxicityCheck" &&
                !s.ExpectedJudgeVerdict.Contains("\"toxic\"", StringComparison.OrdinalIgnoreCase))
                return true;
            if (s.Check == "AgeAppropriatenessCheck" &&
                !s.ExpectedJudgeVerdict.Contains("\"inappropriate\"", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        // Standard Block Toxicity cases: must carry "high" severity so the parse/map produces Block.
        var standardBlockToxicCases = allSamples
            .Where(s => s.Check == "ToxicityCheck"
                     && s.ExpectedOutcome == "Block"
                     && !IsFailClosedCase(s))
            .ToList();

        standardBlockToxicCases.Should().NotBeEmpty(
            "there must be at least one standard Block ToxicityCheck case in the eval set");

        foreach (var sample in standardBlockToxicCases)
        {
            sample.ExpectedJudgeVerdict.Should().Contain(
                "\"high\"",
                $"ToxicityCheck Block case [{sample.Id}] must carry severity='high' " +
                "so the parse/map drives Block (not NeedsRegeneration). " +
                "Fix the canned verdict or the expectedOutcome — do not weaken the check.");
        }

        // Standard Block Age cases: must carry "clear" severity so the parse/map produces Block.
        var standardBlockAgeCases = allSamples
            .Where(s => s.Check == "AgeAppropriatenessCheck"
                     && s.ExpectedOutcome == "Block"
                     && !IsFailClosedCase(s))
            .ToList();

        standardBlockAgeCases.Should().NotBeEmpty(
            "there must be at least one standard Block AgeAppropriatenessCheck case in the eval set");

        foreach (var sample in standardBlockAgeCases)
        {
            sample.ExpectedJudgeVerdict.Should().Contain(
                "\"clear\"",
                $"AgeAppropriatenessCheck Block case [{sample.Id}] must carry severity='clear' " +
                "so the parse/map drives Block (not NeedsRegeneration). " +
                "Fix the canned verdict or the expectedOutcome — do not weaken the check.");
        }
    }
}

// ── Deserialization model ─────────────────────────────────────────────────────

/// <summary>Deserialization model for entries in safety-eval-set.json (v2 schema with subject + expectedJudgeVerdict).</summary>
internal sealed record EvalSample(
    string Id,
    string Subject,
    string Language,
    string Check,
    string Content,
    string ExpectedOutcome,
    [property: JsonPropertyName("expectedJudgeVerdict")]
    string? ExpectedJudgeVerdict,
    string Description);

// ── Suite-level result collector (xUnit IClassFixture) ────────────────────────

/// <summary>
/// Per-suite result collector injected as an xUnit class fixture.
/// Accumulates per-case pass/fail counts during the Theory run and writes the
/// committed run-summary JSON artifact (<c>safety-eval-results.json</c>).
///
/// <para>Thread-safe: Theory tests may run in parallel. Integer counters use
/// <see cref="Interlocked"/>; dictionary breakdowns use a lock.</para>
/// </summary>
public sealed class EvalRunCollector
{
    private int _total;
    private int _passed;

    private readonly object _lock = new();
    private readonly Dictionary<string, (int Passed, int Total)> _byCheck    = new();
    private readonly Dictionary<string, (int Passed, int Total)> _bySubject  = new();
    private readonly Dictionary<string, (int Passed, int Total)> _byLanguage = new();

    public int    TotalCases  => _total;
    public int    PassedCases => _passed;
    public int    FailedCases => _total - _passed;
    public double PassRate    => _total == 0 ? 0.0 : (double)_passed / _total;

    public void Record(string id, string subject, string language, string check, bool passed)
    {
        Interlocked.Increment(ref _total);
        if (passed)
            Interlocked.Increment(ref _passed);

        lock (_lock)
        {
            Bump(_byCheck,    check,    passed);
            Bump(_bySubject,  subject,  passed);
            Bump(_byLanguage, language, passed);
        }
    }

    private static void Bump(Dictionary<string, (int, int)> d, string key, bool passed)
    {
        d.TryGetValue(key, out var cur);
        d[key] = (cur.Item1 + (passed ? 1 : 0), cur.Item2 + 1);
    }

    /// <summary>
    /// Writes the run-summary JSON to:
    /// <list type="number">
    /// <item>The binary output <c>Data/</c> folder (always).</item>
    /// <item>The committed source <c>Ai.EvalTests/Data/safety-eval-results.json</c>
    ///   (walked up from the binary dir), so the embedded resource in Ai.Infrastructure
    ///   picks up the latest run on the next build.</item>
    /// </list>
    /// </summary>
    public void WriteSummaryArtifact(double thresholdPercent)
    {
        var passRatePct = PassRate * 100.0;
        var breached    = passRatePct < thresholdPercent;

        var summary = new EvalRunSummary
        {
            RunId            = Guid.NewGuid(),
            RanAt            = DateTime.UtcNow,
            TotalCases       = TotalCases,
            PassedCases      = PassedCases,
            FailedCases      = FailedCases,
            PassRate         = passRatePct,
            FailRate         = 100.0 - passRatePct,
            ThresholdPercent = thresholdPercent,
            Breached         = breached,
            Tier             = "EvalOffline",
            Note             = breached
                ? $"BREACH: {FailedCases} case(s) failed. Inspect breakdown. Do NOT lower the threshold — fix the code."
                : $"All {TotalCases} offline eval cases passed at {passRatePct:F1}%.",
            ByCheck    = ToBreakdown(_byCheck),
            BySubject  = ToBreakdown(_bySubject),
            ByLanguage = ToBreakdown(_byLanguage),
        };

        var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
        var json     = JsonSerializer.Serialize(summary, jsonOpts);

        // 1. Binary output dir.
        var outputDir = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "safety-eval-results.json"), json);

        // 2. Committed source dir (walk up to find Ai.EvalTests/Data/).
        var sourcePath = FindSourceResultsPath();
        if (sourcePath is not null)
            File.WriteAllText(sourcePath, json);
    }

    private static Dictionary<string, EvalBreakdownEntry> ToBreakdown(
        Dictionary<string, (int Passed, int Total)> raw)
        => raw.ToDictionary(kv => kv.Key, kv => new EvalBreakdownEntry(kv.Value.Passed, kv.Value.Total));

    private static string? FindSourceResultsPath()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                // Check if we are inside .../Ai.EvalTests/bin/...
                if (dir.Name.Equals("Ai.EvalTests", StringComparison.OrdinalIgnoreCase))
                {
                    var candidate = Path.Combine(dir.FullName, "Data", "safety-eval-results.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(candidate)!);
                    return candidate;
                }

                // Walk up looking for the project directory.
                var probe = Path.Combine(dir.FullName, "tests", "Ai.EvalTests", "Data");
                if (Directory.Exists(probe))
                    return Path.Combine(probe, "safety-eval-results.json");

                dir = dir.Parent;
            }
        }
        catch
        {
            // Non-critical — the output-dir copy is always written.
        }

        return null;
    }
}

// ── Summary DTO ───────────────────────────────────────────────────────────────

/// <summary>
/// Run-summary written to <c>safety-eval-results.json</c>.
/// Consumed by <c>AiSafetyEvalResultsQueryAdapter</c> in Ai.Infrastructure
/// to serve the <c>GET /api/Admin/AiSafety/evals</c> endpoint without a DB.
/// </summary>
internal sealed record EvalRunSummary
{
    [JsonPropertyName("runId")]
    public Guid RunId { get; init; }

    [JsonPropertyName("ranAt")]
    public DateTime RanAt { get; init; }

    [JsonPropertyName("totalCases")]
    public int TotalCases { get; init; }

    [JsonPropertyName("passedCases")]
    public int PassedCases { get; init; }

    [JsonPropertyName("failedCases")]
    public int FailedCases { get; init; }

    [JsonPropertyName("passRate")]
    public double PassRate { get; init; }

    [JsonPropertyName("failRate")]
    public double FailRate { get; init; }

    [JsonPropertyName("thresholdPercent")]
    public double ThresholdPercent { get; init; }

    [JsonPropertyName("breached")]
    public bool Breached { get; init; }

    [JsonPropertyName("tier")]
    public string Tier { get; init; } = "EvalOffline";

    [JsonPropertyName("note")]
    public string Note { get; init; } = string.Empty;

    [JsonPropertyName("byCheck")]
    public Dictionary<string, EvalBreakdownEntry> ByCheck { get; init; } = new();

    [JsonPropertyName("bySubject")]
    public Dictionary<string, EvalBreakdownEntry> BySubject { get; init; } = new();

    [JsonPropertyName("byLanguage")]
    public Dictionary<string, EvalBreakdownEntry> ByLanguage { get; init; } = new();
}

internal sealed record EvalBreakdownEntry(int Passed, int Total)
{
    [JsonPropertyName("passed")]
    public int Passed { get; } = Passed;

    [JsonPropertyName("total")]
    public int Total { get; } = Total;

    [JsonPropertyName("passRate")]
    public double PassRate => Total == 0 ? 0.0 : (double)Passed / Total * 100.0;
}
