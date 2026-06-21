// Console entry point for: dotnet run --project backend/tests/Learnexia.PerfTests
// Equivalent to PerfRunner xUnit test but runs as a standalone console process.
//
// Usage:
//   dotnet run --project backend/tests/Learnexia.PerfTests
//
// Exit code: 0 = all SLOs passed; 1 = one or more SLOs breached.

using System.Text;
using Learnexia.IntegrationTests;
using Learnexia.PerfTests;
using NBomber.CSharp;
using NBomber.Contracts.Stats;
using Xunit;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// WebApplicationFactory<Program> needs the Host project's content root when running
// via `dotnet run` (the test runner sets this automatically for `dotnet test`).
// Walk up from the binary directory to locate the Host project directory.
var hostProjectDir = FindHostProjectDir(AppContext.BaseDirectory);
if (hostProjectDir is not null)
{
    // ASPNETCORE_TEST_CONTENTROOT_<ASSEMBLY_NAME_UPPERCASE> is the env var that
    // WebApplicationFactory uses to override the content root search.
    Environment.SetEnvironmentVariable(
        "ASPNETCORE_TEST_CONTENTROOT_LEARNEXIA_HOST",
        hostProjectDir);
    Console.WriteLine($"[perf] Host content root: {hostProjectDir}");
}

Console.WriteLine("[perf] Spinning up WebApplicationFactory + Testcontainers PG...");

var factory = new LearnexiaWebAppFactory();

// Start the Testcontainers PG (IAsyncLifetime.InitializeAsync).
await ((IAsyncLifetime)factory).InitializeAsync();

try
{
    using var client = factory.CreateClient();

    Console.WriteLine("[perf] Running warmup: migrate + seed + register parent/child + resolve IDs...");
    var ctx = await PerfWarmup.InitAsync(factory, client);
    await PerfWarmup.PopulateAttemptAsync(client, ctx);

    Console.WriteLine($"[perf] Context: lessonId={ctx.LessonId}, attemptId={ctx.AttemptId}, questionId={ctx.QuestionId}");

    var coreScenarios = CoreApiScenarios.BuildAll(client, ctx);
    var aiScenarios   = AiOverheadScenario.BuildAll(client, ctx);
    var allScenarios  = coreScenarios.Concat(aiScenarios).ToArray();

    var reportDir = ResolveReportDir();
    Directory.CreateDirectory(reportDir);
    Console.WriteLine($"[perf] Reports: {reportDir}");

    var nodeStats = NBomberRunner
        .RegisterScenarios(allScenarios)
        .WithReportFolder(reportDir)
        .WithReportFileName("P6-01-nbomber-run")
        .Run();

    var coreScenarioNames = new HashSet<string>(
        coreScenarios.Select(s => s.ScenarioName),
        StringComparer.OrdinalIgnoreCase);

    var violations = new List<string>();

    Console.WriteLine();
    Console.WriteLine("=== P6-01 Perf Results ===");
    Console.WriteLine($"{"Scenario",-45} {"p50":>8} {"p95":>8} {"p99":>8} {"RPS":>8} {"Errors":>8} {"SLO",-6}");
    Console.WriteLine(new string('-', 95));

    foreach (var s in nodeStats.ScenarioStats.OrderByDescending(x => x.Ok.Latency.Percent95))
    {
        var isCore = coreScenarioNames.Contains(s.ScenarioName);
        var p95    = s.Ok.Latency.Percent95;
        var slo    = isCore
            ? (p95 < PerfConstants.P95ThresholdMs ? "PASS" : "FAIL")
            : "N/A";

        Console.WriteLine(
            $"{s.ScenarioName,-45} " +
            $"{s.Ok.Latency.MeanMs,8:F0} " +
            $"{p95,8:F0} " +
            $"{s.Ok.Latency.Percent99,8:F0} " +
            $"{s.Ok.Request.RPS,8:F1} " +
            $"{s.Fail.Request.Count,8} " +
            $"{slo,-6}");

        if (isCore && p95 >= PerfConstants.P95ThresholdMs)
            violations.Add($"{s.ScenarioName}: p95={p95:F1}ms");
    }

    // Write markdown summary
    WriteMarkdownSummary(reportDir, nodeStats.ScenarioStats, coreScenarioNames);

    Console.WriteLine();

    if (violations.Count > 0)
    {
        Console.WriteLine($"[FAIL] {violations.Count} scenario(s) breached p95 < {PerfConstants.P95ThresholdMs}ms SLO:");
        foreach (var v in violations)
            Console.WriteLine($"  - {v}");
        Console.WriteLine();
        Console.WriteLine("NOTE: Directional numbers (in-process factory, no Redis, modest Dev seed).");
        Console.WriteLine("Authoritative PASS/FAIL requires a devops staging run.");
        Environment.Exit(1);
    }
    else
    {
        Console.WriteLine($"[PASS] All {coreScenarios.Length} core-API scenarios met p95 < {PerfConstants.P95ThresholdMs}ms SLO.");
        Console.WriteLine("NOTE: Directional only. Devops run is authoritative.");
    }
}
finally
{
    await ((IAsyncLifetime)factory).DisposeAsync();
}

