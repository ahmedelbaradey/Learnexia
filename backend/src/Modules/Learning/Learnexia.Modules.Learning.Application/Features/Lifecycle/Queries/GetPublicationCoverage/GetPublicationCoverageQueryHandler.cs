using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Queries.GetPublicationCoverage;

/// <summary>
/// Handles <see cref="GetPublicationCoverageQuery"/>.
///
/// Reports publication state per (SubjectCode, Language) slot for a grade.
/// Expected slots mirror <c>GetSubjectLanguageCoverageQuery</c>: 6 canonical slots.
/// Augments each slot with <see cref="LifecycleState"/> and <see cref="PublicationCoverageSlotDto.IsPublished"/>.
///
/// Admin read — no IsActive filter applied; global IsDeleted filter is still active.
/// Does NOT apply LifecycleState filter — shows all states (Draft/Published/Archived) so admins
/// can see exactly which trees have been published and which remain draft.
///
/// Option C: all EF access delegated to ILifecycleService via ILearningServiceManager.
/// This handler injects only ILearningServiceManager — no ILearningRepositoryManager, no EF types.
/// </summary>
public class GetPublicationCoverageQueryHandler
    : BaseResponseHandler, IQueryHandler<GetPublicationCoverageQuery, BaseResponse<PublicationCoverageReportDto>>
{
    // The 6 expected (SubjectCode, ContentLanguage) slots per grade — same as P7-01-BE-8.
    private static readonly (SubjectCode Code, ContentLanguage Lang)[] ExpectedSlots =
    {
        (SubjectCode.MATH,    ContentLanguage.Ar),
        (SubjectCode.MATH,    ContentLanguage.En),
        (SubjectCode.SCIENCE, ContentLanguage.Ar),
        (SubjectCode.SCIENCE, ContentLanguage.En),
        (SubjectCode.ARABIC,  ContentLanguage.Ar),   // pinned
        (SubjectCode.ENGLISH, ContentLanguage.En),   // pinned
    };

    private readonly ILearningServiceManager _service;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetPublicationCoverageQueryHandler(
        ILearningServiceManager service,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<PublicationCoverageReportDto>> Handle(
        GetPublicationCoverageQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.GradeId <= 0)
                return BadRequest<PublicationCoverageReportDto>(_localizer[SharedResourcesKey.EmptyIdValidation]);

            var grade = await _service.LifecycleService
                .GetGradeByIdNoTrackingAsync(request.GradeId, cancellationToken);

            if (grade is null)
                return NotFound<PublicationCoverageReportDto>(_localizer[SharedResourcesKey.GradeNotFound]);

            // Load all non-deleted subjects for this grade. Admin read: no IsActive filter.
            var subjects = await _service.LifecycleService
                .GetSubjectsForCoverageAsync(request.GradeId, cancellationToken);

            var subjectLookup = subjects.ToDictionary(s => (s.SubjectCode, s.Language));

            var coverage = ExpectedSlots.Select(slot =>
            {
                var exists = subjectLookup.TryGetValue((slot.Code, slot.Lang), out var subject);
                return new PublicationCoverageSlotDto
                {
                    SubjectCode    = slot.Code,
                    Language       = slot.Lang,
                    Exists         = exists,
                    SubjectId      = exists ? subject!.Id : null,
                    Name           = exists ? subject!.Name : null,
                    IsActive       = exists ? subject!.IsActive : null,
                    LifecycleState = exists ? subject!.LifecycleState : null,
                };
            }).ToList();

            var report = new PublicationCoverageReportDto
            {
                GradeId     = grade.Id,
                GradeNumber = grade.Number,
                Coverage    = coverage,
            };

            var result = Success(report);
            result.Message = _localizer[SharedResourcesKey.PublicationCoverageRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error: in GetPublicationCoverageQuery for GradeId={request.GradeId}");
            return ServerError<PublicationCoverageReportDto>();
        }
    }
}
