using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

public class UnitsProfile : Profile
{
    public UnitsProfile()
    {
        CreateMap<AddUnitCommand, Unit>();
        CreateMap<EditUnitCommand, Unit>();
        CreateMap<Unit, SingleUnitResponse>();
    }
}
