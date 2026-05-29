using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Module-local generic repository seam for the Learning module. Mirrors Catalog's repository seam.
/// In a deferred-commit module, repository writes stage changes only — the per-module
/// UnitOfWorkBehavior owns the commit (ADR 0001/0002). Implementations must NOT call SaveChangesAsync.
/// </summary>
public interface ILearningRepository : IGenericRepository
{
    // ── Skill dependency graph (P2-11 BE-5) ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the set of <see cref="KnowledgeNode"/> rows that are <em>sources</em> of
    /// <c>Prerequisite</c> edges whose <c>TargetNodeId == nodeId</c>
    /// (i.e. "what must be mastered before this node").
    /// </summary>
    Task<List<KnowledgeNode>> GetPrerequisiteNodesAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns the set of <see cref="KnowledgeNode"/> rows that are <em>targets</em> of
    /// <c>Prerequisite</c> edges whose <c>SourceNodeId == nodeId</c>
    /// (i.e. "what does mastering this node unlock").
    /// </summary>
    Task<List<KnowledgeNode>> GetUnlockedByNodeAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if a <see cref="KnowledgeNode"/> with the given <paramref name="nodeId"/> exists.
    /// </summary>
    Task<bool> KnowledgeNodeExistsAsync(int nodeId, CancellationToken ct = default);
}
