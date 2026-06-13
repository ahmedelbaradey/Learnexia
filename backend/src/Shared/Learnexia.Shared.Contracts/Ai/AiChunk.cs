namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// A single streaming chunk yielded by <see cref="IAiGateway.StreamAsync"/>.
/// The SSE endpoint that consumes this is owned by P3-04; the interface is frozen here so
/// P3-02/P3-03 can design against it.
/// </summary>
public sealed record AiChunk
{
    /// <summary>Partial text delta from the provider stream.</summary>
    public required string Delta { get; init; }

    /// <summary>True on the final chunk (end of stream). May carry usage data.</summary>
    public bool IsLast { get; init; }

    /// <summary>
    /// Usage telemetry on the terminal chunk (when <see cref="IsLast"/> is true).
    /// Null on intermediate chunks.
    /// </summary>
    public AiUsage? Usage { get; init; }
}
