using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A directed edge in the skill dependency graph. Connects a source <see cref="KnowledgeNode"/>
/// to a target <see cref="KnowledgeNode"/> with a typed relationship.
///
/// Self-referential on <see cref="KnowledgeNode"/>. Both FKs use <c>DeleteBehavior.Restrict</c>
/// to prevent accidental node deletions that would silently orphan edges.
///
/// Only <see cref="EdgeRelationshipType.Prerequisite"/> edges are considered in acyclicity checks
/// (see <c>SkillGraphValidator</c>). <see cref="EdgeRelationshipType.Related"/> edges are informational.
/// </summary>
public class KnowledgeEdge : AggregateRoot
{
    /// <summary>FK to the source (prerequisite / origin) node.</summary>
    public int SourceNodeId { get; set; }

    /// <summary>FK to the target (dependent / destination) node.</summary>
    public int TargetNodeId { get; set; }

    /// <summary>Relationship semantics: Prerequisite (must be mastered first) or Related (informational).</summary>
    public EdgeRelationshipType RelationshipType { get; set; }

    /// <summary>
    /// Edge weight in the range 0.0–1.0 (how strongly the source is a prerequisite for the target).
    /// Defaults to 1.0 (full prerequisite). Stored as decimal(5,4).
    /// </summary>
    public decimal Strength { get; set; } = 1.0m;

    // ── Navigation properties ────────────────────────────────────────────────────────────────────

    public KnowledgeNode SourceNode { get; set; } = null!;

    public KnowledgeNode TargetNode { get; set; } = null!;
}
