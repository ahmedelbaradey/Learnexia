using Learnexia.Modules.Parent.Api.Bases;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildActivity;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildEnergy;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildProgress;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildReports;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildSubjectMastery;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildWeakAreas;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildWeeklyKpis;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetFamilySummary;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetWeeklyReport;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Parent.Api.Controllers;

// Parent analytics read-only endpoints (P5-08-BE-5..13).
// All routes share the api/Parent base. The acting parent is ALWAYS resolved from the JWT
// inside the handlers (ICurrentUserService) — never from the request body or query string.
// Per-child routes call IParentChildQuery.IsParentOfChildAsync BEFORE any seam fan-out (IDOR guard).
[Authorize(Roles = "Parent,Admin,SuperAdmin")]
[Route("api/Parent")]
[ApiController]
public class ParentAnalyticsController : AppControllerBase
{
    // E1 — Child progress card (level / XP / streak / mastery / weakest skill / active-today / energy)
    [HttpGet("Children/{id:int}/Progress")]
    [ProducesResponseType(typeof(BaseResponse<ChildProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildProgress(int id)
        => NewResult(await Mediator.Send(new GetChildProgressQuery(id)));

    // E2 — Family summary ("this week" strip: activeLearners / lessons / XP / bestStreak / badges)
    [HttpGet("Family/Summary")]
    [ProducesResponseType(typeof(BaseResponse<FamilySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFamilySummary()
        => NewResult(await Mediator.Send(new GetFamilySummaryQuery()));

    // E3 — Rolling-7-day KPIs + WoW deltas (timeLearning / XP / lessons / streak)
    [HttpGet("Children/{id:int}/WeeklyKpis")]
    [ProducesResponseType(typeof(BaseResponse<WeeklyKpisDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildWeeklyKpis(int id)
        => NewResult(await Mediator.Send(new GetChildWeeklyKpisQuery(id)));

    // E4 — Per-subject mastery percentages (Math / Science / Arabic / English + overall)
    [HttpGet("Children/{id:int}/SubjectMastery")]
    [ProducesResponseType(typeof(BaseResponse<SubjectMasteryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildSubjectMastery(int id)
        => NewResult(await Mediator.Send(new GetChildSubjectMasteryQuery(id)));

    // E5 — Weak areas / focus areas (all-subjects, ranked by severity)
    [HttpGet("Children/{id:int}/WeakAreas")]
    [ProducesResponseType(typeof(BaseResponse<WeakAreasResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildWeakAreas(int id)
        => NewResult(await Mediator.Send(new GetChildWeakAreasQuery(id)));

    // E6 — Report charts (daily XP series, 20-day trend, time-of-day buckets)
    [HttpGet("Children/{id:int}/Reports")]
    [ProducesResponseType(typeof(BaseResponse<ChildReportsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildReports(int id)
        => NewResult(await Mediator.Send(new GetChildReportsQuery(id)));

    // E7 — Energy balance + weekly usage by helper kind (pure-read, no write-on-read)
    [HttpGet("Children/{id:int}/Energy")]
    [ProducesResponseType(typeof(BaseResponse<ChildEnergyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildEnergy(int id)
        => NewResult(await Mediator.Send(new GetChildEnergyQuery(id)));

    // E8 — Activity feed (read-time composed from XP / badges / energy / lessons seams)
    [HttpGet("Children/{id:int}/Activity")]
    [ProducesResponseType(typeof(BaseResponse<ActivityFeedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildActivity(int id)
        => NewResult(await Mediator.Send(new GetChildActivityQuery(id)));

    // E9 — Stored weekly report (generated by Hangfire job; latest or by ?week=yyyy-MM-dd)
    // First-week / no-report state → 200 OK with ReportFound=false; never 404.
    [HttpGet("Children/{id:int}/WeeklyReport")]
    [ProducesResponseType(typeof(BaseResponse<WeeklyReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildWeeklyReport(int id, [FromQuery] DateTime? week = null)
        => NewResult(await Mediator.Send(new GetWeeklyReportQuery(id, week)));
}
