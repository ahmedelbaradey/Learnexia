using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Reviews.Queries.GetDueReviews;

/// <summary>
/// Returns the list of skills due for spaced-repetition review for the JWT-authenticated student.
///
/// StudentId is resolved server-side from <c>ICurrentUserService.UserId</c> — NEVER from the
/// request body or route — ensuring IDOR safety (a student reads only their own due list).
///
/// Query — not auto-validated by <c>ValidationBehavior</c> (CLAUDE rule 4).
/// </summary>
public record GetDueReviewsQuery : IQuery<BaseResponse<IReadOnlyList<DueReviewDto>>>;
