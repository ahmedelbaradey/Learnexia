using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Infrastructure.Providers;

/// <summary>
/// Internal provider adapter contract. One implementation per LLM provider
/// (Claude, OpenAI, Gemini, …). Only referenced inside <c>Ai.Infrastructure</c>;
/// never crosses the module boundary.
/// </summary>
public interface IAiProvider
{
    /// <summary>Provider name key that matches the routing table (e.g. "Claude", "OpenAi").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Execute a non-streaming completion against this provider for the given model and request.
    /// Maps HTTP 4xx/5xx/timeout to a typed <see cref="AiError"/>; never throws to the caller.
    /// </summary>
    Task<AiResult> CompleteAsync(
        AiRequest request,
        string modelId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Execute a streaming completion against this provider. Yields text deltas as received.
    /// Signature defined now (frozen for P3-02/P3-03); SSE endpoint is owned by P3-04.
    /// </summary>
    IAsyncEnumerable<AiChunk> StreamAsync(
        AiRequest request,
        string modelId,
        CancellationToken cancellationToken);
}
