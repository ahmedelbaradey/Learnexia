namespace Learnexia.Modules.Ai.Application.Options;

/// <summary>
/// Gateway behaviour options. Bound from <c>Ai:Gateway</c> in appsettings / env.
/// API keys are NOT stored here — they live at <c>Ai:Providers:Claude:ApiKey</c> etc.
/// in secret config / env vars (never committed).
/// </summary>
public sealed class AiGatewayOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Ai:Gateway";

    /// <summary>
    /// Default provider name when the router has no explicit override for a task/tier.
    /// Must match a registered provider name (e.g. "Claude").
    /// </summary>
    public string DefaultProvider { get; set; } = "Claude";

    /// <summary>
    /// Per-task/tier model routing table. Key format: <c>"TaskKind"</c> (default tier) or
    /// <c>"TaskKind:Tier"</c> (explicit tier override).
    /// Values are <see cref="ModelConfig"/> records describing provider + model.
    /// Empty = router uses built-in defaults.
    /// </summary>
    public Dictionary<string, ModelConfig> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hard timeout for a single provider call in seconds.
    /// The gateway creates a linked <c>CancellationTokenSource</c> seeded from this value.
    /// Default: 30 s. Must be positive.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts on transient failures (429 / 5xx / timeout).
    /// 0 = no retries (fail immediately). Default: 3.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Base backoff delay in seconds between retries (exponential: attempt * RetryBackoffSeconds).
    /// Default: 1.0 s.
    /// </summary>
    public double RetryBackoffSeconds { get; set; } = 1.0;
}
