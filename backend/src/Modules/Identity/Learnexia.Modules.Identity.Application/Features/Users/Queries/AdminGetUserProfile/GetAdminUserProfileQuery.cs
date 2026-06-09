using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminGetUserProfile;

/// <summary>
/// Admin-specific user profile query (P7-06 BE-2).
/// Returns the admin-inspect projection including both language axes and status history.
/// An <c>IQuery</c> — NOT auto-validated; input guard runs in the handler.
/// Emits <c>AdminActionPerformedEvent</c> post-read (best-effort, no commit needed for reads).
/// </summary>
public record GetAdminUserProfileQuery : IQuery<BaseResponse<AdminUserProfileDto>>
{
    public int UserId { get; init; }
}
