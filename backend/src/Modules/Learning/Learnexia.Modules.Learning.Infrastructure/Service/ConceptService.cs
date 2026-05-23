using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

public class ConceptService : LearningBaseService<Concept>, IConceptService
{
    public ConceptService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }
}
