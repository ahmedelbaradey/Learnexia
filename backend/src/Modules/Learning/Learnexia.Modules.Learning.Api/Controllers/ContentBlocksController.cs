using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Reorder;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Queries.List;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Admin endpoints for managing content blocks within a lesson.
/// All routes are <c>AdminOnly</c> — content authoring is an admin-only operation.
///
/// Routes are nested under /api/learning/ContentBlocks to mirror the module controller pattern.
/// The lesson-scoping constraint (all blocks belong to one lesson) is enforced by the handlers/validators.
/// </summary>
[Route("api/learning/[controller]")]
[ApiController]
public class ContentBlocksController : AppControllerBase
{
    /// <summary>
    /// Returns all non-deleted content blocks for a lesson (incl. inactive), ordered by SequenceOrder.
    /// GET /api/learning/ContentBlocks/ByLesson/{lessonId}
    /// </summary>
    [HttpGet("ByLesson/{lessonId:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<List<AdminContentBlockDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLesson(int lessonId)
        => NewResult(await Mediator.Send(new ListContentBlocksQuery { LessonId = lessonId }));

    /// <summary>
    /// Adds a content block to a lesson (appended at end of sequence).
    /// POST /api/learning/ContentBlocks
    /// Body: { lessonId, blockType, payload }
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Add([FromBody] AddContentBlockCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Edits the BlockType and Payload of an existing content block.
    /// PUT /api/learning/ContentBlocks
    /// Body: { id, blockType, payload }
    /// </summary>
    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Edit([FromBody] EditContentBlockCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Soft-deletes a content block (IsDeleted = true).
    /// DELETE /api/learning/ContentBlocks/{id}
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteContentBlockCommand { Id = id }));

    /// <summary>
    /// Batch-reorders content blocks within a lesson.
    /// PUT /api/learning/ContentBlocks/Reorder
    /// Body: { contentBlockIds: [3, 1, 2] } — position = new SequenceOrder.
    /// All IDs must belong to the same lesson.
    /// </summary>
    [HttpPut("Reorder")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reorder([FromBody] ReorderContentBlocksCommand command)
        => NewResult(await Mediator.Send(command));
}
