using AutoMapper;
using Learnexia.Modules.Catalog.Application.Features.Categories.Commands.Add;
using Learnexia.Modules.Catalog.Application.Features.Categories.Dtos;
using Learnexia.Modules.Catalog.Domain.Entities;

namespace Learnexia.Modules.Catalog.Application.Mapping;

public class CategoriesProfile : Profile
{
    public CategoriesProfile()
    {
        CreateMap<AddCategoryCommand, Category>();
        CreateMap<Category, SingleCategoryResponse>();
    }
}
