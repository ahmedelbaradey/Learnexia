using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
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

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.Get;

/// <summary>
/// Handles <see cref="GetLessonQuery"/>.
/// Hand-rolls the load of <see cref="Lesson"/> + first <see cref="QuizQuestion"/> (by Id ASC)
/// and maps to <see cref="SingleLessonResponse"/> with <c>QuickCheck</c> filled manually.
///
/// P8-03-BE-4: After loading the lesson the handler walks up the ownership chain
/// (Lesson → Unit → Subject) to verify that the subject's <see cref="Domain.Enums.ContentLanguage"/>
/// matches the student's resolved effective language for that subject's <see cref="Domain.Enums.SubjectCode"/>.
/// If it does not match, the handler returns 403 Forbidden (<see cref="SharedResourcesKey.LessonLanguageMismatch"/>)
/// so a student cannot read a lesson from the wrong-language tree via a direct lesson Id.
///
/// SECURITY: <c>CorrectAnswer</c> is excluded via <c>QuizProfile</c>'s
/// <c>ForSourceMember(...DoNotValidate())</c> — never echoed to the client.
/// ex.Message is NOT propagated to the response (Q12 fix).
/// </summary>
public class GetLessonQueryHandler
    : BaseResponseHandler, IQueryHandler<GetLessonQuery, BaseResponse<SingleLessonResponse>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetLessonQueryHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
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
            // Load the lesson by id. AsNoTracking — query handler; no writes.
            // P7-02: Student-facing reads must only see active (IsActive=true) lessons.
            // Inactive lessons return NotFound (same as deleted) so no admin info is leaked.
            // P7-05: LifecycleState == Published filter — Draft/Archived lessons not served to students.
            var lesson = await _repository.Learning
                .GetByCondition<Lesson>(l => l.Id == request.Id && l.IsActive && l.LifecycleState == LifecycleState.Published, false)
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null)
                return NotFound<SingleLessonResponse>(_localizer[SharedResourcesKey.LessonNotFound]);

            // P8-03-BE-4: Language guard — walk Lesson → Unit → Subject to verify the language.
            // Only applied for authenticated requests (students). Unauthenticated callers skip the guard
            // (defense-in-depth; controller requires [Authorize] in practice).
            var studentId = _currentUser.UserId;
            if (studentId is not null)
            {
                var owningSubject = await _repository.Learning
                    .GetByCondition<Unit>(u => u.Id == lesson.UnitId, false)
                    .Include(u => u.Subject)
                    .Select(u => u.Subject)
                    .FirstOrDefaultAsync(cancellationToken);

                if (owningSubject is not null)
                {
                    var learnerLang = LearningLanguageClaimAccessor.GetLearningLanguage(_currentUser, _logger);
                    var resolved = SubjectLanguageResolver.Resolve(owningSubject.SubjectCode, learnerLang);

                    if (owningSubject.Language != resolved)
                    {
                        // The student is trying to read a lesson from the wrong-language tree.
                        return Forbidden<SingleLessonResponse>(_localizer[SharedResourcesKey.LessonLanguageMismatch]);
                    }
                }
            }

            // Load the first QuizQuestion for the lesson by Id ASC (Q2 rule — first-by-Id).
            // Null when no questions exist; QuickCheck is null in that case (valid per AC1).
            var firstQuestion = await _repository.Learning
                .GetByCondition<QuizQuestion>(q => q.LessonId == request.Id, false)
                .OrderBy(q => q.Id)
                .FirstOrDefaultAsync(cancellationToken);

            // Map Lesson → SingleLessonResponse (Explanation + Visual auto-map by name).
            var dto = _mapper.Map<SingleLessonResponse>(lesson);

            // Fill QuickCheck manually — CorrectAnswer excluded via QuizProfile (QuizProfile.cs:21-26).
            dto.QuickCheck = firstQuestion is not null
                ? _mapper.Map<QuizQuestionDto>(firstQuestion)
                : null;

            return Success(dto);
        }
        catch (Exception ex)
        {
            // [F3] Log full exception server-side; do NOT echo ex.Message to the client (Q12 fix).
            _logger.LogError(ex, "Error: in GetLessonQuery");
            return ServerError<SingleLessonResponse>();
        }
    }
}
