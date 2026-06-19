using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Commands.Delete;

public class DeleteGradeCommandHandler : BaseResponseHandler, ICommandHandler<DeleteGradeCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteGradeCommandHandler(ILearningServiceManager service, ILoggerManager logger, IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _service = service;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(DeleteGradeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Pre-fetch: return 404 rather than letting the base DeleteAsync throw InvalidOperationException
            // when the id does not exist (mirrors DeleteSubjectCommandHandler / DeleteUnitCommandHandler pattern).
            var grade = await _service.GradeService.GetGradeTrackedAsync(request.Id, cancellationToken);
            if (grade is null)
                return NotFound<string>(_localizer[SharedResourcesKey.GradeNotFound]);

            // "Grade not empty" guard: block delete when non-deleted Subjects still reference this grade.
            // Mirrors SubjectHasUnitsAsync / UnitHasLessonsAsync pattern.
            var hasSubjects = await _service.GradeService.GradeHasSubjectsAsync(request.Id, cancellationToken);
            if (hasSubjects)
                return BadRequest<string>(_localizer[SharedResourcesKey.GradeNotEmpty]);

            return await _service.GradeService.DeleteAsync(request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteGradeCommand");
            return ServerError<string>();
        }
    }
}
