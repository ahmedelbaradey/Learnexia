using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Service seam for KnowledgeGraph admin authoring (AddEdge, RemoveEdge) and graph read queries
/// (GetGraph, GetPrerequisites, GetUnlockedBy). All EF calls — AnyAsync, ToListAsync, Select,
/// IgnoreQueryFilters — live inside the Infrastructure implementation; Application stays EF-free.
/// </summary>
public interface IKnowledgeGraphService
{
    // ── AddEdge guard helpers ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the KnowledgeNode for the given id (non-tracked), or null when not found.
    /// Global IsDeleted filter applies so soft-deleted nodes are excluded.
    /// </summary>
    Task<KnowledgeNode?> GetNodeByIdAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the Subject (Language field populated) for the given KnowledgeNode via
    /// KnowledgeNode.SubjectId. Returns null when the node or its subject cannot be resolved.
    /// Used by the cross-language guard in AddKnowledgeEdgeCommandHandler.
    /// AsNoTracking, IgnoreQueryFilters on Subject so structural resolution always works.
    /// </summary>
    Task<Subject?> GetSubjectForNodeAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns true when a live KnowledgeEdge with the same (SourceNodeId, TargetNodeId, RelationshipType)
    /// triple already exists. Used by the duplicate guard in AddKnowledgeEdgeCommandHandler.
    /// </summary>
    Task<bool> EdgeDuplicateExistsAsync(int sourceNodeId, int targetNodeId, int relationshipType, CancellationToken ct = default);

    /// <summary>
    /// Returns all live (non-deleted) Prerequisite-typed KnowledgeEdge rows.
    /// Used by SkillGraphValidator.AssertAcyclic before inserting a new Prerequisite edge.
    /// </summary>
    Task<List<KnowledgeEdge>> GetAllPrerequisiteEdgesAsync(CancellationToken ct = default);

    /// <summary>
    /// Stages a new KnowledgeEdge via AddAsync. Returns the tracked instance so the handler
    /// can raise a domain event on it.
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task<KnowledgeEdge> StageAddEdgeAsync(KnowledgeEdge edge, CancellationToken ct = default);

    // ── RemoveEdge path ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked KnowledgeEdge for the given id (EF change-tracking ON), or null when not found.
    /// The caller sets IsDeleted = true and the UoW behavior commits the soft-delete.
    /// </summary>
    Task<KnowledgeEdge?> GetEdgeTrackedAsync(int edgeId, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked KnowledgeEdge entity (calls repository UpdateAsync).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageEdgeUpdateAsync(KnowledgeEdge edge, CancellationToken ct = default);

    // ── GetGraph query path ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all live (non-deleted) KnowledgeNodes for the given subject.
    /// Admin view — includes nodes that wrap inactive skills (IgnoreQueryFilters NOT applied;
    /// the global soft-delete filter excludes deleted nodes; IsActive is not filtered here).
    /// </summary>
    Task<List<KnowledgeNode>> GetGraphNodesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all live (non-deleted) KnowledgeEdges where both source and target belong to the
    /// given subject. Used by GetGraphQueryHandler.
    /// </summary>
    Task<List<KnowledgeEdge>> GetGraphEdgesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Given a set of SkillIds, returns the subset whose corresponding Skill.IsActive == true.
    /// Used by GetGraphQueryHandler to build the skillActiveMap for node DTOs.
    /// Returns a dictionary of SkillId → IsActive.
    /// </summary>
    Task<Dictionary<int, bool>> GetSkillActiveMapAsync(IReadOnlyCollection<int> skillIds, CancellationToken ct = default);

    // ── GetPrerequisites / GetUnlockedBy query paths ──────────────────────────────────────────────

    /// <summary>
    /// Returns true when a non-deleted KnowledgeNode with the given id exists.
    /// Used by GetPrerequisitesQueryHandler and GetUnlockedByQueryHandler for the 404 check.
    /// </summary>
    Task<bool> NodeExistsAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns nodes that are sources of Prerequisite edges targeting the given nodeId
    /// ("what must be mastered before this node"). AsNoTracking.
    /// </summary>
    Task<List<KnowledgeNode>> GetPrerequisiteNodesAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns nodes that are targets of Prerequisite edges sourced from the given nodeId
    /// ("what does mastering this node unlock"). AsNoTracking.
    /// </summary>
    Task<List<KnowledgeNode>> GetUnlockedByNodeAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Given a set of SkillIds, returns the subset whose corresponding Skill.IsActive == true
    /// as a HashSet for O(1) membership tests.
    /// Used by GetPrerequisitesQueryHandler and GetUnlockedByQueryHandler to filter inactive-skill
    /// nodes before mapping to StudentKnowledgeNodeDto (security: students must not discover hidden skills).
    /// </summary>
    Task<HashSet<int>> GetActiveSkillIdsAsync(IReadOnlyCollection<int> skillIds, CancellationToken ct = default);

    // ── BL-03 BE-4: GetRelatedConcepts ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns nodes connected to the given nodeId via <see cref="EdgeRelationshipType.Related"/> edges
    /// (either direction: source or target). Inactive-skill nodes are NOT filtered here — the handler
    /// applies the student-safe filter after calling this method.
    /// </summary>
    Task<List<KnowledgeNode>> GetRelatedNodesAsync(int nodeId, CancellationToken ct = default);

    // ── BL-03 BE-5: GetRemediationPath (transitive BFS prerequisite chain) ──────────────────────

    /// <summary>
    /// Returns all Prerequisite-typed edges for the full graph (used by remediation BFS in the handler).
    /// Same as <see cref="GetAllPrerequisiteEdgesAsync"/> semantically — materialized for BFS traversal.
    /// </summary>
    Task<List<KnowledgeEdge>> GetPrerequisiteEdgesForRemediationAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the KnowledgeNode for the given id (non-tracked), or null when not found.
    /// Used by GetRemediationPathQueryHandler for the 404 check and node detail lookup.
    /// </summary>
    Task<KnowledgeNode?> GetNodeForRemediationAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns all live (non-deleted) KnowledgeNode rows whose Id is in the given set.
    /// Single <c>WHERE Id IN (...)</c> round-trip — used by GetRemediationPathQueryHandler to
    /// batch-load all BFS result nodes instead of N separate round-trips.
    /// AsNoTracking. Absent ids (deleted or never existed) are silently omitted.
    /// </summary>
    Task<Dictionary<int, KnowledgeNode>> GetNodesByIdsAsync(IReadOnlyCollection<int> nodeIds, CancellationToken ct = default);
}
