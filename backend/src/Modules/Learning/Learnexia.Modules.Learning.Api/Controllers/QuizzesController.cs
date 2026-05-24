using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Attempts.Commands.StartAttempt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Learning quiz endpoints.
/// Route: api/Learning/Quizzes
/// </summary>
[Route("api/Learning/[controller]")]
public class QuizzesController : AppControllerBase
{
    /// <summary>
    /// Start a quiz attempt for the given lesson.
    /// StudentId is resolved from the authenticated JWT — it is NEVER supplied by the client.
    /// Returns the new AttemptId and the lesson's questions without their correct answers.
    /// </summary>
    /// <param name="lessonId">The lesson whose question set forms this quiz.</param>
    [HttpPost("{lessonId}/Attempt")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> StartAttempt([FromRoute] int lessonId)
        => NewResult(await Mediator.Send(new StartAttemptCommand { LessonId = lessonId }));
}
