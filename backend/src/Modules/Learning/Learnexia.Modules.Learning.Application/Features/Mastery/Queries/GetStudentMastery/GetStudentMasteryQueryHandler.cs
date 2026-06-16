using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Mastery.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Mastery.Queries.GetStudentMastery;

/// <summary>
/// Handles <see cref="GetStudentMasteryQuery"/>.
///
/// Returns all <c>StudentSkillMastery</c> rows for the JWT-authenticated student.
/// StudentId is always resolved from <c>ICurrentUserService.UserId</c> — never from the request body
/// or route — ensuring IDOR safety (a student reads only their own mastery data).
///
/// Empty list is a valid response (200) — student has not yet completed any skill attempts.
///
/// Option C: all EF access delegated to IMasteryQueryService via ILearningServiceManager.
/// This handler injects only ILearningServiceManager — no ILearningRepositoryManager, no EF types.
/// </summary>
public class GetStudentMasteryQueryHandler
    : BaseResponseHandler, IQueryHandler<GetStudentMasteryQuery, BaseResponse<List<MasteryDto>>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetStudentMasteryQueryHandler(
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

    public async Task<BaseResponse<List<MasteryDto>>> Handle(
        GetStudentMasteryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — IDOR guard: resolve StudentId from JWT, never from the client.
            var studentId = _currentUser.UserId;
            if (studentId is null)
                return Unauthorized<List<MasteryDto>>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 2 — Fetch all mastery rows for this student.
            var rows = await _service.MasteryQueryService
                .GetAllMasteryForStudentAsync(studentId.Value, cancellationToken);

            // Step 3 — Map to response DTOs.
            var dtos = rows.Select(m => new MasteryDto
            {
                SkillId           = m.SkillId,
                MasteryPercentage = m.MasteryPercentage,
                Status            = m.Status,
                AttemptsCount     = m.AttemptsCount,
                LastPracticedAt   = m.LastPracticedAt == default ? null : m.LastPracticedAt,
            }).ToList();

            var result = Success(dtos);
            result.Message = _localizer[SharedResourcesKey.MasteryRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in GetStudentMasteryQuery");
            return ServerError<List<MasteryDto>>();
        }
    }
}
