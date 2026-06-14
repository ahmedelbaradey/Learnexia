using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Reviews.Queries.GetDueReviews;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Spaced-repetition review endpoints — student-scoped, IDOR-safe (P3-10 BE-7, AC2).
/// StudentId is ALWAYS resolved from the authenticated JWT (ICurrentUserService.UserId)
/// and NEVER accepted from route parameters or query strings.
/// Route: api/Learning/Reviews
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
[Authorize(Roles = "Student")]
public class ReviewsController : AppControllerBase
{
    /// <summary>
    /// Returns the list of skills due for spaced-repetition review for the authenticated student.
    ///
    /// A skill is "due" when:
    ///   - Its mastery status is <c>NeedsReview</c> (remediation needed immediately), OR
    ///   - Its mastery status is <c>Mastered</c> AND the scheduled review date has passed
    ///     (forgetting check — the spaced-repetition interval elapsed).
    ///
    /// An empty list is a valid 200 response — no skills are due for review.
    /// </summary>
    [HttpGet("Due")]
    [ProducesResponseType(typeof(BaseResponse<IReadOnlyList<DueReviewDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDueReviews()
        => NewResult(await Mediator.Send(new GetDueReviewsQuery()));
}
