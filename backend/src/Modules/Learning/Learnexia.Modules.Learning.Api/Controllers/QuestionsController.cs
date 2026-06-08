using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Questions.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Questions.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Questions.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Questions.Commands.Reorder;
using Learnexia.Modules.Learning.Application.Features.Questions.Commands.SetActive;
using Learnexia.Modules.Learning.Application.Features.Questions.Dtos;
using Learnexia.Modules.Learning.Application.Features.Questions.Queries.GetById;
using Learnexia.Modules.Learning.Application.Features.Questions.Queries.ListForLesson;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Admin endpoints for authoring quiz questions within a lesson's implicit quiz.
/// All routes are <c>AdminOnly</c> — question authoring is an admin-only operation.
///
/// A new dedicated controller is used (rather than extending QuizzesController) because:
///   1. QuizzesController is student-facing (Start/Submit/Complete/Abandon) — mixing admin
///      authoring routes there would blur the admin/student boundary and raise IDOR risk.
///   2. The module follows a one-controller-per-resource style (ContentBlocksController,
///      SkillsController, KnowledgeGraphController all have dedicated controllers).
///   3. Clear AdminOnly gating is easier to verify when all endpoints in the controller
///      share the same access level.
///
/// Route: api/Learning/Questions
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
public class QuestionsController : AppControllerBase
{
    /// <summary>
    /// Returns all non-deleted quiz questions for a lesson (admin view — includes inactive),
    /// ordered by SequenceOrder. Includes CorrectAnswer for authoring purposes.
    /// GET /api/Learning/Questions/ByLesson/{lessonId}
    /// </summary>
    [HttpGet("ByLesson/{lessonId:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<List<AdminQuestionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByLesson(int lessonId)
        => NewResult(await Mediator.Send(new ListQuestionsForLessonQuery { LessonId = lessonId }));

    /// <summary>
    /// Returns a single quiz question by Id (admin view — includes CorrectAnswer).
    /// GET /api/Learning/Questions/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<AdminQuestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetQuestionByIdQuery { Id = id }));

    /// <summary>
    /// Adds a quiz question to a lesson (appended at end of sequence).
    /// POST /api/Learning/Questions
    /// Body: { lessonId, skillId?, questionType, questionText, options, correctAnswer, difficulty, generatedBy }
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Add([FromBody] AddQuestionCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Edits an existing quiz question's type, text, options, correct answer, and difficulty.
    /// PUT /api/Learning/Questions
    /// Body: { id, questionType, questionText, options, correctAnswer, difficulty }
    /// </summary>
    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Edit([FromBody] EditQuestionCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Soft-deletes a quiz question (IsDeleted = true).
    /// The global soft-delete filter hides it from all subsequent reads (admin and student).
    /// DELETE /api/Learning/Questions/{id}
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteQuestionCommand { Id = id }));

    /// <summary>
    /// Batch-reorders quiz questions within a lesson.
    /// PUT /api/Learning/Questions/Reorder
    /// Body: { lessonId: 5, questionIds: [3, 1, 2] } — position = new SequenceOrder.
    /// LessonId is the access-scoping anchor: every ID must resolve to that lesson.
    /// </summary>
    [HttpPut("Reorder")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reorder([FromBody] ReorderQuestionsCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Activates or deactivates a quiz question (IsActive toggle).
    /// Inactive questions are hidden from student quiz/attempt reads but are not deleted.
    /// PATCH /api/Learning/Questions/{id}/SetActive
    /// Body: { id, isActive }
    /// </summary>
    [HttpPatch("{id:int}/SetActive")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetQuestionActiveCommand command)
    {
        command = command with { Id = id };
        return NewResult(await Mediator.Send(command));
    }
}
