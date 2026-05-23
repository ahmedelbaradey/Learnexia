using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

public class SubjectsProfile : Profile
{
    public SubjectsProfile()
    {
        CreateMap<AddSubjectCommand, Subject>();
        CreateMap<EditSubjectCommand, Subject>();
        CreateMap<Subject, SingleSubjectResponse>();
    }
}
