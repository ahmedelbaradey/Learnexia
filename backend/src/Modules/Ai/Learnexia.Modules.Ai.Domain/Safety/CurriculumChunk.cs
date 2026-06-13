namespace Learnexia.Modules.Ai.Domain.Safety;

/// <summary>
/// A curriculum text chunk passed as RAG (Retrieval-Augmented Generation) context
/// to the <see cref="IHallucinationCheck"/>.
///
/// <para><strong>P3-07 placeholder:</strong> the full RAG pipeline ships in P3-07.
/// Until then this record is defined but <c>IHallucinationCheck.CheckAsync</c> receives
/// a null or empty list, and implementations use heuristic-only detection.
/// Once P3-07 lands, implementations compare AI output against these chunks for
/// grounded factual accuracy.</para>
/// </summary>
/// <param name="ChunkId">Opaque identifier for the chunk (e.g. a curriculum node ID).</param>
/// <param name="Text">The raw curriculum text for this chunk.</param>
public sealed record CurriculumChunk(string ChunkId, string Text);
