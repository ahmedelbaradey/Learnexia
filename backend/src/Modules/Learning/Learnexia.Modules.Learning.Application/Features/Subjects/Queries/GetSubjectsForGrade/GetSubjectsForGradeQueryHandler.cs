using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Application.Helpers;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectsForGrade;

/// <summary>
/// Handles <see cref="GetSubjectsForGradeQuery"/>.
/// Resolves the Grade entity by Number, then returns exactly one Subject per <see cref="SubjectCode"/>
/// at the language resolved by <see cref="SubjectLanguageResolver"/> for the authenticated student.
///
/// P8-03: For each of the four subject codes the handler:
///   1. Reads the student's <c>learning_language</c> JWT claim via <see cref="LearningLanguageClaimAccessor"/>.
///   2. Resolves the effective <see cref="ContentLanguage"/> via <see cref="SubjectLanguageResolver.Resolve"/>.
///   3. Queries <c>Subject WHERE GradeId = g AND SubjectCode = code AND Language = resolved</c>.
///   4. If the resolved tree is absent (seed gap), falls back to the other language tree and logs a warning.
///
/// Empty subject list → 200 + empty collection (not 404).
/// Invalid grade number (outside 1–6) → 400. Unknown grade number → 404.
/// </summary>
public class GetSubjectsForGradeQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSubjectsForGradeQuery, BaseResponse<List<StudentSubjectDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    // All four stable subject codes — drives the per-code resolution loop.
    private static readonly SubjectCode[] AllSubjectCodes =
    {
        SubjectCode.MATH,
        SubjectCode.SCIENCE,
        SubjectCode.ARABIC,
        SubjectCode.ENGLISH,
    };

    public GetSubjectsForGradeQueryHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
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

            // P8-03-BE-1: resolve the student's learning language from the JWT claim.
            var learnerLang = LearningLanguageClaimAccessor.GetLearningLanguage(_currentUser, _logger);

            // Load all ACTIVE + PUBLISHED subjects for the grade once, then resolve per-code in memory.
            // P7-01: IsActive == true filter — inactive subjects hidden from student reads.
            // P7-05: LifecycleState == Published filter — Draft/Archived subjects not served to students.
            var allGradeSubjects = await _repository.Learning
                .GetByCondition<Subject>(s => s.GradeId == grade.Id && s.IsActive && s.LifecycleState == LifecycleState.Published, false)
                .ToListAsync(cancellationToken);

            var dtos = new List<StudentSubjectDto>(AllSubjectCodes.Length);

            foreach (var code in AllSubjectCodes)
            {
                // P8-03-BE-2: resolve effective language for this subject code.
                var resolved = SubjectLanguageResolver.Resolve(code, learnerLang);

                // P8-03-BE-3/BE-6: pick the resolved-language tree; fall back if absent.
                var subject = allGradeSubjects.FirstOrDefault(s => s.SubjectCode == code && s.Language == resolved);

                if (subject is null)
                {
                    // Missing-tree fallback (AC-10): serve opposite language tree and log a warning.
                    var fallbackLang = resolved == ContentLanguage.Ar ? ContentLanguage.En : ContentLanguage.Ar;
                    subject = allGradeSubjects.FirstOrDefault(s => s.SubjectCode == code && s.Language == fallbackLang);

                    if (subject is not null)
                    {
                        _logger.LogWarn(
                            $"Missing subject tree for SubjectCode={code} Language={resolved} GradeId={grade.Id}." +
                            $" Falling back to {fallbackLang}.");
                    }
                }

                if (subject is null)
                    continue; // no tree at all for this code — skip silently (seed not run)

                dtos.Add(new StudentSubjectDto
                {
                    Id          = subject.Id,
                    Name        = subject.Name,
                    GradeNumber = grade.Number,
                    SubjectCode = subject.SubjectCode,
                });
            }

            if (!dtos.Any())
                return EmptyCollection(new List<StudentSubjectDto>());

            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSubjectsForGradeQuery");
            return ServerError<List<StudentSubjectDto>>();  // P8-SEC-3: do not expose ex.Message to the client.
        }
    }
}
