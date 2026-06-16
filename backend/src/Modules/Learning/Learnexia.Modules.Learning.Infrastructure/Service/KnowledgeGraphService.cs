using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="IKnowledgeGraphService"/>.
/// All EF calls (AnyAsync, ToListAsync, Select, IgnoreQueryFilters, FirstOrDefaultAsync)
/// stay inside this class — Application handlers call only materialized-result service methods.
/// </summary>
public class KnowledgeGraphService : LearningBaseService<KnowledgeEdge>, IKnowledgeGraphService
{
    public KnowledgeGraphService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    // ── AddEdge guard helpers ─────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<KnowledgeNode?> GetNodeByIdAsync(int nodeId, CancellationToken ct = default)
        => await _repository.GetKnowledgeNodeByIdAsync(nodeId, trackChanges: false, ct);

    /// <inheritdoc />
    public async Task<Subject?> GetSubjectForNodeAsync(int nodeId, CancellationToken ct = default)
        => await _repository.GetSubjectForNodeAsync(nodeId, ct);

    /// <inheritdoc />
    public async Task<bool> EdgeDuplicateExistsAsync(int sourceNodeId, int targetNodeId, int relationshipType, CancellationToken ct = default)
        => await _repository.KnowledgeEdgeDuplicateExistsAsync(sourceNodeId, targetNodeId, relationshipType, ct);

    /// <inheritdoc />
    public async Task<List<KnowledgeEdge>> GetAllPrerequisiteEdgesAsync(CancellationToken ct = default)
        => await _repository.GetAllPrerequisiteEdgesAsync(ct);

    /// <inheritdoc />
    public async Task<KnowledgeEdge> StageAddEdgeAsync(KnowledgeEdge edge, CancellationToken ct = default)
    {
        await _repository.AddAsync(edge, ct);
        return edge;
    }

    // ── RemoveEdge path ───────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<KnowledgeEdge?> GetEdgeTrackedAsync(int edgeId, CancellationToken ct = default)
        => await _repository.GetKnowledgeEdgeByIdAsync(edgeId, trackChanges: true, ct);

    /// <inheritdoc />
    public async Task StageEdgeUpdateAsync(KnowledgeEdge edge, CancellationToken ct = default)
        => await _repository.UpdateAsync(edge);

    // ── GetGraph query path ───────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<KnowledgeNode>> GetGraphNodesAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetGraphNodesAsync(subjectId, ct);

    /// <inheritdoc />
    public async Task<List<KnowledgeEdge>> GetGraphEdgesAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetGraphEdgesAsync(subjectId, ct);

    /// <inheritdoc />
    public async Task<Dictionary<int, bool>> GetSkillActiveMapAsync(IReadOnlyCollection<int> skillIds, CancellationToken ct = default)
    {
        if (skillIds.Count == 0)
            return new Dictionary<int, bool>();

        var rows = await _repository
            .GetByCondition<Skill>(s => skillIds.Contains(s.Id), trackChanges: false)
            .Select(s => new { s.Id, s.IsActive })
            .ToListAsync(ct);

        return rows.ToDictionary(s => s.Id, s => s.IsActive);
    }

    // ── GetPrerequisites / GetUnlockedBy query paths ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> NodeExistsAsync(int nodeId, CancellationToken ct = default)
        => await _repository.KnowledgeNodeExistsAsync(nodeId, ct);

    /// <inheritdoc />
    public async Task<List<KnowledgeNode>> GetPrerequisiteNodesAsync(int nodeId, CancellationToken ct = default)
        => await _repository.GetPrerequisiteNodesAsync(nodeId, ct);

    /// <inheritdoc />
    public async Task<List<KnowledgeNode>> GetUnlockedByNodeAsync(int nodeId, CancellationToken ct = default)
        => await _repository.GetUnlockedByNodeAsync(nodeId, ct);

    /// <inheritdoc />
    public async Task<HashSet<int>> GetActiveSkillIdsAsync(IReadOnlyCollection<int> skillIds, CancellationToken ct = default)
    {
        if (skillIds.Count == 0)
            return new HashSet<int>();

        var ids = await _repository
            .GetByCondition<Skill>(s => skillIds.Contains(s.Id) && s.IsActive, trackChanges: false)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }
}
