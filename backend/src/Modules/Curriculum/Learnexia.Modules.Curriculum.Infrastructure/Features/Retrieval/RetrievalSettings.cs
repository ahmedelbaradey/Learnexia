namespace Learnexia.Modules.Curriculum.Infrastructure.Features.Retrieval;

/// <summary>
/// Configurable retrieval parameters for <c>RetrieveChunksQueryHandler</c>.
/// Bind from <c>Curriculum:Retrieval</c> in appsettings.
///
/// <para><strong>Similarity floor (OQ-7):</strong>
/// Expressed as a cosine DISTANCE threshold (0 = identical, 2 = maximally different).
/// BGE-M3 normalized embeddings produce cosine distances in [0, 2].
/// Chunks with distance &gt; <see cref="SimilarityDistanceFloor"/> are rejected.
/// Default 0.4 is empirically chosen for the seeded placeholder corpus; it should be
/// re-calibrated against real BGE-M3 embeddings once BE-0 (TEI endpoint) is live.</para>
/// </summary>
public sealed class RetrievalSettings
{
    public const string SectionName = "Curriculum:Retrieval";

    /// <summary>
    /// Cosine distance threshold above which a chunk is considered not relevant.
    /// Lower = stricter; higher = more permissive.
    /// Default: 0.4 (40% away from the query direction on the unit sphere).
    /// </summary>
    public double SimilarityDistanceFloor { get; init; } = 0.4;
}
