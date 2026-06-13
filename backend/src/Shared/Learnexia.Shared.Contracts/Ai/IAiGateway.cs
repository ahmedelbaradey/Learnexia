namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// The single public entry point for all AI completions in Learnexia.
/// Feature handlers call this interface; they never reference a provider SDK type directly.
///
/// Implemented by <c>AiGateway</c> in <c>Ai.Infrastructure</c>.
/// Contract registered in <c>Shared.Contracts/Ai/</c> so any module can inject it without
/// referencing the AI module's projects (module isolation rule).
///
/// FR-AI-6: returns content only — never a progression, difficulty, or unlock decision.
/// </summary>
public interface IAiGateway
{
    /// <summary>
    /// Execute a non-streaming AI completion against the best-fit provider and model
    /// selected by the router from <paramref name="request"/>.
    /// Never throws — failures are encoded in <see cref="AiResult.Error"/> with
    /// <c>Successed = false</c>.
    /// </summary>
    Task<AiResult> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a streaming AI completion, yielding text deltas as they arrive.
    /// The SSE endpoint wiring is owned by P3-04; this signature is frozen here so
    /// P3-02 / P3-03 can design against it.
    /// Never throws — the terminal chunk carries the error if the stream fails mid-way.
    /// </summary>
    IAsyncEnumerable<AiChunk> StreamAsync(AiRequest request, CancellationToken cancellationToken = default);
}
