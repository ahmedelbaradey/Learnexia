namespace Learnexia.Modules.Curriculum.Application.Features.Retrieval.Queries.RetrieveChunks;

/// <summary>
/// A single retrieved curriculum chunk returned by <see cref="RetrieveChunksQueryHandler"/>.
///
/// <para><strong>Security:</strong> raw embedding vectors are NEVER exposed through this DTO.
/// Only the text content, metadata, and similarity score are returned.</para>
/// </summary>
/// <param name="ChunkId">Opaque integer identifier of the <c>CurriculumChunk</c> row.</param>
/// <param name="Content">The text content of the curriculum chunk.</param>
/// <param name="Metadata">Supplementary metadata JSON string describing pedagogical context.</param>
/// <param name="Score">
/// Cosine similarity score in [0, 1] — 1.0 = identical direction, lower = less similar.
/// Computed as <c>1 - cosineDistance</c> where cosineDistance = <c>Vector &lt;=&gt; queryVector</c>.
/// Chunks below the configured similarity floor are excluded before this DTO is produced.
/// </param>
public sealed record CurriculumChunkDto(
    int ChunkId,
    string Content,
    string Metadata,
    double Score);
