namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Per-call usage telemetry captured by the gateway facade and logged at Debug.
/// No prompt/response text is included (no PII). Feeds P7-11 cost dashboard.
/// </summary>
public sealed record AiUsage
{
    /// <summary>Provider name (e.g. "Claude", "OpenAi").</summary>
    public required string Provider { get; init; }

    /// <summary>Model ID actually used (e.g. "claude-haiku-4-5").</summary>
    public required string ModelId { get; init; }

    /// <summary>Input prompt tokens billed by the provider.</summary>
    public int PromptTokens { get; init; }

    /// <summary>Output completion tokens billed by the provider.</summary>
    public int CompletionTokens { get; init; }

    /// <summary>End-to-end latency from gateway send to response received, in milliseconds.</summary>
    public long LatencyMs { get; init; }

    /// <summary>
    /// Estimated USD cost approximated from per-model rates in config.
    /// Indicative only — actual billing is from the provider dashboard.
    /// </summary>
    public decimal EstimatedCostUsd { get; init; }

    /// <summary>Whether the call was cache-hit on the provider (input tokens cached).</summary>
    public bool WasCacheHit { get; init; }
}
