// DEMO ONLY — disposable; remove before curriculum/RAG story.
// This entity exists solely to prove a usable pgvector column (P1-06 AC-2).
// The real embedding table will be built in the curriculum/RAG module under its own schema.
using Pgvector;

namespace Learnexia.Modules.Catalog.Domain.Entities;

/// <summary>
/// Disposable demo entity that proves pgvector round-trip capability.
/// Remove this class (and the DEMO_PgvectorProof migration) once AC-2 is verified.
/// </summary>
public class EmbeddingDemo
{
    public int Id { get; set; }

    /// <summary>Label / description for the demo vector — not a business field.</summary>
    public string Label { get; set; } = null!;

    /// <summary>
    /// 3-dimensional demo vector (kept small for the proof; real RAG embeddings will be
    /// vector(1536) or similar, living in the curriculum schema).
    /// Mapped to a PostgreSQL <c>vector(3)</c> column via Pgvector.EntityFrameworkCore.
    /// </summary>
    public Vector Embedding { get; set; } = null!;
}
