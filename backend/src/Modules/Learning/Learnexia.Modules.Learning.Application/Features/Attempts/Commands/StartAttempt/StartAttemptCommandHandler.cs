using System.Text.Json;
using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Application.Helpers;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
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
/// (Lesson → Unit → Subject walk). If it does not match, 403 Forbidden is returned.
///
/// The new-attempt path delegates to <c>IAttemptService.StartNewAsync</c> which commits immediately
/// via <c>LearningDbContext.SaveChangesAsync(studentId)</c> so the DB-generated <c>Id</c> is
/// populated before the response is built. Mirrors the <c>LinkParentStudentService</c> precedent.
///
/// P3-11-BE-4: After loading the Published+Active candidate questions, calls
/// <c>IAdaptivityService.GetTargetDifficulty</c> to get the student's target difficulty, then
/// <c>QuizSelectionEngine.Select</c> to return a difficulty-weighted subset.
///
/// Option C (no EF in Application): all DB access delegated to IStartAttemptService
/// (for read-only lesson/subject/question loads) and IAttemptService.StartNewAsync (for the
/// persist-and-read-Id escape hatch — left intact per Stage 6 constraint).
/// </summary>
public class StartAttemptCommandHandler : BaseResponseHandler,
    ICommandHandler<StartAttemptCommand, BaseResponse<StartAttemptResponse>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IAdaptivityService _adaptivityService;
    private readonly QuizSelectionOptions _selectionOptions;

    public StartAttemptCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IAdaptivityService adaptivityService,
        IOptions<QuizSelectionOptions> selectionOptions)
    {
        _service          = service;
        _currentUser      = currentUser;
        _mapper           = mapper;
        _logger           = logger;
        _localizer        = localizer;
        _adaptivityService = adaptivityService;
        _selectionOptions = selectionOptions.Value;
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

            // [F7] Verify the lesson exists AND is Published+Active before any write.
            var lesson = await _service.StartAttemptService
                .GetPublishedActiveLessonAsync(request.LessonId, cancellationToken);

            if (lesson is null)
                return NotFound<StartAttemptResponse>(_localizer[SharedResourcesKey.LessonNotFound]);

            // P8-03-BE-4: Language guard — walk Lesson → Unit → Subject to verify the language.
            var owningSubject = await _service.StartAttemptService
                .GetOwningSubjectForLessonAsync(request.LessonId, cancellationToken);

            if (owningSubject is not null)
            {
                var learnerLang = LearningLanguageClaimAccessor.GetLearningLanguage(_currentUser, _logger);
                var resolved    = SubjectLanguageResolver.Resolve(owningSubject.SubjectCode, learnerLang);

                if (owningSubject.Language != resolved)
                {
                    return Forbidden<StartAttemptResponse>(_localizer[SharedResourcesKey.LessonLanguageMismatch]);
                }
            }

            // [F4] Resume an existing in-progress attempt instead of creating a duplicate.
            var existingAttempt = await _service.StartAttemptService
                .GetInProgressAttemptAsync(studentId.Value, request.LessonId, cancellationToken);

            if (existingAttempt is not null)
            {
                var candidates = await _service.StartAttemptService
                    .GetPublishedActiveQuestionsAsync(request.LessonId, cancellationToken);

                // P3-11: Re-run selection with the same deterministic inputs (sort-by-Id).
                var resumeDecision = await _adaptivityService.GetTargetDifficulty(
                    studentId.Value, lesson.SkillId, cancellationToken);

                var resumeSelected = QuizSelectionEngine.Select(candidates, resumeDecision.Difficulty, _selectionOptions);

                IReadOnlyList<Learnexia.Modules.Learning.Domain.Entities.QuizQuestion> resumeQuestions =
                    resumeSelected.Count > 0 ? resumeSelected : candidates;

                var questionDtos = _mapper.Map<List<QuizQuestionDto>>(resumeQuestions);

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
            var newCandidates = await _service.StartAttemptService
                .GetPublishedActiveQuestionsAsync(request.LessonId, cancellationToken);

            // P3-11: Get the student's target difficulty from the adaptivity model (AC1).
            var decision = await _adaptivityService.GetTargetDifficulty(
                studentId.Value, lesson.SkillId, cancellationToken);

            // P3-11: Select a difficulty-weighted subset from the candidate pool (AC2).
            var selected = QuizSelectionEngine.Select(newCandidates, decision.Difficulty, _selectionOptions);

            // P3-11: Graceful degradation.
            IReadOnlyList<Learnexia.Modules.Learning.Domain.Entities.QuizQuestion> servedQuestions;
            if (selected.Count == 0 && newCandidates.Count > 0)
            {
                _logger.LogWarn(
                    $"P3-11 content gap: no questions at target difficulty {decision.Difficulty} " +
                    $"for lesson {request.LessonId}. Falling back to full candidate pool.");
                servedQuestions = newCandidates;
            }
            else
            {
                var hasTargetDifficulty = selected.Any(q => q.Difficulty == decision.Difficulty);
                if (selected.Count > 0 && !hasTargetDifficulty && newCandidates.Count > 0)
                {
                    _logger.LogWarn(
                        $"P3-11 content gap: no questions at target difficulty {decision.Difficulty} " +
                        $"for lesson {request.LessonId}. Serving adjacent difficulties.");
                }
                servedQuestions = selected.Count > 0 ? selected : newCandidates;
            }

            // P3-11: Persist the difficulty mix on the Attempt (AC4).
            var easyCount   = servedQuestions.Count(q => q.Difficulty == DifficultyLevel.Easy);
            var mediumCount = servedQuestions.Count(q => q.Difficulty == DifficultyLevel.Medium);
            var hardCount   = servedQuestions.Count(q => q.Difficulty == DifficultyLevel.Hard);

            var mixPayload = new
            {
                Easy      = easyCount,
                Medium    = mediumCount,
                Hard      = hardCount,
                Target    = decision.Difficulty.ToString(),
                WasDefault = decision.IsDefault
            };

            attempt.ServedDifficultyMix = JsonSerializer.Serialize(mixPayload);
            attempt.TargetDifficulty    = (int)decision.Difficulty;

            var newQuestionDtos = _mapper.Map<List<QuizQuestionDto>>(servedQuestions);

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
            _logger.LogError(ex, "Error in StartAttemptCommand");
            return ServerError<StartAttemptResponse>();
        }
    }
}
