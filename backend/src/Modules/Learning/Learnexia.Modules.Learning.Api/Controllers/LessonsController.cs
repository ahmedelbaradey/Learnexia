using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Reorder;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.SetActive;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Modules.Learning.Application.Features.Lessons.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Lessons.Queries.GetAdmin;
using Learnexia.Modules.Learning.Application.Features.Lessons.Queries.List;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

[Route("api/learning/[controller]")]
[ApiController]
public class LessonsController : AppControllerBase
{
    /// <summary>
    /// Admin-only flat list of all lessons (including inactive ones). Paginated.
    /// Anonymous access is intentionally blocked — this endpoint exposes admin metadata
    /// (EstimatedMinutes, UnitId, SequenceOrder, SkillId). Students use
    /// GET /Subjects/{id}/Lessons for the active-only curriculum view.
    /// P7-SEC-1: gated post security audit (mirrors Subjects/Units List contract).
    /// </summary>
    [HttpGet("List")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> List([FromQuery] ListLessonsQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// Get a lesson by ID (student-facing route). Returns the lesson with optional
    /// Explanation, Visual, and QuickCheck (first quiz question; CorrectAnswer excluded).
    /// P7-02: Only active (IsActive=true) lessons are returned to students.
    /// Requires authentication. Introduced in P2-05.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(BaseResponse<SingleLessonResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdRoute(int id)
        => NewResult(await Mediator.Send(new GetLessonQuery { Id = id }));

    /// <remarks>
    /// Deprecated. Use <c>GET /api/learning/Lessons/{id}</c> (authenticated route) introduced in P2-05.
    /// Kept for back-compat with existing admin tooling. Will be removed in P6-06 / hardening wave.
    /// </remarks>
    [HttpGet]
    [Authorize]   // P8-SEC-2: closes unauthenticated language-guard bypass; all lesson access requires auth.
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetLessonQuery { Id = id }));

    [HttpPost("Create")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] AddLessonCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Update([FromBody] EditLessonCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteLessonCommand { Id = id }));

    // ── P7-02 Admin management endpoints ─────────────────────────────────────

    /// <summary>
    /// Admin-only: returns full lesson detail including IsActive, EstimatedMinutes, and all
    /// non-deleted content blocks (incl. inactive) ordered by SequenceOrder.
    /// GET /api/learning/Lessons/{id}/Admin
    /// </summary>
    [HttpGet("{id:int}/Admin")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<AdminLessonDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminDetail(int id)
        => NewResult(await Mediator.Send(new GetAdminLessonQuery { Id = id }));

    /// <summary>
    /// Activates or deactivates a lesson. Inactive lessons are hidden from student reads.
    /// PUT /api/learning/Lessons/{id}/Active
    /// Body: { lessonId: 1, isActive: false }
    /// </summary>
    [HttpPut("{id:int}/Active")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetLessonActiveCommand command)
        => NewResult(await Mediator.Send(command with { LessonId = id }));

    /// <summary>
    /// Batch-reorders lessons within the same Unit.
    /// PUT /api/learning/Lessons/Reorder
    /// Body: { lessonIds: [3, 1, 2] } — position = new SequenceOrder.
    /// </summary>
    [HttpPut("Reorder")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reorder([FromBody] ReorderLessonsCommand command)
        => NewResult(await Mediator.Send(command));
}
