using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminGetUserFamily;

/// <summary>
/// Admin family-linkage query (P7-06 BE-3).
/// For a parent → list of linked children.
/// For a child  → list of linked parents.
/// Cross-module data sourced via <c>IParentChildQuery</c> seam (no cross-module FK).
/// An <c>IQuery</c> — NOT auto-validated; input guard runs in the handler.
/// </summary>
public record GetUserFamilyQuery : IQuery<BaseResponse<AdminFamilyDto>>
{
    public int UserId { get; init; }
}
