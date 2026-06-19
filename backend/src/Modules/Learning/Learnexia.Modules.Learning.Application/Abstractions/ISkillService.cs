using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface ISkillService : IBaseService<Skill>
{
    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when a Concept with the given id exists (non-deleted global filter applies).
    /// Used by AddSkillCommandHandler to verify the parent Concept before staging the insert.
    /// </summary>
    Task<bool> ConceptExistsAsync(int conceptId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the Subject (with SubjectId, GradeId, Language) for the given Concept id via
    /// Concept.SubjectId. Returns null when the concept or its subject cannot be found.
    /// Used by AddSkillCommandHandler to populate auto-created KnowledgeNode fields.
    /// AsNoTracking.
    /// </summary>
    Task<Subject?> GetSubjectByConceptIdAsync(int conceptId, CancellationToken ct = default);

    /// <summary>
    /// Stages a new Skill via AddAsync. Returns the tracked instance so the handler can reference
    /// the skill object (e.g. when creating the wrapping KnowledgeNode with Skill = skill).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task<Skill> StageAddSkillAsync(Skill skill, CancellationToken ct = default);

    /// <summary>
    /// Stages a new KnowledgeNode via AddAsync (used by AddSkillCommandHandler to auto-create
    /// the wrapping node in the same UoW transaction).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageAddKnowledgeNodeAsync(KnowledgeNode node, CancellationToken ct = default);

    // ── Delete path ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked Skill for the given id (EF change-tracking ON), or null when not found.
    /// The caller mutates the entity and the UoW behavior commits the change.
    /// </summary>
    Task<Skill?> GetSkillTrackedAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Returns the KnowledgeNode whose SkillId == skillId, or null if none exists.
    /// Uses IgnoreQueryFilters (soft-deleted nodes are visible here for the cascade-delete path).
    /// trackChanges=true so the caller can mutate the entity inline.
    /// </summary>
    Task<KnowledgeNode?> GetNodeBySkillIdTrackedAsync(int skillId, CancellationToken ct = default);

    /// <summary>
    /// Returns all live (non-deleted) KnowledgeEdge rows where either
    /// SourceNodeId == nodeId or TargetNodeId == nodeId, with change-tracking ON.
    /// Used by the skill soft-delete cascade to find and soft-delete referencing edges.
    /// </summary>
    Task<List<KnowledgeEdge>> GetEdgesForNodeTrackedAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked Skill entity (calls repository UpdateAsync).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageSkillUpdateAsync(Skill skill, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked KnowledgeNode entity.
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageNodeUpdateAsync(KnowledgeNode node, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked KnowledgeEdge entity.
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageEdgeUpdateAsync(KnowledgeEdge edge, CancellationToken ct = default);

    // ── SetActive path ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked Skill for the given id (EF change-tracking ON), or null when not found.
    /// Used by SetSkillActiveCommandHandler and EditSkillCommandHandler (post-update identity-map re-fetch).
    /// </summary>
    Task<Skill?> GetSkillTrackedByIdAsync(int skillId, CancellationToken ct = default);

    // ── List/pagination ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated, materialized list of Skills projected to <see cref="SingleSkillResponse"/>.
    /// When <paramref name="conceptId"/> is provided only skills for that concept are returned.
    /// When <paramref name="search"/> is provided only skills whose name contains it are returned (CUR-TC-66).
    /// All EF + ProjectTo + pagination composition stays inside Infrastructure (Option C).
    /// </summary>
    Task<PaginatedResult<SingleSkillResponse>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? orderBy,
        int? conceptId,
        string? search,
        CancellationToken ct = default);
}
