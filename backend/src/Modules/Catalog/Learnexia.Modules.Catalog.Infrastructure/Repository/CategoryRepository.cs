using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Infrastructure.Persistence;

namespace Learnexia.Modules.Catalog.Infrastructure.Repository;

public class CategoryRepository : GenericRepository, ICategoryRepository
{
    public CategoryRepository(CatalogDbContext repositoryContext, ICurrentUserService currentUserService)
        : base(repositoryContext, currentUserService)
    {
    }
}
