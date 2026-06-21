namespace Learnexia.PerfTests;

/// <summary>
/// Tuneable constants for the NBomber perf harness.
///
/// <para><strong>Latency-focused, scenario-ISOLATED in-process profile.</strong> Scenarios run
/// SEQUENTIALLY (one at a time) against a single in-process <c>WebApplicationFactory</c> TestServer —
/// running all ~14 in parallel saturates the in-process server and inflates every p95 into
/// cross-scenario-contention queueing rather than real handler time. Each scenario runs alone at a low
/// <see cref="ConcurrentCopies"/> so the recorded p95 ≈ handler latency (the signal that catches slow
/// handlers / N+1 / missing-index hotspots — what the NFR-1 p95<500ms SLO is about). **Throughput /
/// high-concurrency / soak = the devops live-Kestrel staging run** (see README), not this baseline.</para>
/// </summary>
internal static class PerfConstants
{
    // ------------------------------------------------------------------
    // Concurrency knobs — edit these to change load profile
    // ------------------------------------------------------------------

    /// <summary>
    /// Concurrent virtual users per scenario. Each scenario runs in ISOLATION (sequentially), so this is
    /// the only in-flight load while that scenario runs → p95 reflects handler latency under light load.
    /// Raise (and switch to parallel/ramped) only for the live-Kestrel devops run.
    /// </summary>
    public const int ConcurrentCopies = 5;

    /// <summary>Measurement window per scenario, in seconds.</summary>
    public const int DurationSeconds = 20;

    /// <summary>Warmup duration before measurement starts, in seconds.</summary>
    public const int WarmupSeconds = 3;

    // ------------------------------------------------------------------
    // SLO thresholds
    // ------------------------------------------------------------------

    /// <summary>
    /// Core-API p95 latency SLO (ms). Scenarios assert Percent95 &lt; this.
    /// NFR-1: core API p95 &lt; 500 ms.
    /// </summary>
    public const double P95ThresholdMs = 500.0;

    /// <summary>
    /// Max acceptable error-rate (%) for a core-API scenario. INTEGRITY GATE: a scenario with no
    /// successful requests (or error-rate above this) is a perf FAIL regardless of latency — otherwise
    /// a fully-erroring scenario shows Ok.Latency.Percent95 == 0 and spuriously "passes" the p95 check.
    /// </summary>
    public const double MaxErrorRatePct = 5.0;

    // ------------------------------------------------------------------
    // Accounts used in warmup
    // ------------------------------------------------------------------
    public const string SuperAdminUserName = "superadmin";
    public const string SuperAdminPassword = "123Pa$$word!";

    public const string ParentEmail    = "perf_parent@test.local";
    public const string ParentPassword = "PerfParent@1234";

    public const string ChildEmail    = "perf_student@test.local";
    public const string ChildPassword = "PerfChild@1234";

    // ------------------------------------------------------------------
    // Environment override for live-Kestrel runs (devops)
    // ------------------------------------------------------------------

    /// <summary>
    /// RESERVED for the future Kestrel-targeting follow-up — **NOT yet consumed**. The harness currently
    /// always uses the in-process <c>WebApplicationFactory</c> (the warmup seeds via an in-process
    /// <c>DbContext</c>, which can't reach a remote DB). To wire Kestrel mode: switch the client in
    /// <c>PerfRunner</c>/<c>Program</c> to <c>new HttpClient { BaseAddress = PerfBaseUrl }</c> when
    /// <c>PERF_BASE_URL</c> is set, and replace the in-process seed with API-based discovery. See README.
    /// </summary>
    public static string PerfBaseUrl =>
        Environment.GetEnvironmentVariable("PERF_BASE_URL") ?? "http://localhost:5080";

    // ------------------------------------------------------------------
    // Report output directory (relative to CWD of the test runner)
    // ------------------------------------------------------------------
    public const string ReportFolder = "docs/perf";
}
