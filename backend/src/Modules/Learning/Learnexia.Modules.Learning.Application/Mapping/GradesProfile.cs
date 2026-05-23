using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

public class GradesProfile : Profile
{
    public GradesProfile()
    {
        CreateMap<AddGradeCommand, Grade>();
        CreateMap<EditGradeCommand, Grade>();
        CreateMap<Grade, SingleGradeResponse>();
    }
}
