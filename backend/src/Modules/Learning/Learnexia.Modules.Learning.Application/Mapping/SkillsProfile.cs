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
        CreateMap<AddSkillCommand, Skill>();
        CreateMap<EditSkillCommand, Skill>();
        CreateMap<Skill, SingleSkillResponse>();
    }
}
