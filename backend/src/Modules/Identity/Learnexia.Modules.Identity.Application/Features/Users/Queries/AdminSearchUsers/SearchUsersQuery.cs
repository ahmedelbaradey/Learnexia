using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminSearchUsers;

/// <summary>
/// Admin paginated/filterable search over parent + child accounts (P7-06 BE-1).
/// An <c>IQuery</c> — NOT auto-validated by <c>ValidationBehavior</c>; input guards run in the handler.
/// </summary>
public record SearchUsersQuery : IQuery<PaginatedResult<AdminUserListItemDto>>
{
    /// <summary>Optional role filter ("Parent" / "Student" / "Admin" etc.).</summary>
    public string? Role { get; init; }

    /// <summary>
    /// Optional <c>AccountStatus</c> filter (P7-07 upgrade from IsActive flag).
    /// Values: 0 = Active, 1 = Suspended, 2 = Deleted.
    /// When null, results include Active and Suspended accounts (Deleted excluded by base query).
    /// </summary>
    public AccountStatus? Status { get; init; }

    /// <summary>Optional free-text filter over <c>FullName</c> OR <c>Email</c>.</summary>
    public string? Q { get; init; }

    public int PageNumber { get; init; } = 1;

    /// <summary>Page size clamped to 1–100 in the handler.</summary>
    public int PageSize { get; init; } = 20;

    public string? OrderBy { get; init; }
}
