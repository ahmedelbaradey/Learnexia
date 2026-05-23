using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Infrastructure.Repository;

/// <summary>
/// Provides a single entry-point to all Learning module repositories.
/// Mirrors Catalog's <c>RepositoryManager</c> pattern.
/// </summary>
public sealed class LearningRepositoryManager : ILearningRepositoryManager
{
    private readonly Lazy<ILearningRepository> _learning;

    public LearningRepositoryManager(LearningDbContext dbContext, ICurrentUserService currentUserService)
    {
        _learning = new Lazy<ILearningRepository>(() => new LearningRepository(dbContext, currentUserService));
    }

    public ILearningRepository Learning => _learning.Value;
}
