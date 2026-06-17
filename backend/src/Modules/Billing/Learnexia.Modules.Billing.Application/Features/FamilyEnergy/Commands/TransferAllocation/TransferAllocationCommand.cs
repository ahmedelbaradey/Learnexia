using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Commands.TransferAllocation;

/// <summary>
/// Command: transfer unspent allocated energy from one child to another within the same family
/// (P10-16-BE-5).
///
/// <para><strong>Security / IDOR:</strong> <c>ParentUserId</c> is JWT-resolved in the handler via
/// <c>ICurrentUserService.UserId</c> — never supplied by the client. All business rules (same-family
/// guard, cap-at-remaining, active-seat check, mid-cycle-INSERT) live in the service, not here.</para>
///
/// <para><strong>IdempotencyKey:</strong> client-supplied (OQ-I). A retry with the same key returns
/// the prior result without double-moving.</para>
///
/// <para><strong>No UoW (ADR 0001):</strong> the service owns the explicit transaction.</para>
/// </summary>
public sealed record TransferAllocationCommand(
    int FromChildId,
    int ToChildId,
    long Amount,
    string IdempotencyKey) : ICommand<BaseResponse<TransferResultDto>>;
