namespace Learnexia.Modules.Catalog.Application.Abstractions;

// NOTE: backend/ IServiceManager also exposed StorageService (MinIO). Dropped — no Catalog
// handler uses storage. Add a Shared.Contracts storage seam if a future feature needs it.
public interface IServiceManager
{
    IProductService ProductService { get; }
}
