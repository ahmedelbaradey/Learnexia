using Learnexia.Modules.Learning.Application.Features.Mastery.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Mastery.Queries.GetStudentMastery;

/// <summary>
/// Returns all mastery rows for the student identified by the authenticated JWT.
/// StudentId is resolved server-side from <c>ICurrentUserService.UserId</c> — NEVER from the request.
/// This is an IDOR guard: a student can only read their own mastery.
/// Query — not auto-validated by ValidationBehavior (CLAUDE rule 4).
/// </summary>
public record GetStudentMasteryQuery : IQuery<BaseResponse<List<MasteryDto>>>;
