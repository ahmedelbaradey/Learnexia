using Learnexia.Modules.Learning.Application.Features.Adaptivity.Dtos;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Adaptivity.Queries.GetAdaptivityDecision;

/// <summary>
/// Handles <see cref="GetAdaptivityDecisionQuery"/>.
///
/// Returns the adaptive difficulty decision for the JWT-authenticated student and the given skill.
/// StudentId is ALWAYS resolved from <c>ICurrentUserService.UserId</c> — IDOR guard.
///
/// This is the BE-5 inspection endpoint handler (AC4 — exposed downstream).
/// P3-11 calls <see cref="IAdaptivityService.GetTargetDifficulty"/> directly (same service method),
/// not this handler.
/// </summary>
public class GetAdaptivityDecisionQueryHandler
    : BaseResponseHandler, IQueryHandler<GetAdaptivityDecisionQuery, BaseResponse<AdaptivityDecisionDto>>
{
    private readonly IAdaptivityService _adaptivityService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetAdaptivityDecisionQueryHandler(
        IAdaptivityService adaptivityService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _adaptivityService = adaptivityService;
        _currentUser       = currentUser;
        _logger            = logger;
        _localizer         = localizer;
    }

    public async Task<BaseResponse<AdaptivityDecisionDto>> Handle(
        GetAdaptivityDecisionQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — Inline validation (queries are NOT auto-validated by ValidationBehavior).
            if (request.SkillId <= 0)
                return BadRequest<AdaptivityDecisionDto>(_localizer[SharedResourcesKey.SkillIdMustBePositive]);

            // Step 2 — IDOR guard: resolve StudentId from JWT, never from the client.
            var studentId = _currentUser.UserId;
            if (studentId is null)
                return Unauthorized<AdaptivityDecisionDto>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 3 — Delegate to the in-process seam.
            var decision = await _adaptivityService.GetTargetDifficulty(studentId.Value, request.SkillId, cancellationToken);

            // Step 4 — Map to DTO.
            var dto = new AdaptivityDecisionDto
            {
                Difficulty = decision.Difficulty,
                IsDefault  = decision.IsDefault,
                Score      = decision.Score,
            };

            var result = Success(dto);
            result.Message = _localizer[SharedResourcesKey.AdaptivityDecisionRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in GetAdaptivityDecisionQuery");
            return ServerError<AdaptivityDecisionDto>();
        }
    }
}
