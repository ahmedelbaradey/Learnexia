using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

public class SkillsProfile : Profile
{
    public SkillsProfile()
    {
        // Mass-assignment guard: IsActive and IsDeleted must never be overwritten
        // by caller-supplied data. Only explicit commands (SetSkillActive / soft-delete)
        // may change these fields.
        CreateMap<AddSkillCommand, Skill>()
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

        CreateMap<EditSkillCommand, Skill>()
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore());

        CreateMap<Skill, SingleSkillResponse>();
    }
}
