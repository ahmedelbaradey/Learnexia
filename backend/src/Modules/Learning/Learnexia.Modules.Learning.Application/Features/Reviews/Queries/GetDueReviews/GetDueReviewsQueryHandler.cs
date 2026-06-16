using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Reviews.Queries.GetDueReviews;

/// <summary>
/// Handles <see cref="GetDueReviewsQuery"/> (P3-10 BE-7, AC2).
///
/// Returns all <c>StudentSkillMastery</c> rows that are currently due for spaced-repetition
/// review for the JWT-authenticated student. Uses <see cref="IReviewsService.GetDueMasteryRowsAsync"/>
/// which applies the IsDue rule (NeedsReview OR Mastered+stale) and filters to the student's rows.
///
/// IDOR guard: StudentId is resolved from <c>ICurrentUserService.UserId</c> only.
/// UTC discipline: <c>DateTime.UtcNow</c> passed to the service.
///
/// Option C: all EF access delegated to IReviewsService via ILearningServiceManager.
/// This handler injects only ILearningServiceManager — no ILearningRepositoryManager, no EF types.
/// </summary>
public class GetDueReviewsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetDueReviewsQuery, BaseResponse<IReadOnlyList<DueReviewDto>>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetDueReviewsQueryHandler(
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

    public async Task<BaseResponse<IReadOnlyList<DueReviewDto>>> Handle(
        GetDueReviewsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — IDOR guard: resolve StudentId from JWT, never from the client.
            var studentId = _currentUser.UserId;
            if (studentId is null)
                return Unauthorized<IReadOnlyList<DueReviewDto>>(
                    _localizer[SharedResourcesKey.Unauthorized]);

            // Step 2 — UTC discipline: always use UtcNow (never DateTime.Now).
            var utcNow = DateTime.UtcNow;

            // Step 3 — Fetch all due mastery rows globally, then filter to this student.
            // The service returns rows with Skill navigation populated (Include in GetDueMasteryRowsAsync).
            // We filter in-memory after the query to apply the StudentId IDOR guard cleanly.
            // For scale: if needed, add a student-scoped variant; deferred until needed.
            var allDueRows = await _service.ReviewsService.GetDueMasteryRowsAsync(utcNow, cancellationToken);
            var studentRows = allDueRows.Where(m => m.StudentId == studentId.Value).ToList();

            // Step 4 — Map to DueReviewDto (Skill.Name from navigation property).
            var dtos = studentRows.Select(m => new DueReviewDto
            {
                SkillId          = m.SkillId,
                SkillName        = m.Skill?.Name ?? string.Empty,
                NextReviewDueAt  = m.NextReviewDueAt,
                Status           = m.Status,
                RepetitionNumber = m.RepetitionNumber,
            }).ToList();

            var result = Success<IReadOnlyList<DueReviewDto>>(dtos);
            result.Message = _localizer[SharedResourcesKey.DueReviewsRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in GetDueReviewsQuery");
            return ServerError<IReadOnlyList<DueReviewDto>>();
        }
    }
}
