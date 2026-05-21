using AutoMapper;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Domain.Entities;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Catalog.Infrastructure.Service.Catalog;

public class ProductService : BaseService<Product>, IProductService
{
    public ProductService(IGenericRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
        _repository = repository;
    }
}
