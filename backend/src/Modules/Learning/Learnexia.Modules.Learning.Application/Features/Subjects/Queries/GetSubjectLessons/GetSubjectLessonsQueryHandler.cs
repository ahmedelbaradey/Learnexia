using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectLessons;

/// <summary>
/// Handles <see cref="GetSubjectLessonsQuery"/>.
/// Verifies the subject exists (404 if not), then loads Units ordered by SequenceOrder,
/// each with their Lessons ordered by SequenceOrder.
/// A subject with no units returns 200 + empty collection.
/// </summary>
public class GetSubjectLessonsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSubjectLessonsQuery, BaseResponse<List<UnitWithLessonsDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSubjectLessonsQueryHandler(
        ILearningRepositoryManager repository,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<List<UnitWithLessonsDto>>> Handle(
        GetSubjectLessonsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var subjectExists = await _repository.Learning
                .AnyAsync<Subject>(s => s.Id == request.SubjectId);

            if (!subjectExists)
                return NotFound<List<UnitWithLessonsDto>>(_localizer[SharedResourcesKey.SubjectNotFound]);

            var units = await _repository.Learning
                .GetByCondition<Unit>(u => u.SubjectId == request.SubjectId, false)
                .Include(u => u.Lessons)
                .OrderBy(u => u.SequenceOrder)
                .ToListAsync(cancellationToken);

            if (!units.Any())
                return EmptyCollection(new List<UnitWithLessonsDto>());

            var dtos = units.Select(u => new UnitWithLessonsDto
            {
                UnitId        = u.Id,
                Name          = u.Name,
                SequenceOrder = u.SequenceOrder,
                Lessons       = u.Lessons
                                 .OrderBy(l => l.SequenceOrder)
                                 .Select(l => new LessonInUnitDto
                                 {
                                     LessonId      = l.Id,
                                     Name          = l.Name,
                                     Difficulty    = l.Difficulty,
                                     SequenceOrder = l.SequenceOrder,
                                     IsLocked      = l.IsLocked,
                                     SkillId       = l.SkillId
                                 }).ToList()
            }).ToList();

            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSubjectLessonsQuery");
            return ServerError<List<UnitWithLessonsDto>>(ex.Message);
        }
    }
}
