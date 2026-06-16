using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetSkillStats;

/// <summary>
/// Handles GetSkillStatsQuery.
///
/// Aggregates StudentAnswer rows for a given (SkillId, StudentId) pair.
/// Only answers for questions that have QuizQuestion.SkillId == skillId are included;
/// answers for questions with a null SkillId are silently excluded.
///
/// Authorization scope: a student may only request their own stats (IDOR guard).
/// Zero-data case returns a zeroed SkillStatsDto — never 404 or 500.
///
/// Option C (no EF in Application): all DB access delegated to IAttemptQueryService.
/// </summary>
public class GetSkillStatsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSkillStatsQuery, BaseResponse<SkillStatsDto>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSkillStatsQueryHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service     = service;
        _currentUser = currentUser;
        _logger      = logger;
        _localizer   = localizer;
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

            // Step 3 — Delegate to service (all EF inside Infrastructure).
            var dto = await _service.AttemptQueryService
                .GetSkillStatsAsync(request.StudentId, request.SkillId, cancellationToken);

            // Step 4 — Return.
            var result = Success(dto);
            result.Message = _localizer[SharedResourcesKey.SkillStatsRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetSkillStatsQuery");
            return ServerError<SkillStatsDto>();
        }
    }
}
