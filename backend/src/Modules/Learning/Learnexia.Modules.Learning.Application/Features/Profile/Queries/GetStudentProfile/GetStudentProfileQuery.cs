using Learnexia.Modules.Learning.Application.Features.Profile.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Profile.Queries.GetStudentProfile;

/// <summary>
/// Query for the student behavioral profile inspection endpoint (P3-13 BE-8 / AC5).
///
/// IDOR safety: studentId is resolved EXCLUSIVELY from ICurrentUserService.UserId inside
/// the handler — it is NEVER a parameter on this query record. The query carries no identity
/// inputs; the handler resolves the caller's identity from the JWT.
/// </summary>
public record GetStudentProfileQuery : IQuery<BaseResponse<StudentLearningProfileDto>>;
