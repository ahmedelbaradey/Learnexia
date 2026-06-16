using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Mastery.Dtos;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Mastery.Queries.GetSkillMastery;

/// <summary>
/// Handles <see cref="GetSkillMasteryQuery"/>.
///
/// Returns the <c>StudentSkillMastery</c> row for the JWT-authenticated student and the given skill.
/// StudentId is always resolved from <c>ICurrentUserService.UserId</c> — IDOR guard.
///
/// Zero-data case: if no mastery row exists (student has not yet attempted the skill, or unknown skillId),
/// returns a <see cref="MasteryDto"/> with <c>Status = NotStarted</c> and <c>MasteryPercentage = 0</c>.
/// Never returns 404 or 500 for the zero-data / unknown-skill case.
///
/// Option C: all EF access delegated to IMasteryQueryService via ILearningServiceManager.
/// This handler injects only ILearningServiceManager — no ILearningRepositoryManager, no EF types.
/// </summary>
public class GetSkillMasteryQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSkillMasteryQuery, BaseResponse<MasteryDto>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSkillMasteryQueryHandler(
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

    public async Task<BaseResponse<MasteryDto>> Handle(
        GetSkillMasteryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — Inline validation (queries are not auto-validated by ValidationBehavior).
            if (request.SkillId <= 0)
                return BadRequest<MasteryDto>(_localizer[SharedResourcesKey.SkillIdMustBePositive]);

            // Step 2 — IDOR guard: resolve StudentId from JWT, never from the client.
            var studentId = _currentUser.UserId;
            if (studentId is null)
                return Unauthorized<MasteryDto>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 3 — Fetch the mastery row (may be null if the student hasn't touched this skill).
            var row = await _service.MasteryQueryService
                .GetSkillMasteryForStudentAsync(studentId.Value, request.SkillId, cancellationToken);

            // Step 4 — Zero-data default: return NotStarted instead of 404/500.
            MasteryDto dto;
            string messageKey;
            if (row is null)
            {
                dto = new MasteryDto
                {
                    SkillId           = request.SkillId,
                    MasteryPercentage = 0,
                    Status            = MasteryStatus.NotStarted,
                    AttemptsCount     = 0,
                    LastPracticedAt   = null,
                };
                messageKey = SharedResourcesKey.SkillMasteryNotFound;
            }
            else
            {
                dto = new MasteryDto
                {
                    SkillId           = row.SkillId,
                    MasteryPercentage = row.MasteryPercentage,
                    Status            = row.Status,
                    AttemptsCount     = row.AttemptsCount,
                    LastPracticedAt   = row.LastPracticedAt == default ? null : row.LastPracticedAt,
                };
                messageKey = SharedResourcesKey.SkillMasteryRetrievedSuccessfully;
            }

            var result = Success(dto);
            result.Message = _localizer[messageKey];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in GetSkillMasteryQuery");
            return ServerError<MasteryDto>();
        }
    }
}
