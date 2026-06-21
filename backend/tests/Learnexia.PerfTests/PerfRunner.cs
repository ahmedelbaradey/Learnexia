using Learnexia.IntegrationTests;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using Xunit;

namespace Learnexia.PerfTests;

/// <summary>
/// NBomber perf harness — xUnit entry point.
///
/// HOW TO RUN:
///   dotnet test backend/tests/Learnexia.PerfTests -filter "Category=Perf"
///   -- or --
///   dotnet run --project backend/tests/Learnexia.PerfTests
///
/// The harness spins an in-process WebApplicationFactory (Testcontainers PG).
/// Rate-limiter is neutralised by the factory (int.MaxValue cap).
/// Redis is NOT configured — gamification numbers reflect the code path with the
/// in-memory distributed-cache fallback (not the prod Redis hot path).
///
/// Reports are written to docs/perf/ (HTML + CSV + Markdown).
/// Thresholds: core-API p95 &lt; 500 ms (NFR-1).
/// AI scenarios: pipeline overhead only; no threshold assertion locally.
///   Live &lt;4 s = devops with real keys.
/// </summary>
[Trait("Category", "Perf")]
public sealed class PerfRunner : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public PerfRunner()
    {
        _factory = new LearnexiaWebAppFactory();
        _client  = _factory.CreateClient(); // factory starts Testcontainers on first CreateClient
    }

    public async Task InitializeAsync()
    {
        // Factory IAsyncLifetime.InitializeAsync starts the Postgres container.
        // We call it explicitly because we own the factory (not via ICollectionFixture).
        await ((IAsyncLifetime)_factory).InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await ((IAsyncLifetime)_factory).DisposeAsync();
    }

    [Fact(DisplayName = "P6-01 Core-API + AI perf harness (NBomber, in-process factory)")]
    public async Task Run_PerfHarness()
    {
        // ------------------------------------------------------------------
        // Phase 1: Warmup — apply migrations + seed + mint JWTs + resolve IDs
        // ------------------------------------------------------------------
        var ctx = await PerfWarmup.InitAsync(_factory, _client);
        await PerfWarmup.PopulateAttemptAsync(_client, ctx);

        // Fail fast if warmup did not produce the IDs the scenarios need. Otherwise the id-gated
        // scenarios (S02/S03/S05/S06/S10/S12) return Response.Ok(sizeBytes:0) and would spuriously
        // PASS the SLO + integrity gate on a broken warmup (the masking risk reviewer flagged).
        Assert.True(ctx.SubjectId  > 0, "Warmup failed to seed a Subject (ctx.SubjectId).");
        Assert.True(ctx.LessonId   > 0, "Warmup failed to seed a Lesson (ctx.LessonId).");
        Assert.True(ctx.QuestionId > 0, "Warmup failed to produce a QuestionId (ctx.QuestionId).");
        Assert.True(ctx.ChildId    > 0, "Warmup failed to create a child (ctx.ChildId).");
        Assert.True(ctx.AttemptId  > 0, "Warmup failed to start an Attempt (ctx.AttemptId).");

        // ------------------------------------------------------------------
        // Phase 2: Build scenarios
        // ------------------------------------------------------------------
        var coreScenarios = CoreApiScenarios.BuildAll(_client, ctx);
        var aiScenarios   = AiOverheadScenario.BuildAll(_client, ctx);

        // ------------------------------------------------------------------
        // Phase 3: Run NBomber
        // ------------------------------------------------------------------
        var allScenarios = coreScenarios.Concat(aiScenarios).ToArray();

        // Ensure the report directory exists (relative to the solution root when run from the
        // project directory; absolute path preferred for the run output).
        var reportDir = ResolveReportDir();
        Directory.CreateDirectory(reportDir);

        // Run each scenario in ISOLATION (sequentially): running all ~14 in parallel saturates the
        // single in-process TestServer and inflates every p95 into cross-scenario contention. One
        // scenario at a time at a low copy count → p95 ≈ handler latency (the hotspot signal).
        var allStats = new List<ScenarioStats>();
        foreach (var scenario in allScenarios)
        {
            var run = NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFolder(reportDir)
                .WithReportFileName($"P6-01-{scenario.ScenarioName}")
                .Run();
            allStats.AddRange(run.ScenarioStats);
        }

        // ------------------------------------------------------------------
        // Phase 4: Assert p95 < 500 ms for core-API scenarios
        // ------------------------------------------------------------------
        var coreScenarioNames = new HashSet<string>(
            coreScenarios.Select(s => s.ScenarioName),
            StringComparer.OrdinalIgnoreCase);

        var violations = new List<string>();

        foreach (var scenarioStats in allStats)
        {
            // Only assert on core-API scenarios; AI scenarios are overhead-only, no local SLO.
            if (!coreScenarioNames.Contains(scenarioStats.ScenarioName))
                continue;

            var okCount   = scenarioStats.Ok.Request.Count;
            var failCount = scenarioStats.Fail.Request.Count;
            var total     = okCount + failCount;
            var errorRate = total == 0 ? 100.0 : (double)failCount / total * 100.0;
            var p95Ms     = scenarioStats.Ok.Latency.Percent95;

            // INTEGRITY GATE (checked BEFORE latency): a scenario with no successful requests, or an
            // error-rate above the threshold, is a FAILURE — Ok.Latency.Percent95 is 0 when every
            // request errors, which would otherwise spuriously "pass" the <500 ms latency check.
            if (okCount == 0)
            {
                violations.Add(
                    $"FAIL [{scenarioStats.ScenarioName}] NO successful requests " +
                    $"({failCount} errors) — scenario is erroring, not a latency pass.");
            }
            else if (errorRate > PerfConstants.MaxErrorRatePct)
            {
                violations.Add(
                    $"FAIL [{scenarioStats.ScenarioName}] error-rate {errorRate:F1}% > " +
                    $"{PerfConstants.MaxErrorRatePct}% ({failCount}/{total} failed) — endpoint unhealthy under load.");
            }
            else if (p95Ms >= PerfConstants.P95ThresholdMs)
            {
                violations.Add(
                    $"FAIL [{scenarioStats.ScenarioName}] p95={p95Ms:F1} ms >= {PerfConstants.P95ThresholdMs} ms SLO " +
                    $"(p50={scenarioStats.Ok.Latency.MeanMs:F1} ms, " +
                    $"p99={scenarioStats.Ok.Latency.Percent99:F1} ms, " +
                    $"RPS={scenarioStats.Ok.Request.RPS:F1}, " +
                    $"errors={scenarioStats.Fail.Request.Count})");
            }
        }

        // Write a concise markdown summary alongside the NBomber HTML report.
        WriteMarkdownSummary(reportDir, allStats.ToArray(), coreScenarioNames);

        if (violations.Count > 0)
        {
            var message =
                $"NFR-1 p95 < {PerfConstants.P95ThresholdMs} ms SLO BREACHED for " +
                $"{violations.Count} scenario(s):\n" +
                string.Join("\n", violations) +
                "\n\nSee docs/perf/ for the full NBomber report. " +
                "These are directional numbers (in-process factory, in-memory Redis, modest Dev seed). " +
                "Authoritative pass/fail = devops staging run.";

            // Use xUnit assertion failure to surface the violations in the test output.
            Assert.Fail(message);
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string ResolveReportDir()
    {
        // Walk up from the test binary to find the solution root (contains Learnexia.Modular.sln).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Learnexia.Modular.sln")))
            dir = dir.Parent;

        // Fallback: place in the current working directory if the solution was not found.
        var solutionRoot = dir?.FullName ?? Directory.GetCurrentDirectory();
        return Path.Combine(solutionRoot, "..", "docs", "perf");
    }

    private static void WriteMarkdownSummary(
        string reportDir,
        ScenarioStats[] stats,
        HashSet<string> coreScenarioNames)
    {
        var summaryPath = Path.Combine(reportDir, "P6-01-run-summary.md");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# P6-01 NBomber Run Summary");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:u}");
        sb.AppendLine($"Concurrency: {PerfConstants.ConcurrentCopies} copies, {PerfConstants.DurationSeconds}s, {PerfConstants.WarmupSeconds}s warmup — scenarios run SEQUENTIALLY (isolated)");
        sb.AppendLine($"SLO: p95 < {PerfConstants.P95ThresholdMs} ms (NFR-1, core-API only); integrity gate: 0-OK or >{PerfConstants.MaxErrorRatePct}% errors = FAIL");
        sb.AppendLine($"Mode: in-process WebApplicationFactory + Testcontainers PG, in-memory Redis fallback, scenario-isolated sequential runs");
        sb.AppendLine();
        sb.AppendLine("| Scenario | p50 ms | p95 ms | p99 ms | RPS | Errors | SLO |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var s in stats.OrderByDescending(x => x.Ok.Latency.Percent95))
        {
            var isCore    = coreScenarioNames.Contains(s.ScenarioName);
            var p95       = s.Ok.Latency.Percent95;
            var okCount   = s.Ok.Request.Count;
            var failCount = s.Fail.Request.Count;
            var total     = okCount + failCount;
            var errorRate = total == 0 ? 100.0 : (double)failCount / total * 100.0;

            // Mirror the integrity gate: zero-OK or high-error = FAIL (not a fake p95=0 pass).
            string slo;
            if (!isCore)
                slo = "N/A (overhead only)";
            else if (okCount == 0)
                slo = "FAIL (no OK reqs)";
            else if (errorRate > PerfConstants.MaxErrorRatePct)
                slo = $"FAIL ({errorRate:F0}% err)";
            else
                slo = p95 < PerfConstants.P95ThresholdMs ? "PASS" : "FAIL";

            sb.AppendLine(
                $"| {s.ScenarioName} " +
                $"| {s.Ok.Latency.MeanMs:F0} " +
                $"| {p95:F0} " +
                $"| {s.Ok.Latency.Percent99:F0} " +
                $"| {s.Ok.Request.RPS:F1} " +
                $"| {s.Fail.Request.Count} " +
                $"| {slo} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Caveats");
        sb.AppendLine("- Numbers are DIRECTIONAL: in-process factory, single-box, Testcontainers/seeded PG.");
        sb.AppendLine("- Redis is NOT active: gamification p95 numbers reflect DB/in-memory fallback, not the prod Redis hot path.");
        sb.AppendLine("- Seed data is minimal (Dev curriculum + 1 parent + 1 child). Index/data-volume hotspots may not surface.");
        sb.AppendLine("- Authoritative PASS/FAIL requires a devops staging run at prod-scale + real Redis.");
        sb.AppendLine("- AI scenarios (S13/S14): pipeline overhead only (no API keys → fail-closed). Live <4s = devops.");

        File.WriteAllText(summaryPath, sb.ToString());
    }
}
