using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetSkillStats;

/// <summary>
/// Handles GetSkillStatsQuery.
///
/// Aggregates StudentAnswer rows for a given (SkillId, StudentId) pair.
/// Only answers for questions that have QuizQuestion.SkillId == skillId are included;
/// answers for questions with a null SkillId are silently excluded (correct behaviour — a
/// question with no skill cannot contribute to per-skill stats).
///
/// Authorization scope: a student may only request their own stats (IDOR guard).
/// Zero-data case returns a zeroed SkillStatsDto — never 404 or 500.
/// </summary>
public class GetSkillStatsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSkillStatsQuery, BaseResponse<SkillStatsDto>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSkillStatsQueryHandler(
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

    public async Task<BaseResponse<SkillStatsDto>> Handle(
        GetSkillStatsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — Inline validation: queries are NOT auto-validated by ValidationBehavior.
            if (request.SkillId <= 0)
                return BadRequest<SkillStatsDto>(_localizer[SharedResourcesKey.SkillIdMustBePositive]);

            if (request.StudentId <= 0)
                return BadRequest<SkillStatsDto>(_localizer[SharedResourcesKey.StudentIdMustBePositive]);

            // Step 2 — Authorization scope: student may only read their own stats (IDOR guard).
            var currentUserId = _currentUser.UserId;
            if (currentUserId is null || request.StudentId != currentUserId.Value)
                return Unauthorized<SkillStatsDto>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 3 — Query: fetch relevant answers with only the fields needed for aggregation.
            // EF Core resolves the navigation-property predicates via implicit JOINs.
            // QuizQuestion.SkillId is nullable — the equality excludes null (correct behaviour).
            var answers = await _repository.Learning
                .GetByCondition<StudentAnswer>(
                    sa => sa.Question.SkillId == request.SkillId
                          && sa.Attempt.StudentId == request.StudentId,
                    trackChanges: false)
                .Select(sa => new { sa.IsCorrect, sa.TimeSpentSeconds, sa.HintUsed })
                .ToListAsync(cancellationToken);

            // Step 4 — Aggregate in memory. Zero-data → return zeroed DTO (not 404, not 500).
            var total   = answers.Count;
            var correct = answers.Count(a => a.IsCorrect);

            var dto = new SkillStatsDto
            {
                SkillId             = request.SkillId,
                StudentId           = request.StudentId,
                TotalAnswers        = total,
                CorrectAnswers      = correct,
                AccuracyPercentage  = total == 0 ? 0.0 : Math.Round((double)correct / total * 100, 2),
                AvgTimeSpentSeconds = total == 0 ? 0.0 : Math.Round(answers.Average(a => a.TimeSpentSeconds), 2),
                HintUsageRate       = total == 0 ? 0.0 : Math.Round(answers.Count(a => a.HintUsed) / (double)total * 100, 2),
            };

            // Step 5 — Return.
            var result = Success(dto);
            result.Message = _localizer[SharedResourcesKey.SkillStatsRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in GetSkillStatsQuery");
            return ServerError<SkillStatsDto>();
        }
    }
}
