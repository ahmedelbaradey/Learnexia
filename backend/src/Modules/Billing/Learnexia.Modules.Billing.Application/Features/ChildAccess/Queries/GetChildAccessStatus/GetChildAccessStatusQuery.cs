using Learnexia.Modules.Billing.Application.Features.ChildAccess.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.ChildAccess.Queries.GetChildAccessStatus;

/// <summary>
/// Query: returns the combined access status for a child (P10-18-BE-6).
///
/// <para>Returns <see cref="ChildAccessStatusDto"/> containing <c>SeatState</c>,
/// <c>ParentPauseState</c>, and the derived <c>IsAccessAllowed</c> flag.</para>
///
/// <para><strong>Security / IDOR:</strong> <c>ParentUserId</c> is JWT-resolved in the handler.
/// The child must belong to the authenticated parent's family.</para>
///
/// <para><strong>Note:</strong> queries are NOT auto-validated by <c>ValidationBehavior</c>;
/// input validation is performed inside the handler.</para>
/// </summary>
public sealed record GetChildAccessStatusQuery(int ChildId) : IQuery<BaseResponse<ChildAccessStatusDto>>;
