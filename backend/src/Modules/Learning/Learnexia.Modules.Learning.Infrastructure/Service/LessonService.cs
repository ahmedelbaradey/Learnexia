using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

public class LessonService : LearningBaseService<Lesson>, ILessonService
{
    public LessonService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }
}
