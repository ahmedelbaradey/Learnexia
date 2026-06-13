namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Provider-neutral completion result returned by <see cref="IAiGateway.CompleteAsync"/>.
/// Mirrors the <c>Successed</c> flag convention used across BaseResponse&lt;T&gt;.
/// The gateway never throws — failures are encoded here.
/// </summary>
public sealed record AiResult
{
    /// <summary>True when the provider returned a usable completion; false on any failure.</summary>
    public required bool Successed { get; init; }

    /// <summary>Completion text from the provider. Null when <see cref="Successed"/> is false.</summary>
    public string? Content { get; init; }

    /// <summary>Typed error descriptor. Non-null when <see cref="Successed"/> is false.</summary>
    public AiError? Error { get; init; }

    /// <summary>Usage telemetry (always populated, even on failure, best-effort).</summary>
    public AiUsage? Usage { get; init; }

    // --- Factory helpers ---

    public static AiResult Ok(string content, AiUsage usage) =>
        new() { Successed = true, Content = content, Usage = usage };

    public static AiResult Fail(AiError error, AiUsage? usage = null) =>
        new() { Successed = false, Error = error, Usage = usage };
}
