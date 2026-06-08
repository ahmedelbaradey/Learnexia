using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminGetUserActivity;

/// <summary>
/// Admin activity summary query (P7-06 BE-4).
/// Composes data from Gamification seam queries; each seam call is best-effort.
/// A failing seam degrades to null/empty in the DTO — NEVER returns 500.
/// An <c>IQuery</c> — NOT auto-validated; input guard runs in the handler.
/// </summary>
public record GetUserActivitySummaryQuery : IQuery<BaseResponse<AdminActivitySummaryDto>>
{
    public int UserId { get; init; }
}
