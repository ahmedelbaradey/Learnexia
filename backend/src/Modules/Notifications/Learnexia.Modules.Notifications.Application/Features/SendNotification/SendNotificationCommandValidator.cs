using FluentValidation;

namespace Learnexia.Modules.Notifications.Application.Features.SendNotification;

public sealed class SendNotificationCommandValidator : AbstractValidator<SendNotificationCommand>
{
    public SendNotificationCommandValidator()
    {
        RuleFor(x => x.RecipientUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.NotificationTypeId).NotEmpty();
        RuleFor(x => x.RecipientEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.RecipientEmail));
    }
}
