using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetLanguageCoverage;

/// <summary>
/// Handles <see cref="GetSubjectLanguageCoverageQuery"/>.
///
/// Expected slots for a grade are the 6 canonical (SubjectCode, Language) pairs:
///   MATH/Ar, MATH/En, SCIENCE/Ar, SCIENCE/En, ARABIC/Ar, ENGLISH/En
/// (Arabic pinned to Ar; English pinned to En — no cross-language slot expected for these two.)
///
/// Admin read — soft-deleted subjects are excluded by the global query filter (only non-deleted trees
/// are "present"), and IsActive filtering is NOT applied (admins see inactive too).
///
/// Option C: all EF queries delegated to ISubjectService. Handler injects only ILearningServiceManager.
/// </summary>
public class GetSubjectLanguageCoverageQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSubjectLanguageCoverageQuery, BaseResponse<SubjectLanguageCoverageReportDto>>
{
    private readonly ILearningServiceManager _service;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    // The 6 expected (SubjectCode, ContentLanguage) slots per grade.
    private static readonly (SubjectCode Code, ContentLanguage Lang)[] ExpectedSlots =
    {
        (SubjectCode.MATH,    ContentLanguage.Ar),
        (SubjectCode.MATH,    ContentLanguage.En),
        (SubjectCode.SCIENCE, ContentLanguage.Ar),
        (SubjectCode.SCIENCE, ContentLanguage.En),
        (SubjectCode.ARABIC,  ContentLanguage.Ar),   // pinned
        (SubjectCode.ENGLISH, ContentLanguage.En),   // pinned
    };

    public GetSubjectLanguageCoverageQueryHandler(
        ILearningServiceManager service,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<SubjectLanguageCoverageReportDto>> Handle(
        GetSubjectLanguageCoverageQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.GradeId <= 0)
                return BadRequest<SubjectLanguageCoverageReportDto>(_localizer[SharedResourcesKey.EmptyIdValidation]);

            var grade = await _service.SubjectService.GetGradeByIdAsync(request.GradeId, cancellationToken);
            if (grade is null)
                return NotFound<SubjectLanguageCoverageReportDto>(_localizer[SharedResourcesKey.GradeNotFound]);

            // Load all non-deleted subjects for this grade (global IsDeleted filter is active).
            // Admin read: no IsActive filter — show inactive trees too.
            var subjects = await _service.SubjectService.GetSubjectsForGradeAdminAsync(request.GradeId, cancellationToken);

            var subjectLookup = subjects
                .ToDictionary(s => (s.SubjectCode, s.Language));

            var coverage = ExpectedSlots.Select(slot =>
            {
                var exists = subjectLookup.TryGetValue((slot.Code, slot.Lang), out var subject);
                return new SubjectLanguageCoverageDto
                {
                    SubjectCode = slot.Code,
                    Language    = slot.Lang,
                    Exists      = exists,
                    SubjectId   = exists ? subject!.Id : null,
                    Name        = exists ? subject!.Name : null,
                    IsActive    = exists ? subject!.IsActive : null,
                };
            }).ToList();

            var report = new SubjectLanguageCoverageReportDto
            {
                GradeId     = grade.Id,
                GradeNumber = grade.Number,
                Coverage    = coverage,
            };

            return Success(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSubjectLanguageCoverageQuery");
            return ServerError<SubjectLanguageCoverageReportDto>();
        }
    }
}
