using Learnexia.Modules.Parent.Api.Bases;
using Learnexia.Modules.Parent.Application.Features.AddChild;
using Learnexia.Modules.Parent.Application.Features.ChangeChildLearningLanguage;
using Learnexia.Modules.Parent.Application.Features.LinkChild;
using Learnexia.Modules.Parent.Application.Features.ListMyChildren;
using Learnexia.Modules.Parent.Application.Features.TransitionChildGrade;
using Learnexia.Modules.Parent.Application.Features.UnlinkChild;
using Learnexia.Modules.Parent.Application.Features.UpdateChild;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Parent.Api.Controllers;

// Parent-facing family endpoints (relocated from Identity into the Parent module). Gated to the Parent role
// (Admin/SuperAdmin may also call, e.g. support flows). Role names match the Roles enum PascalCase, which is
// what the JWT emits. The acting parent is ALWAYS resolved from the JWT inside the handlers
// (ICurrentUserService) — never from the request body. Family-scope is enforced per handler.
[Authorize(Roles = "Parent,Admin,SuperAdmin")]
[Route("api/Parent")]
[ApiController]
public class ParentController : AppControllerBase
{
    [HttpPost("Add-Child")]
    [ProducesResponseType(typeof(BaseResponse<AddedChildResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddChild([FromBody] AddChildCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPost("Link-Child")]
    [ProducesResponseType(typeof(BaseResponse<LinkedChildResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LinkChild([FromBody] LinkChildCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update-Child")]
    [ProducesResponseType(typeof(BaseResponse<UpdatedChildResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateChild([FromBody] UpdateChildCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpGet("My-Children")]
    [ProducesResponseType(typeof(BaseResponse<IEnumerable<LinkedChildResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyChildren()
        => NewResult(await Mediator.Send(new ListMyChildrenQuery()));

    // NEW (P2-12): unlink a child. The acting parent is resolved from the JWT; the last-parent guard +
    // family-scope check are enforced in the handler.
    [HttpDelete("Unlink-Child")]
    [ProducesResponseType(typeof(BaseResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlinkChild([FromBody] UnlinkChildCommand command)
        => NewResult(await Mediator.Send(command));

    // NEW (P8-04): change a child's learning language. Destructive — resets Math/Science Attempt rows.
    // Gated behind confirmFreshStart = true in the body (returns 424 if absent/false).
    // Family-scope: acting parent resolved from JWT; IsLinked check in handler.
    [HttpPut("Change-Learning-Language")]
    [ProducesResponseType(typeof(BaseResponse<ChangedLearningLanguageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeLearningLanguage([FromBody] ChangeLearningLanguageCommand command)
        => NewResult(await Mediator.Send(command));

    // NEW (P5-06): parent-initiated grade transition for a linked child.
    // Non-destructive: XP, badges, streaks, mastery are preserved.
    // childId from route; targetGrade from body; acting parent resolved from JWT.
    // Same-grade request is a success no-op (isNoOp=true, no event). Invalid grade (outside 1–6) → 422.
    [HttpPut("Children/{childId:int}/Grade")]
    [ProducesResponseType(typeof(BaseResponse<TransitionedChildGradeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TransitionChildGrade(
        [FromRoute] int childId,
        [FromBody] TransitionChildGradeCommand command)
    {
        command.ChildId = childId;
        return NewResult(await Mediator.Send(command));
    }
}