static string? FindHostProjectDir(string startDir)
{
    // Walk up from the binary to find the solution root (contains Learnexia.Modular.sln).
    var dir = new DirectoryInfo(startDir);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Learnexia.Modular.sln")))
        dir = dir.Parent;

    if (dir is null) return null;

    // The Host project lives at backend/src/Host/Learnexia.Host
    var hostDir = Path.Combine(dir.FullName, "src", "Host", "Learnexia.Host");
    return Directory.Exists(hostDir) ? hostDir : null;
}

static string ResolveReportDir()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Learnexia.Modular.sln")))
        dir = dir.Parent;
    var solutionRoot = dir?.FullName ?? Directory.GetCurrentDirectory();
    return Path.GetFullPath(Path.Combine(solutionRoot, "..", "docs", "perf"));
}

static void WriteMarkdownSummary(
    string reportDir,
    ScenarioStats[] stats,
    HashSet<string> coreScenarioNames)
{
    var summaryPath = Path.Combine(reportDir, "P6-01-run-summary.md");
    var sb = new StringBuilder();
    sb.AppendLine("# P6-01 NBomber Run Summary");
    sb.AppendLine();
    sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:u}");
    sb.AppendLine($"Concurrency: {PerfConstants.ConcurrentCopies} copies, {PerfConstants.DurationSeconds}s, {PerfConstants.WarmupSeconds}s warmup");
    sb.AppendLine($"SLO: p95 < {PerfConstants.P95ThresholdMs} ms (NFR-1, core-API only)");
    sb.AppendLine($"Mode: in-process WebApplicationFactory + Testcontainers PG, in-memory Redis fallback");
    sb.AppendLine();
    sb.AppendLine("| Scenario | p50 ms | p95 ms | p99 ms | RPS | Errors | SLO |");
    sb.AppendLine("|---|---|---|---|---|---|---|");

    foreach (var s in stats.OrderByDescending(x => x.Ok.Latency.Percent95))
    {
        var isCore = coreScenarioNames.Contains(s.ScenarioName);
        var p95    = s.Ok.Latency.Percent95;
        var slo    = isCore
            ? (p95 < PerfConstants.P95ThresholdMs ? "PASS" : "FAIL")
            : "N/A (overhead only)";

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
    sb.AppendLine("- Redis NOT active: gamification numbers reflect DB/in-memory fallback, not prod Redis hot path.");
    sb.AppendLine("- Minimal seed data: index/data-volume hotspots may not surface.");
    sb.AppendLine("- Authoritative PASS/FAIL requires a devops staging run at prod-scale + real Redis.");
    sb.AppendLine("- AI scenarios (S13/S14): pipeline overhead only (no API keys). Live <4s = devops.");

    File.WriteAllText(summaryPath, sb.ToString());
}
