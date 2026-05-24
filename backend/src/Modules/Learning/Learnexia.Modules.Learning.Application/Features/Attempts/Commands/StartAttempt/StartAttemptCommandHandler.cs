using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
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
            var lessonExists = await _repository.Learning
                .AnyAsync<Lesson>(l => l.Id == request.LessonId);
            if (!lessonExists)
                return NotFound<StartAttemptResponse>(_localizer[SharedResourcesKey.LessonNotFound]);

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
                var questions = _repository.Learning
                    .GetByCondition<QuizQuestion>(q => q.LessonId == request.LessonId, false)
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
            var newQuestions = _repository.Learning
                .GetByCondition<QuizQuestion>(q => q.LessonId == request.LessonId, false)
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
