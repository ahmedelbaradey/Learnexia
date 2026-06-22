using FluentValidation;
using Learnexia.Modules.Billing.Application.Abstractions;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.SimulateProviderPayment;

/// <summary>
/// FluentValidation validator for <see cref="SimulateProviderPaymentCommand"/>.
/// Runs automatically via <c>ValidationBehavior</c> (commands only — per Convention §4).
/// </summary>
public sealed class SimulateProviderPaymentValidator : AbstractValidator<SimulateProviderPaymentCommand>
{
    /// <summary>Allowed normalized event type values.</summary>
    private static readonly HashSet<string> AllowedEventTypes = new(StringComparer.Ordinal)
    {
        WebhookEventTypes.PaymentSucceeded,
        WebhookEventTypes.PaymentFailed,
        WebhookEventTypes.RefundSucceeded,
    };

    public SimulateProviderPaymentValidator()
    {
        RuleFor(x => x.PaymentId)
            .GreaterThan(0)
            .WithMessage(SharedResourcesKey.SimulationPaymentIdRequired);

        RuleFor(x => x.EventType)
            .NotEmpty()
            .WithMessage(SharedResourcesKey.SimulationEventTypeRequired)
            .Must(t => AllowedEventTypes.Contains(t))
            .WithMessage(SharedResourcesKey.SimulationEventTypeUnsupported);
    }
}
