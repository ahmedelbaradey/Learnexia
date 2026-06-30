namespace Learnexia.Modules.Ai.Application.Options;

/// <summary>
/// Configuration options for the P3-15 AI batch path (IAiBatchGateway seam).
///
/// <para>Bound from <c>Ai:Batch</c> in appsettings / env vars.
/// <b>Enabled defaults to false</b> — the batch path is dormant until explicitly activated.</para>
///
/// <para>Offline jobs (cache-prewarm BE-4, bulk-question-gen BE-5) are deferred to P3-15b;
/// those flags are not present here yet.</para>
///
/// <para>API keys are NOT stored here — they reuse the same
/// <c>Ai:Providers:Claude:ApiKey</c> config key as the runtime gateway.</para>
/// </summary>
public sealed class AiBatchOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Ai:Batch";

    /// <summary>
    /// Master kill-switch for the batch path.
    /// When false (default), all batch jobs are no-ops and the batch provider is dormant.
    /// Set to true via environment variable / appsettings override after keys + schedule exist.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Number of milliseconds to wait between poll attempts when waiting for a batch job to finish.
    /// Default: 30 000 ms (30 seconds).
    /// </summary>
    public int PollIntervalMs { get; set; } = 30_000;

    /// <summary>
    /// Maximum number of poll attempts before giving up on a batch job (fail-soft).
    /// Default: 60 (30 min at 30-second intervals).
    /// </summary>
    public int MaxPollAttempts { get; set; } = 60;
}
