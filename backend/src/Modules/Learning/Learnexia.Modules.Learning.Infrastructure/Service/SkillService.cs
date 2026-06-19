using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

public class SkillService : LearningBaseService<Skill>, ISkillService
{
    public SkillService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> ConceptExistsAsync(int conceptId, CancellationToken ct = default)
        => await _repository.AnyAsync<Concept>(c => c.Id == conceptId);

    /// <inheritdoc />
    public async Task<Subject?> GetSubjectByConceptIdAsync(int conceptId, CancellationToken ct = default)
        => await _repository.GetSubjectByConceptIdAsync(conceptId, ct);

    /// <inheritdoc />
    public async Task<Skill> StageAddSkillAsync(Skill skill, CancellationToken ct = default)
    {
        await _repository.AddAsync(skill, ct);
        return skill;
    }

    /// <inheritdoc />
    public async Task StageAddKnowledgeNodeAsync(KnowledgeNode node, CancellationToken ct = default)
        => await _repository.AddAsync(node, ct);

    // ── Delete path ───────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Skill?> GetSkillTrackedAsync(int id, CancellationToken ct = default)
        => await _repository
            .GetByCondition<Skill>(s => s.Id == id, trackChanges: true)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<KnowledgeNode?> GetNodeBySkillIdTrackedAsync(int skillId, CancellationToken ct = default)
        // Delegates to the repository method which uses IgnoreQueryFilters for cascade-delete visibility.
        // GetNodeBySkillIdAsync does NOT enforce trackChanges via IGenericRepository but the entity
        // returned will be tracked if the DbContext is the same scoped instance (which it is here).
        => await _repository.GetNodeBySkillIdAsync(skillId, ct);

    /// <inheritdoc />
    public async Task<List<KnowledgeEdge>> GetEdgesForNodeTrackedAsync(int nodeId, CancellationToken ct = default)
        => await _repository.GetEdgesForNodeAsync(nodeId, trackChanges: true, ct);

    /// <inheritdoc />
    public async Task StageSkillUpdateAsync(Skill skill, CancellationToken ct = default)
        => await _repository.UpdateAsync(skill);

    /// <inheritdoc />
    public async Task StageNodeUpdateAsync(KnowledgeNode node, CancellationToken ct = default)
        => await _repository.UpdateAsync(node);

    /// <inheritdoc />
    public async Task StageEdgeUpdateAsync(KnowledgeEdge edge, CancellationToken ct = default)
        => await _repository.UpdateAsync(edge);

    // ── SetActive path ────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Skill?> GetSkillTrackedByIdAsync(int skillId, CancellationToken ct = default)
        => await _repository
            .GetByCondition<Skill>(s => s.Id == skillId, trackChanges: true)
            .FirstOrDefaultAsync(ct);

    // ── List/pagination ───────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PaginatedResult<SingleSkillResponse>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? orderBy,
        int? conceptId,
        string? search,
        CancellationToken ct = default)
    {
        // IQueryable composition stays entirely inside Infrastructure — no EF type crosses the boundary.
        var query = conceptId.HasValue
            ? _repository.GetByCondition<Skill>(s => s.ConceptId == conceptId.Value, trackChanges: false)
            : _repository.GetAll<Skill>(trackChanges: false);

        // CUR-TC-66: apply the Search filter (case-insensitive name contains) — was previously dropped.
        // EF.Functions.ILike → Postgres ILIKE (parameterized; case-insensitive).
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => EF.Functions.ILike(s.Name, $"%{search}%"));

        if (!query.Any())
            return PaginatedResult<SingleSkillResponse>.EmptyCollection();

        return await _mapper
            .ProjectTo<SingleSkillResponse>(query)
            .ToPaginatedListAsync(pageNumber, pageSize, orderBy);
    }
}
