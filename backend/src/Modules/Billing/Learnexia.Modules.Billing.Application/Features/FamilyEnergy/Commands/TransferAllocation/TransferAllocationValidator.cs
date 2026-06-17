using FluentValidation;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Commands.TransferAllocation;

/// <summary>
/// FluentValidation validator for <see cref="TransferAllocationCommand"/> (P10-16-BE-5).
///
/// <para><strong>Shape-rules only (per CONVENTIONS rule 4):</strong>
/// <list type="bullet">
///   <item><c>Amount</c> must be &gt; 0.</item>
///   <item><c>FromChildId</c> and <c>ToChildId</c> must be &gt; 0.</item>
///   <item><c>FromChildId</c> must not equal <c>ToChildId</c>.</item>
///   <item><c>IdempotencyKey</c> must be provided.</item>
/// </list>
/// </para>
///
/// <para><strong>NOT validated here:</strong> same-family guard, cap-at-remaining, active-seat check,
/// destination zero-allocation (mid-cycle valid use case). All business rules live in the service.</para>
/// </summary>
public sealed class TransferAllocationValidator : AbstractValidator<TransferAllocationCommand>
{
    public TransferAllocationValidator()
    {
        RuleFor(x => x.FromChildId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.EmptyIdValidation);

        RuleFor(x => x.ToChildId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.EmptyIdValidation);

        RuleFor(x => x)
            .Must(cmd => cmd.FromChildId != cmd.ToChildId)
            .WithMessage(SharedResourcesKey.AllocationTransferSameChild);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.AllocationTransferAmountRequired);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage(SharedResourcesKey.Required)
            .MaximumLength(128)
            .WithMessage(SharedResourcesKey.MaxLengthValidation);
    }
}
