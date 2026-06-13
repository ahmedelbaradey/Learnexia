using Learnexia.Shared.Kernel.Entities;
using Pgvector;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// BGE-M3 (1024-dim) embedding row for a <see cref="CurriculumChunk"/>.
/// Maps to the physical table <c>chunk_embeddings_bge_m3</c> in the <c>curriculum</c> schema.
///
/// Design rules (curriculum-system-of-record.md §4 — Decision D):
/// - One physical table per (Model, Dimension). This table is for BGE-M3 / vector(1024) ONLY.
///   A future model gets a NEW table (e.g. chunk_embeddings_openai_3072) — no ALTER COLUMN here.
/// - <see cref="ChunkId"/> is an intra-module FK to <see cref="CurriculumChunk"/> with ON DELETE CASCADE.
/// - <see cref="IsActive"/> marks the currently-served embedding table for retrieval.
///   Exactly one model should be active at a time; the retrieval query filters IsActive = true.
/// - <see cref="Vector"/> is vector(1024). The HNSW cosine index is built on this column (BE-4).
/// - <see cref="Dimension"/> is a documentation field (1024); pgvector enforces the physical column type.
/// - <see cref="ModelVersion"/> must match the pinned TEI runtime version (parity requirement).
///
/// Note: derives from <see cref="CreationAuditedEntity"/> rather than <see cref="AggregateRoot"/>
/// because this is an append-only embedding store — no update/delete lifecycle, no domain events.
/// </summary>
public class ChunkEmbeddingBgeM3 : CreationAuditedEntity
{
    /// <summary>
    /// Intra-module FK to <see cref="CurriculumChunk"/> (ON DELETE CASCADE in migration).
    /// </summary>
    public int ChunkId { get; set; }

    /// <summary>
    /// Embedding provider identifier (e.g. "huggingface-tei").
    /// </summary>
    public string Provider { get; set; } = null!;

    /// <summary>
    /// Model slug (e.g. "bge-m3"). Must match the runtime TEI endpoint's served model.
    /// </summary>
    public string Model { get; set; } = null!;

    /// <summary>
    /// Pinned model version (e.g. "1.0"). Must match the TEI endpoint's model version.
    /// BgeM3EmbeddingProvider fails-fast on mismatch (parity requirement).
    /// </summary>
    public string ModelVersion { get; set; } = null!;

    /// <summary>
    /// Documentation field: the vector dimension count (1024 for BGE-M3).
    /// The physical column type (vector(1024)) is the authority; this field is informational.
    /// </summary>
    public int Dimension { get; set; }

    /// <summary>
    /// The 1024-dimensional BGE-M3 embedding vector.
    /// HNSW cosine index is built on this column (see ChunkEmbeddingBgeM3Config).
    /// </summary>
    public Vector Vector { get; set; } = null!;

    /// <summary>
    /// Whether this embedding row belongs to the currently-active model for retrieval.
    /// Exactly one model should have IsActive = true; the retrieval query filters on this column.
    /// </summary>
    public bool IsActive { get; set; }

    // Navigation
    public CurriculumChunk Chunk { get; set; } = null!;
}
