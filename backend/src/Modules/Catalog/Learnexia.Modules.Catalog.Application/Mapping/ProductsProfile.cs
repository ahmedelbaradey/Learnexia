using AutoMapper;
using Learnexia.Modules.Catalog.Application.Features.Products.Commands.Add;
using Learnexia.Modules.Catalog.Application.Features.Products.Commands.Edit;
using Learnexia.Modules.Catalog.Application.Features.Products.Dtos;
using Learnexia.Modules.Catalog.Domain.Entities;

namespace Learnexia.Modules.Catalog.Application.Mapping;

public class ProductsProfile : Profile
{
    public ProductsProfile()
    {
        CreateMap<AddProductCommand, Product>();
        CreateMap<EditProductCommand, Product>();
        CreateMap<Product, SingleProductResponse>();
    }
}
