using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// A discrete chunk of curriculum content extracted from the pedagogical corpus.
///
/// Design rules (P3-07, curriculum-system-of-record.md §4):
/// - NO inline embedding column. Vectors live in the separate <see cref="ChunkEmbeddingBgeM3"/> table.
///   A future model gets a new per-dimension table — never an ALTER COLUMN on CurriculumChunk.
/// - Hierarchy references (SubjectId, GradeId, ConceptId, SkillId) are plain int — no cross-module FK.
///   Mirroring QuizQuestion.SkillId in the Learning module.
/// - <see cref="CurriculumVersionId"/> is an intra-module FK to <see cref="CurriculumVersion"/>.
/// - <see cref="ProvenanceRef"/> is null for seeded chunks; BL-05 backfills source Page/Block refs.
/// - Visibility is governed by <c>CurriculumVersion.Status</c>, NOT a chunk-level status.
///
/// BL-04/BL-05 will extend this entity; P3-07 delivers the minimal slice.
/// </summary>
public class CurriculumChunk : AggregateRoot
{
    /// <summary>
    /// The text content of this curriculum chunk.
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Supplementary metadata (JSON or plain string) describing the chunk's pedagogical context.
    /// Stored as jsonb. Application layer owns the shape.
    /// </summary>
    public string Metadata { get; set; } = "{}";

    /// <summary>
    /// Pedagogical difficulty level on a 1–5 scale (1 = easiest, 5 = hardest).
    /// </summary>
    public int Difficulty { get; set; }

    /// <summary>
    /// Loose reference to the subject (no cross-module FK). Mirrors QuizQuestion.SubjectId pattern.
    /// </summary>
    public int SubjectId { get; set; }

    /// <summary>
    /// Loose reference to the grade (no cross-module FK). Mirrors QuizQuestion.GradeId pattern.
    /// </summary>
    public int GradeId { get; set; }

    /// <summary>
    /// Optional loose reference to a concept node (no cross-module FK). Null until BL-04 links provenance.
    /// </summary>
    public int? ConceptId { get; set; }

    /// <summary>
    /// Optional loose reference to a skill (no cross-module FK). Used as the weak-area filter key
    /// in retrieval. Mirrors QuizQuestion.SkillId pattern.
    /// </summary>
    public int? SkillId { get; set; }

    /// <summary>
    /// Stable semantic identifier matching P2-10/P2-11 skill seeds.
    /// Format: {SubjectCode}.{GradeLevel}.{UnitSlug}.{SkillSlug} (e.g. "math.grade4.fractions.add-unlike-denominators").
    /// Set at creation; never mutated even if the display name changes.
    /// </summary>
    public string SkillKey { get; set; } = null!;

    /// <summary>
    /// Language of this chunk's content. Stored as int.
    /// </summary>
    public ContentLanguage Language { get; set; }

    /// <summary>
    /// Intra-module FK to <see cref="CurriculumVersion"/>. Governs visibility via Version.Status.
    /// </summary>
    public int CurriculumVersionId { get; set; }

    /// <summary>
    /// Nullable provenance reference (Page/Block within a ContentSource).
    /// Null for seeded chunks. BL-05 backfills source references.
    /// </summary>
    public string? ProvenanceRef { get; set; }

    // Navigation
    public CurriculumVersion CurriculumVersion { get; set; } = null!;
    public ICollection<ChunkEmbeddingBgeM3> Embeddings { get; set; } = new List<ChunkEmbeddingBgeM3>();
}
