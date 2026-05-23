using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

public class LessonsProfile : Profile
{
    public LessonsProfile()
    {
        CreateMap<AddLessonCommand, Lesson>();
        CreateMap<EditLessonCommand, Lesson>();
        CreateMap<Lesson, SingleLessonResponse>();
    }
}
