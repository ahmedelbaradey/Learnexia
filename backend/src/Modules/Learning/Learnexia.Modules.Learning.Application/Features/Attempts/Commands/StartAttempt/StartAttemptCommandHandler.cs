using System.Text.Json;
using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Application.Helpers;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
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
/// (Lesson → Unit → Subject walk). If it does not match, 403 Forbidden is returned
/// (<see cref="SharedResourcesKey.LessonLanguageMismatch"/>).
///
/// The new-attempt path delegates to <c>IAttemptService.StartNewAsync</c> which commits immediately
/// via <c>LearningDbContext.SaveChangesAsync(studentId)</c> so the DB-generated <c>Id</c> is
/// populated before the response is built. Mirrors the <c>LinkParentStudentService</c> precedent.
///
/// P3-11-BE-4: After loading the Published+Active candidate questions, calls
/// <c>IAdaptivityService.GetTargetDifficulty</c> to get the student's target difficulty, then
/// <c>QuizSelectionEngine.Select</c> to return a difficulty-weighted subset. Falls back to the full
/// candidate pool if the selected set is empty (graceful degradation). On new-attempt start,
/// persists <c>Attempt.ServedDifficultyMix</c> (jsonb) and <c>Attempt.TargetDifficulty</c> (int)
/// before the UnitOfWorkBehavior commits. On resume, re-runs Select with the same deterministic
/// inputs (sort-by-Id) so the same question set is reproduced without re-persisting the mix.
///
/// Mirrors AddSkillCommandHandler shape: ILearningRepositoryManager + ILoggerManager +
/// IStringLocalizer + try/catch → ServerError.
/// The UnitOfWorkBehavior's subsequent SaveChanges commits the newly staged mix fields.
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
    private readonly IAdaptivityService _adaptivityService;
    private readonly QuizSelectionOptions _selectionOptions;

    public StartAttemptCommandHandler(
        ILearningRepositoryManager repository,
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IAdaptivityService adaptivityService,
        IOptions<QuizSelectionOptions> selectionOptions)
    {
        _repository = repository;
        _service = service;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
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
            // Security #1: Without the lifecycle/active guard a student could start an attempt on a
            // Draft or Archived lesson even though its questions are filtered later.
            // Mirror GetLessonQueryHandler's guard exactly.
            var lesson = await _repository.Learning
                .GetByCondition<Lesson>(
                    l => l.Id == request.LessonId
                      && l.IsActive
                      && l.LifecycleState == LifecycleState.Published,
                    false)
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
                // P7-05: Filter LifecycleState == Published — Draft/Archived questions excluded from attempts.
                // Soft-deleted questions are already excluded by the global query filter.
                var candidates = _repository.Learning
                    .GetByCondition<QuizQuestion>(q => q.LessonId == request.LessonId && q.IsActive && q.LifecycleState == LifecycleState.Published, false)
                    .ToList();

                // P3-11: Re-run selection with the same deterministic inputs (sort-by-Id) so the
                // question set reproduced on resume is identical to what was served at first start (AC4).
                // Do NOT re-persist the mix — it was recorded at first start.
                var resumeDecision = await _adaptivityService.GetTargetDifficulty(
                    studentId.Value, lesson.SkillId, cancellationToken);

                var resumeSelected = QuizSelectionEngine.Select(candidates, resumeDecision.Difficulty, _selectionOptions);

                // Graceful degradation: if selection returns empty (empty pool), fall back to all candidates.
                IReadOnlyList<QuizQuestion> resumeQuestions = resumeSelected.Count > 0
                    ? resumeSelected
                    : candidates;

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
            // P7-04: Filter IsActive == true so deactivated questions do not appear in student quizzes.
            // P7-05: Filter LifecycleState == Published — Draft/Archived questions excluded from attempts.
            // Soft-deleted questions are already excluded by the global query filter.
            var newCandidates = _repository.Learning
                .GetByCondition<QuizQuestion>(q => q.LessonId == request.LessonId && q.IsActive && q.LifecycleState == LifecycleState.Published, false)
                .ToList();

            // P3-11: Get the student's target difficulty from the adaptivity model (AC1).
            // IAdaptivityService returns a default (Medium, IsDefault=true) for null SkillId or cold-start (AC3).
            var decision = await _adaptivityService.GetTargetDifficulty(
                studentId.Value, lesson.SkillId, cancellationToken);

            // P3-11: Select a difficulty-weighted subset from the candidate pool (AC2).
            var selected = QuizSelectionEngine.Select(newCandidates, decision.Difficulty, _selectionOptions);

            // P3-11: Graceful degradation — if selection returns empty (content gap), fall back to all
            // candidates so the quiz never returns empty when questions exist.
            // Log a content-gap warning server-side only (not in the response) per Q5.
            IReadOnlyList<QuizQuestion> servedQuestions;
            if (selected.Count == 0 && newCandidates.Count > 0)
            {
                _logger.LogWarn(
                    $"P3-11 content gap: no questions at target difficulty {decision.Difficulty} " +
                    $"for lesson {request.LessonId}. Falling back to full candidate pool.");
                servedQuestions = newCandidates;
            }
            else
            {
                // Also log a warning when the target bucket was empty (QuizSelectionEngine returned the
                // full sorted pool as its own graceful-degradation, so selected.Count == newCandidates.Count
                // and the target bucket was thin). We detect the content-gap when there are no questions
                // at the decided difficulty level in the served set.
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
            // The UnitOfWorkBehavior will commit this update alongside any other staged changes.
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
            // [F3] Log the full exception server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in StartAttemptCommand");
            return ServerError<StartAttemptResponse>();
        }
    }
}
