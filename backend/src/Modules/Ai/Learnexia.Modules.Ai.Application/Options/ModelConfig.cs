namespace Learnexia.Modules.Ai.Application.Options;

/// <summary>
/// Per-route model configuration. The key in <see cref="AiGatewayOptions.Models"/> is
/// <c>"taskKind:tier"</c> (e.g. <c>"Hint:Cheap"</c>) or just <c>"taskKind"</c> for the default tier.
/// </summary>
public sealed class ModelConfig
{
    /// <summary>Provider name that should handle this task/tier combination (e.g. "Claude").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Model identifier sent to the provider API (e.g. "claude-haiku-4-5").</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Input price per million tokens (USD). Used by the gateway to estimate cost per call.
    /// Sourced from provider pricing documentation; update before launch.
    /// </summary>
    public decimal InputPricePerMillion { get; set; }

    /// <summary>
    /// Output price per million tokens (USD). Used by the gateway to estimate cost per call.
    /// </summary>
    public decimal OutputPricePerMillion { get; set; }
}
