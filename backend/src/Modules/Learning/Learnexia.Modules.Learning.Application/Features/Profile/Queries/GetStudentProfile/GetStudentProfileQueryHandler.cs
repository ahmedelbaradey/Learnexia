using Learnexia.Modules.Learning.Application.Features.Profile.Dtos;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Profile.Queries.GetStudentProfile;

/// <summary>
/// Handles <see cref="GetStudentProfileQuery"/>.
///
/// Returns the behavioral learning profile for the JWT-authenticated student.
///
/// IDOR safety (AC5): <c>studentId</c> is ALWAYS resolved from <c>ICurrentUserService.UserId</c>.
/// It is NEVER taken from a route parameter, query string, or request body.
/// This is structurally enforced by the fact that <see cref="GetStudentProfileQuery"/> carries
/// no identity fields — the handler is the sole identity resolver.
///
/// DATA MINIMIZATION (child privacy): the response maps only to <see cref="StudentLearningProfileDto"/>
/// which contains only the four AC1 derived attributes + confidence indicators.
/// No raw answer data, no individual timestamps, no raw session logs are returned.
///
/// Cold-start (AC4): if no profile exists or DataPointCount == 0, the handler still returns
/// HTTP 200 with a valid DTO containing neutral defaults. Never 404.
/// </summary>
public class GetStudentProfileQueryHandler
    : BaseResponseHandler, IQueryHandler<GetStudentProfileQuery, BaseResponse<StudentLearningProfileDto>>
{
    private readonly IStudentProfileService _profileService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetStudentProfileQueryHandler(
        IStudentProfileService profileService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _profileService = profileService;
        _currentUser    = currentUser;
        _logger         = logger;
        _localizer      = localizer;
    }

    public async Task<BaseResponse<StudentLearningProfileDto>> Handle(
        GetStudentProfileQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── IDOR guard: resolve studentId from JWT ONLY — never from request. ─────────────────
            var studentId = _currentUser.UserId;
            if (studentId is null)
                return Unauthorized<StudentLearningProfileDto>(_localizer[SharedResourcesKey.Unauthorized]);

            // ── Fetch the derived profile (or cold-start defaults). ───────────────────────────────
            var derived = await _profileService.GetProfile(studentId.Value, cancellationToken);

            // ── Map DerivedProfile → StudentLearningProfileDto (data-minimization). ──────────────
            // Only the four AC1 attributes + DataPointCount are exposed.
            // No raw answer data, no timestamps of individual answers, no raw session logs.
            var dto = new StudentLearningProfileDto
            {
                QuestionTypeAffinity      = derived.QuestionTypeAffinity,
                RecurringErrorSkillIds    = derived.RecurringErrorSkillIds,
                AttentionSpanMinutes      = derived.AttentionSpanMinutes,
                PreferredExplanationStyle = derived.PreferredExplanationStyle,
                DataPointCount            = derived.DataPointCount,
            };

            var result = Success(dto);
            result.Message = _localizer[SharedResourcesKey.StudentProfileRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client (no PII leakage).
            _logger.LogError(ex, "Error in GetStudentProfileQuery");
            return ServerError<StudentLearningProfileDto>();
        }
    }
}
