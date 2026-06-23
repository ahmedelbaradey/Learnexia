using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// Many-to-many mapping between a <see cref="CurriculumChunk"/> and its physical provenance
/// in the source documents (provenance tree → pedagogical tree bridge).
/// (curriculum-system-of-record.md §1c, BL-04 BE-8)
///
/// Design rules:
/// - <see cref="ChunkId"/> is an intra-module FK to <see cref="CurriculumChunk"/> (real EF nav OK).
/// - <see cref="ContentSourceId"/> is an intra-module FK to <see cref="ContentSource"/>.
/// - <see cref="ChapterId"/> is an intra-module nullable FK to <see cref="Chapter"/>.
/// - <see cref="PedagogicalNodeId"/> is a plain int (no cross-module FK/nav — cross-module isolation).
/// - <see cref="PedagogicalNodeType"/> is a free-form string (e.g. "Lesson", "Concept").
/// </summary>
public class ProvenanceMapping : AggregateRoot
{
    /// <summary>
    /// Intra-module FK to <see cref="CurriculumChunk"/>.
    /// </summary>
    public int ChunkId { get; set; }

    /// <summary>
    /// Intra-module FK to <see cref="ContentSource"/>.
    /// </summary>
    public int ContentSourceId { get; set; }

    /// <summary>
    /// Optional intra-module FK to <see cref="Chapter"/>.
    /// Null when the mapping is at source level (not chapter-specific).
    /// </summary>
    public int? ChapterId { get; set; }

    /// <summary>
    /// Free-form reference to the exact page or block within the chapter/source
    /// (e.g. "p.47" or "block-3").
    /// </summary>
    public string PageOrBlockRef { get; set; } = null!;

    /// <summary>
    /// Loose reference to a node in the pedagogical tree (no cross-module FK).
    /// Cross-module isolation: PedagogicalNodeId is a plain int referencing
    /// learning module entities (Lesson.Id, Concept.Id, etc.).
    /// </summary>
    public int PedagogicalNodeId { get; set; }

    /// <summary>
    /// Describes the type of the pedagogical node (e.g. "Lesson", "Concept").
    /// Free-form string — curriculum module owns this classification.
    /// </summary>
    public string PedagogicalNodeType { get; set; } = null!;

    // Navigation
    public CurriculumChunk Chunk { get; set; } = null!;
    public ContentSource ContentSource { get; set; } = null!;
    public Chapter? Chapter { get; set; }
}
