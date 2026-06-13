using Pgvector;

namespace Learnexia.Modules.Curriculum.Application.Abstractions;

/// <summary>
/// Cross-cutting seam: converts a text string to a BGE-M3 1024-dimensional embedding vector.
///
/// <para>The active implementation is <c>BgeM3EmbeddingProvider</c> — a typed HttpClient adapter
/// that calls the self-hosted BGE-M3 TEI endpoint on Hetzner (synchronous HTTP call).</para>
///
/// <para><strong>Fail-soft contract:</strong> implementations MUST return <c>null</c> on any
/// transient failure (network, non-2xx, parse error) rather than throwing. Callers treat
/// <c>null</c> as "no context available" and short-circuit retrieval with an empty result.</para>
///
/// <para><strong>Parity guard:</strong> the implementation stamps <c>Provider</c>, <c>Model</c>,
/// and <c>ModelVersion</c> on every <c>chunk_embeddings_bge_m3</c> row it seeds, and logs a
/// warning when the runtime configuration does not match what was stamped on the DB rows.
/// Mismatched model versions produce incompatible vector spaces and invalid cosine distances.</para>
///
/// <para>Declared in <c>Curriculum.Application</c> (not Shared.Contracts) — this is an
/// intra-module seam, not a cross-module contract.</para>
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Embeds the given text and returns a <c>vector(1024)</c> suitable for cosine-distance
    /// search against <c>chunk_embeddings_bge_m3</c>.
    /// Returns <c>null</c> on any failure — callers must handle null gracefully.
    /// </summary>
    /// <param name="text">The text to embed (query text for retrieval, chunk content for ingestion).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Vector?> EmbedAsync(string text, CancellationToken ct = default);
}
