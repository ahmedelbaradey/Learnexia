using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
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

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.StartAttempt;

/// <summary>
/// Handles StartAttemptCommand.
///
/// Creates an Attempt (Status=InProgress, StartedAt=UtcNow, StudentId from JWT — never from client).
/// Then fetches the lesson's questions and returns them WITHOUT CorrectAnswer.
/// A lesson with no questions yet returns 200 with an empty Questions list.
///
/// P8-03-BE-4: Before creating or resuming an attempt the handler verifies that the lesson's
/// owning Subject language matches the student's resolved effective language for that SubjectCode
/// (Lesson → Unit → Subject walk). If it does not match, 403 Forbidden is returned
/// (<see cref="SharedResourcesKey.LessonLanguageMismatch"/>).
///
/// The new-attempt path delegates to <c>IAttemptService.StartNewAsync</c> which commits immediately
/// via <c>LearningDbContext.SaveChangesAsync(studentId)</c> so the DB-generated <c>Id</c> is
/// populated before the response is built. Mirrors the <c>LinkParentStudentService</c> precedent.
///
/// Mirrors AddSkillCommandHandler shape: ILearningRepositoryManager + ILoggerManager +
/// IStringLocalizer + try/catch → ServerError.
/// The UnitOfWorkBehavior's subsequent SaveChanges is a harmless no-op for the committed attempt.
/// </summary>
public class StartAttemptCommandHandler : BaseResponseHandler,
    ICommandHandler<StartAttemptCommand, BaseResponse<StartAttemptResponse>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public StartAttemptCommandHandler(
        ILearningRepositoryManager repository,
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _service = service;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<StartAttemptResponse>> Handle(
        StartAttemptCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // StudentId is resolved from the authenticated JWT — never supplied by the client.
            var studentId = _currentUser.UserId;
            if (studentId is null)
                return Unauthorized<StartAttemptResponse>(_localizer[SharedResourcesKey.Unauthorized]);

            // [F7] Verify the lesson exists before any write.
            var lesson = await _repository.Learning
                .GetByCondition<Lesson>(l => l.Id == request.LessonId, false)
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null)
                return NotFound<StartAttemptResponse>(_localizer[SharedResourcesKey.LessonNotFound]);

            // P8-03-BE-4: Language guard — walk Lesson → Unit → Subject to verify the language.
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
                    // The student is trying to start an attempt in the wrong-language tree.
                    return Forbidden<StartAttemptResponse>(_localizer[SharedResourcesKey.LessonLanguageMismatch]);
                }
            }

            // [F4] Resume an existing in-progress attempt instead of creating a duplicate.
            var existingAttempt = _repository.Learning
                .GetByCondition<Attempt>(
                    a => a.StudentId == studentId.Value
                      && a.LessonId == request.LessonId
                      && a.Status == AttemptStatus.InProgress,
                    trackChanges: false)
                .FirstOrDefault();

            if (existingAttempt is not null)
            {
                // P7-04: Filter IsActive == true so deactivated questions do not appear in student quizzes.
                // Soft-deleted questions are already excluded by the global query filter.
                var questions = _repository.Learning
                    .GetByCondition<QuizQuestion>(q => q.LessonId == request.LessonId && q.IsActive, false)
                    .ToList();

                var questionDtos = _mapper.Map<List<QuizQuestionDto>>(questions);

                var resumeResponse = new StartAttemptResponse
                {
                    AttemptId = existingAttempt.Id,
                    Questions = questionDtos
                };

                var resumeResult = Success(resumeResponse);
                resumeResult.Message = _localizer[SharedResourcesKey.AttemptResumedSuccessfully];
                return resumeResult;
            }

            // No in-progress attempt exists — create and immediately persist so the DB-generated Id
            // is available. IAttemptService.StartNewAsync commits inside the Infrastructure layer,
            // mirroring the LinkParentStudentService precedent for "persist-and-read-Id" cases.
            var attempt = await _service.AttemptService.StartNewAsync(
                studentId.Value, request.LessonId, cancellationToken);

            // Load questions for the lesson (no correct answer included in the DTO).
            // P7-04: Filter IsActive == true so deactivated questions do not appear in student quizzes.
            // Soft-deleted questions are already excluded by the global query filter.
            var newQuestions = _repository.Learning
                .GetByCondition<QuizQuestion>(q => q.LessonId == request.LessonId && q.IsActive, false)
                .ToList();

            var newQuestionDtos = _mapper.Map<List<QuizQuestionDto>>(newQuestions);

            var response = new StartAttemptResponse
            {
                AttemptId = attempt.Id,
                Questions = newQuestionDtos
            };

            var result = Success(response);
            result.Message = _localizer[SharedResourcesKey.AttemptStartedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // [F3] Log the full exception server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in StartAttemptCommand");
            return ServerError<StartAttemptResponse>();
        }
    }
}
