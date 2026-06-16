using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Modules.Learning.Application.Helpers;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.Get;

/// <summary>
/// Handles <see cref="GetLessonQuery"/>.
/// Loads <see cref="Learnexia.Modules.Learning.Domain.Entities.Lesson"/> + first <see cref="Learnexia.Modules.Learning.Domain.Entities.QuizQuestion"/>
/// and maps to <see cref="SingleLessonResponse"/> with <c>QuickCheck</c> filled manually.
///
/// P8-03-BE-4: After loading the lesson the handler walks up the ownership chain
/// (Lesson → Unit → Subject) to verify that the subject's ContentLanguage matches the student's
/// resolved effective language for that subject's SubjectCode.
///
/// SECURITY: <c>CorrectAnswer</c> is excluded via <c>QuizProfile</c>'s
/// <c>ForSourceMember(...DoNotValidate())</c> — never echoed to the client.
///
/// Option-C: all EF calls moved into ILessonService (Infrastructure). Handler is now thin.
/// Pure-domain helpers (SubjectLanguageResolver, LearningLanguageClaimAccessor) remain here.
/// </summary>
public class GetLessonQueryHandler
    : BaseResponseHandler, IQueryHandler<GetLessonQuery, BaseResponse<SingleLessonResponse>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetLessonQueryHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<SingleLessonResponse>> Handle(
        GetLessonQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load the active + published lesson (student-facing).
            // Inactive or non-published lessons return NotFound so no admin info is leaked.
            var lesson = await _service.LessonService.GetActiveLessonAsync(request.Id, cancellationToken);

            if (lesson is null)
                return NotFound<SingleLessonResponse>(_localizer[SharedResourcesKey.LessonNotFound]);

            // P8-03-BE-4: Language guard — walk Lesson → Unit → Subject to verify the language.
            // Only applied for authenticated requests (students). Unauthenticated callers skip the guard.
            var studentId = _currentUser.UserId;
            if (studentId is not null)
            {
                var owningSubject = await _service.LessonService.GetOwningSubjectByUnitAsync(lesson.UnitId, cancellationToken);

                if (owningSubject is not null)
                {
                    var learnerLang = LearningLanguageClaimAccessor.GetLearningLanguage(_currentUser, _logger);
                    var resolved = SubjectLanguageResolver.Resolve(owningSubject.SubjectCode, learnerLang);

                    if (owningSubject.Language != resolved)
                    {
                        return Forbidden<SingleLessonResponse>(_localizer[SharedResourcesKey.LessonLanguageMismatch]);
                    }
                }
            }

            // Load the first QuizQuestion for the lesson by Id ASC (Q2 rule — first-by-Id).
            var firstQuestion = await _service.LessonService.GetFirstQuizQuestionAsync(request.Id, cancellationToken);

            // Map Lesson → SingleLessonResponse (Explanation + Visual auto-map by name).
            var dto = _mapper.Map<SingleLessonResponse>(lesson);

            // Fill QuickCheck manually — CorrectAnswer excluded via QuizProfile.
            dto.QuickCheck = firstQuestion is not null
                ? _mapper.Map<QuizQuestionDto>(firstQuestion)
                : null;

            return Success(dto);
        }
        catch (Exception ex)
        {
            // Log full exception server-side; do NOT echo ex.Message to the client (Q12 fix).
            _logger.LogError(ex, "Error: in GetLessonQuery");
            return ServerError<SingleLessonResponse>();
        }
    }
}
