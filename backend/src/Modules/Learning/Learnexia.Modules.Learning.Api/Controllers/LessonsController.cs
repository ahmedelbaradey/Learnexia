using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Modules.Learning.Application.Features.Lessons.Queries.Get;
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
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListLessonsQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// Get a lesson by ID (student-facing route). Returns the lesson with optional
    /// Explanation, Visual, and QuickCheck (first quiz question; CorrectAnswer excluded).
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
}
