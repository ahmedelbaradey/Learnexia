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
        // P7-SEC-4: IsActive and SequenceOrder must not be settable via Create/Update —
        // those are controlled exclusively by SetActive and Reorder endpoints.
        // IsActive defaults to true (entity default); SequenceOrder defaults to 0.
        CreateMap<AddUnitCommand, Unit>()
            .ForMember(d => d.IsActive, opt => opt.Ignore())
            .ForMember(d => d.SequenceOrder, opt => opt.Ignore());
        CreateMap<EditUnitCommand, Unit>()
            .ForMember(d => d.IsActive, opt => opt.Ignore())
            .ForMember(d => d.SequenceOrder, opt => opt.Ignore());

        // P7-01: Unit → UnitDto (admin DTO with IsActive).
        // SingleUnitResponse inherits UnitDto so this mapping covers it.
        CreateMap<Unit, UnitDto>();
        CreateMap<Unit, SingleUnitResponse>();
    }
}
