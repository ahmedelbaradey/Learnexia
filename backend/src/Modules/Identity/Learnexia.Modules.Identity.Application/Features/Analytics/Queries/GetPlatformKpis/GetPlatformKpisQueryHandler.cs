using Learnexia.Modules.Identity.Application.Features.Analytics.Dtos;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Analytics.Queries.GetPlatformKpis;

/// <summary>
/// Façade handler for <see cref="GetPlatformKpisQuery"/>.
/// Fans out to four platform-aggregate <c>Shared.Contracts</c> seams and assembles
/// the <see cref="PlatformKpiSummaryDto"/>.
///
/// <para>Module isolation: the handler injects ONLY <c>Shared.Contracts</c> seam interfaces —
/// never any concrete Learning/Gamification/Billing/Ai types. Cross-module coupling is
/// exclusively through the interfaces declared in <c>Shared.Contracts</c>.</para>
///
/// <para>Persistence architecture: this is a query handler (no write). It coordinates multiple
/// service calls (the four seam interfaces) — permissible per CONVENTIONS §7 ("Handlers MAY
/// orchestrate — not required to be one-line shells"). No repo, no DbContext, no EF here.</para>
///
/// <para>Caching: v1 does NOT add Redis caching — the fan-out is already over <c>AsNoTracking</c>
/// group-bys (lightweight) and the handler is admin-only (low traffic). Redis caching can be
/// added behind the same handler as a follow-up without API contract change.</para>
///
/// <para>Input defaults: a null <c>FromUtc</c> defaults to 30 days ago; a null <c>ToUtc</c> defaults
/// to now (UTC). Max window = 365 days (reasonable guard; prevents accidentally querying all time).</para>
/// </summary>
public sealed class GetPlatformKpisQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetPlatformKpisQuery, BaseResponse<PlatformKpiSummaryDto>>
{
    private readonly IPlatformLearningStatsQuery  _learningStats;
    private readonly IPlatformEngagementQuery     _engagement;
    private readonly IPlatformSubscriptionStatsQuery _subscriptionStats;
    private readonly IPlatformAiSafetyStatsQuery  _aiSafetyStats;
    private readonly ILoggerManager               _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    private static readonly TimeSpan DefaultWindowDays = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaxWindow         = TimeSpan.FromDays(365);

    public GetPlatformKpisQueryHandler(
        IPlatformLearningStatsQuery      learningStats,
        IPlatformEngagementQuery         engagement,
        IPlatformSubscriptionStatsQuery  subscriptionStats,
        IPlatformAiSafetyStatsQuery      aiSafetyStats,
        ILoggerManager                   logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _learningStats     = learningStats;
        _engagement        = engagement;
        _subscriptionStats = subscriptionStats;
        _aiSafetyStats     = aiSafetyStats;
        _logger            = logger;
        _localizer         = localizer;
    }

    public async Task<BaseResponse<PlatformKpiSummaryDto>> Handle(
        GetPlatformKpisQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── Input defaults and validation (query — not auto-validated) ────────────────────────
            var now    = DateTime.UtcNow;
            var fromUtc = request.FromUtc ?? now.Subtract(DefaultWindowDays);
            var toUtc   = request.ToUtc   ?? now;

            if (fromUtc >= toUtc)
                return BadRequest<PlatformKpiSummaryDto>(
                    _localizer[SharedResourcesKey.PlatformKpisFromMustBeBeforeTo]);

            if ((toUtc - fromUtc) > MaxWindow)
                return BadRequest<PlatformKpiSummaryDto>(
                    _localizer[SharedResourcesKey.PlatformKpisWindowTooLarge]);

            // ── Fan-out to four module seams ──────────────────────────────────────────────────────
            // All four queries are independent — run in parallel for minimal latency.
            var learningTask      = _learningStats.GetPlatformAsync(fromUtc, toUtc, cancellationToken);
            var engagementTask    = _engagement.GetPlatformAsync(fromUtc, toUtc, cancellationToken);
            var subscriptionTask  = _subscriptionStats.GetPlatformAsync(cancellationToken);
            var aiSafetyTask      = _aiSafetyStats.GetPlatformAsync(fromUtc, toUtc, cancellationToken);

            await Task.WhenAll(learningTask, engagementTask, subscriptionTask, aiSafetyTask);

            var learning     = await learningTask;
            var engagement   = await engagementTask;
            var subscription = await subscriptionTask;
            var aiSafety     = await aiSafetyTask;

            // ── Assemble KPI DTO ──────────────────────────────────────────────────────────────────
            var dto = new PlatformKpiSummaryDto
            {
                FromUtc                   = fromUtc,
                ToUtc                     = toUtc,

                // Learning
                LessonsCompleted          = learning.LessonsCompleted,
                TotalAttempts             = learning.TotalAttempts,
                DistinctActiveStudents    = learning.DistinctActiveStudents,
                BySubject                 = learning.BySubject,
                ByGrade                   = learning.ByGrade,

                // Engagement
                MissionsCompleted         = engagement.MissionsCompleted,
                XpEarnedInWindow          = engagement.XpEarnedInWindow,

                // Subscriptions
                TotalActiveSubscriptions  = subscription.TotalActiveSubscriptions,
                SubscriptionsByTier       = subscription.ByTier,
                RevenueNaReason           = subscription.RevenueNaReason,

                // AI Safety
                TotalAiSafetyEvents       = aiSafety.TotalSafetyEvents,
                AiBlockedCount            = aiSafety.BlockedCount,
                AiFlaggedCount            = aiSafety.FlaggedCount,
                AiRequestVolumeNaReason   = aiSafety.AiRequestVolumeNaReason,
                // QuizzesCompleted, Retention, SessionDuration N/A reasons use DTO defaults
            };

            _logger.LogInfo(
                $"GetPlatformKpisQuery: assembled KPIs for [{fromUtc:u}, {toUtc:u}) — " +
                $"lessons={dto.LessonsCompleted}, activeStudents={dto.DistinctActiveStudents}, " +
                $"missions={dto.MissionsCompleted}, subs={dto.TotalActiveSubscriptions}, " +
                $"aiEvents={dto.TotalAiSafetyEvents}");

            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.PlatformKpisRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPlatformKpisQueryHandler");
            return ServerError<PlatformKpiSummaryDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
