using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Attempts.Commands.AbandonAttempt;
using Learnexia.Modules.Learning.Application.Features.Attempts.Commands.CompleteAttempt;
using Learnexia.Modules.Learning.Application.Features.Attempts.Commands.StartAttempt;
using Learnexia.Modules.Learning.Application.Features.Attempts.Commands.SubmitAnswer;
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

    /// <summary>
    /// Submit a student's answer for a single question within an in-progress attempt.
    /// AttemptId is taken from the route; StudentId is resolved from the JWT — NEVER from the client.
    /// Correctness is determined server-side.
    /// </summary>
    /// <param name="attemptId">The in-progress attempt to submit the answer into.</param>
    /// <param name="command">The answer payload (QuestionId, AnswerPayload, TimeSpentSeconds, HintUsed).</param>
    [HttpPost("{attemptId}/Answers")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitAnswer([FromRoute] int attemptId, [FromBody] SubmitAnswerCommand command)
    {
        command = command with { AttemptId = attemptId };
        return NewResult(await Mediator.Send(command));
    }

    /// <summary>
    /// Complete an in-progress attempt, finalising all aggregates
    /// (AccuracyPercentage, DurationSeconds, HintsUsedCount, CompletedAt).
    /// Idempotent: if already Completed, returns current state.
    /// StudentId is resolved from the JWT — NEVER from the client.
    /// </summary>
    /// <param name="attemptId">The in-progress attempt to complete.</param>
    [HttpPost("{attemptId}/Complete")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CompleteAttempt([FromRoute] int attemptId)
        => NewResult(await Mediator.Send(new CompleteAttemptCommand { AttemptId = attemptId }));

    /// <summary>
    /// Abandon an in-progress attempt, capturing partial signal from any answers submitted so far.
    /// Zero answers is valid (accuracy = 0). Idempotent: if already Abandoned, returns current state.
    /// StudentId is resolved from the JWT — NEVER from the client.
    /// </summary>
    /// <param name="attemptId">The in-progress attempt to abandon.</param>
    [HttpPost("{attemptId}/Abandon")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> AbandonAttempt([FromRoute] int attemptId)
        => NewResult(await Mediator.Send(new AbandonAttemptCommand { AttemptId = attemptId }));
}
