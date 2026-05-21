using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Infrastructure.Persistence;

namespace Learnexia.Modules.Catalog.Infrastructure.Repository;

public sealed class RepositoryManager : IRepositoryManager
{
    private readonly Lazy<ICategoryRepository> _categoryRepository;

    public RepositoryManager(CatalogDbContext repositoryContext, ICurrentUserService currentUserService)
    {
        _categoryRepository = new Lazy<ICategoryRepository>(() => new CategoryRepository(repositoryContext, currentUserService));
    }

    public ICategoryRepository Categories => _categoryRepository.Value;
}
