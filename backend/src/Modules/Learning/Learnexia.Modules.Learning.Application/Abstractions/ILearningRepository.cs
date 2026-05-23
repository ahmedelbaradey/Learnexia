using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Module-local generic repository seam for the Learning module. Mirrors Catalog's repository seam.
/// In a deferred-commit module, repository writes stage changes only — the per-module
/// UnitOfWorkBehavior owns the commit (ADR 0001/0002). Implementations must NOT call SaveChangesAsync.
/// </summary>
public interface ILearningRepository : IGenericRepository
{
}
