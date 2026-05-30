using Learnexia.Modules.Gamification.Application.Features.Profile.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Profile.Queries.GetMyProfile;

/// <summary>
/// Returns the XP profile for the currently authenticated student.
/// StudentId is resolved from <c>ICurrentUserService.UserId</c> inside the handler —
/// no client-supplied identity parameter (IDOR-proof by construction).
///
/// This is a QUERY (read-only) — <c>IQuery&lt;T&gt;</c>, NOT <c>ICommand&lt;T&gt;</c>.
/// <c>ValidationBehavior</c> does NOT apply (rule 4). No fluent validator needed.
/// </summary>
public record GetMyProfileQuery : IQuery<BaseResponse<StudentProfileDto>>;
