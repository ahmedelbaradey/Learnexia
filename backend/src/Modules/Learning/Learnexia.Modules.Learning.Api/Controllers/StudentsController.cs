using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetStudentAttempts;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Student-scoped learning endpoints.
/// Route: api/Learning/Students
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
public class StudentsController : AppControllerBase
{
    /// <summary>
    /// Returns the list of quiz attempts for the specified student, ordered by StartedAt descending.
    /// StudentId in the route is cross-checked against the JWT — a student may only read their own attempts.
    /// Empty list is valid (200); CorrectAnswer is NEVER in the response.
    /// </summary>
    /// <param name="studentId">The identity user ID of the student whose attempts are requested.</param>
    [HttpGet("{studentId}/Attempts")]
    [Authorize]
    [ProducesResponseType(typeof(BaseResponse<List<AttemptListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttempts([FromRoute] int studentId)
        => NewResult(await Mediator.Send(new GetStudentAttemptsQuery { StudentId = studentId }));
}
