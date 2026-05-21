using AutoMapper;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Infrastructure.Service.Catalog;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Catalog.Infrastructure.Service;

// NOTE: backend/ ServiceManager also built a MinIO-backed StorageService. Dropped — unused by Catalog.
public class ServiceManager : IServiceManager
{
    private readonly Lazy<IProductService> _productService;

    public ServiceManager(IGenericRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
    {
        _productService = new Lazy<IProductService>(() => new ProductService(repository, mapper, localizer));
    }

    public IProductService ProductService => _productService.Value;
}
