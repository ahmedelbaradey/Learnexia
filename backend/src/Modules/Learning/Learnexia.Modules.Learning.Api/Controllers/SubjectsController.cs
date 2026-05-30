using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Application.Features.Subjects.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectLessons;
using Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectsForGrade;
using Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectSkillTree;
using Learnexia.Modules.Learning.Application.Features.Subjects.Queries.List;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

[Route("api/learning/[controller]")]
[ApiController]
public class SubjectsController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListSubjectsQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetSubjectQuery { Id = id }));

    // ── Student-facing browse endpoints (P2-02) ─────��─────────────────────────

    /// <summary>
    /// Returns all subjects for a given grade number (1–6).
    /// GET /api/learning/Subjects/ForGrade?grade={n}
    /// </summary>
    [HttpGet("ForGrade")]
    [ProducesResponseType(typeof(BaseResponse<List<StudentSubjectDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForGrade([FromQuery] int grade)
        => NewResult(await Mediator.Send(new GetSubjectsForGradeQuery { Grade = grade }));

    /// <summary>
    /// Returns a subject's units with nested lessons, both ordered by SequenceOrder.
    /// GET /api/learning/Subjects/{id}/Lessons
    /// </summary>
    [HttpGet("{id:int}/Lessons")]
    [Authorize]   // P2-04: tightened from anonymous. BREAKING CHANGE: unauthenticated callers now get 401.
    [ProducesResponseType(typeof(BaseResponse<List<UnitWithLessonsDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLessons(int id)
        => NewResult(await Mediator.Send(new GetSubjectLessonsQuery { SubjectId = id }));

    /// <summary>
    /// Returns a subject's concept-and-skill tree with engine-derived per-student NodeState values.
    /// GET /api/learning/Subjects/{id}/SkillTree
    /// </summary>
    [HttpGet("{id:int}/SkillTree")]
    [Authorize]   // P2-04: tightened from anonymous. BREAKING CHANGE: unauthenticated callers now get 401.
    [ProducesResponseType(typeof(BaseResponse<List<ConceptNodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSkillTree(int id)
        => NewResult(await Mediator.Send(new GetSubjectSkillTreeQuery { SubjectId = id }));

    // ── Admin CRUD ────────────────────────────────────────────────────────────

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] AddSubjectCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromBody] EditSubjectCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteSubjectCommand { Id = id }));
}
