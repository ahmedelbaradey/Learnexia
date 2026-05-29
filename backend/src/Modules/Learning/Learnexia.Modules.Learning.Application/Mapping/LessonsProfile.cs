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
        // Lesson → SingleLessonResponse: Explanation + Visual auto-map by name (P2-05 new columns).
        // QuickCheck is filled manually in GetLessonQueryHandler — NOT by AutoMapper from Lesson
        // (Lesson has no QuickCheck nav property). CorrectAnswer exclusion is in QuizProfile.
        CreateMap<Lesson, SingleLessonResponse>()
            .ForMember(dest => dest.QuickCheck, opt => opt.Ignore());
    }
}
