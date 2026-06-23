using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A node in the skill dependency graph. A node may wrap a known <see cref="Skill"/> (when
/// <see cref="SkillId"/> is non-null) or represent a standalone concept / review checkpoint.
///
/// Lead decision Q1 (P2-11): KnowledgeNode wraps Skill via nullable SkillId FK with a filtered
/// unique index (UX_KnowledgeNodes_SkillId WHERE SkillId IS NOT NULL). No changes to Skill itself.
/// </summary>
public class KnowledgeNode : AggregateRoot
{
    /// <summary>Node display name (max 200 characters).</summary>
    public string Name { get; set; } = null!;

    /// <summary>Classifies the node as a Skill, Concept, or Review checkpoint.</summary>
    public KnowledgeNodeType NodeType { get; set; }

    /// <summary>FK to <see cref="Subject"/> (same-module, no cross-module FK). Not a navigation property at MVP.</summary>
    public int SubjectId { get; set; }

    /// <summary>FK to <see cref="Grade"/> (same-module). Not a navigation property at MVP.</summary>
    public int GradeId { get; set; }

    /// <summary>
    /// Difficulty on a 1–5 integer scale (SRS §6). Intentionally a plain int rather than
    /// <c>DifficultyLevel</c> enum to match the SRS definition and avoid coupling to that enum.
    /// </summary>
    public int Difficulty { get; set; }

    /// <summary>
    /// Optional FK to <see cref="Skill"/>. Non-null when this node wraps an existing curriculum Skill.
    /// Enforced unique by a filtered index (UX_KnowledgeNodes_SkillId WHERE SkillId IS NOT NULL).
    /// </summary>
    public int? SkillId { get; set; }

    public Skill? Skill { get; set; }

    /// <summary>
    /// Stable human-readable semantic identifier for cross-version identity (BL-04 BE-11/12, Decision C).
    /// Format: {SubjectCode}.grade{N}.{unitSlug}.{skillSlug}
    /// Example: math.grade4.fractions.add-unlike-denominators
    /// Slugify = lowercase, spaces→hyphens, strip non-alphanumeric except hyphens.
    ///
    /// Rules:
    /// - Set at creation time. Immutable once non-null — an explicit correction command is required to change it.
    /// - Nullable (Q3 decision): NOT NULL constraint is deferred until all writers confirm readiness.
    /// - Partial unique index UX_KnowledgeNodes_SkillKey WHERE SkillKey IS NOT NULL.
    ///   Mirrors UX_KnowledgeNodes_SkillId (see KnowledgeNodeConfig).
    /// </summary>
    public string? SkillKey { get; set; }

    // ── Inverse navigation collections ──────────────────────────────────────────────────────────

    /// <summary>Edges that originate from this node (this node is the prerequisite/source).</summary>
    public ICollection<KnowledgeEdge> SourceEdges { get; set; } = new List<KnowledgeEdge>();

    /// <summary>Edges that point to this node (this node is the target that requires prerequisites).</summary>
    public ICollection<KnowledgeEdge> TargetEdges { get; set; } = new List<KnowledgeEdge>();
}
