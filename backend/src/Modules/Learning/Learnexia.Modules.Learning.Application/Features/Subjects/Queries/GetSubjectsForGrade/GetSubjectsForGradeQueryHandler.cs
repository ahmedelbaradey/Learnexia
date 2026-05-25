using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectsForGrade;

/// <summary>
/// Handles <see cref="GetSubjectsForGradeQuery"/>.
/// Resolves the Grade entity by Number, then returns all Subjects for that Grade.
/// Empty subject list → 200 + empty collection (not 404).
/// Invalid grade number (outside 1–6) → 400. Unknown grade number → 404.
/// </summary>
public class GetSubjectsForGradeQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSubjectsForGradeQuery, BaseResponse<List<StudentSubjectDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSubjectsForGradeQueryHandler(
        ILearningRepositoryManager repository,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<List<StudentSubjectDto>>> Handle(
        GetSubjectsForGradeQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.Grade < 1 || request.Grade > 6)
                return BadRequest<List<StudentSubjectDto>>(_localizer[SharedResourcesKey.GradeOutOfRange]);

            var grade = await _repository.Learning
                .GetByCondition<Grade>(g => g.Number == request.Grade, false)
                .FirstOrDefaultAsync(cancellationToken);

            if (grade is null)
                return NotFound<List<StudentSubjectDto>>(_localizer[SharedResourcesKey.GradeNotFound]);

            var subjects = await _repository.Learning
                .GetByCondition<Subject>(s => s.GradeId == grade.Id, false)
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken);

            if (!subjects.Any())
                return EmptyCollection(new List<StudentSubjectDto>());

            var dtos = subjects.Select(s => new StudentSubjectDto
            {
                Id          = s.Id,
                Name        = s.Name,
                GradeNumber = grade.Number
            }).ToList();

            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSubjectsForGradeQuery");
            return ServerError<List<StudentSubjectDto>>(ex.Message);
        }
    }
}
