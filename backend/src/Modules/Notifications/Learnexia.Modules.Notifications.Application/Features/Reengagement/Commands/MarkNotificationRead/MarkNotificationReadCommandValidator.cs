using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandValidator
    : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty()
            .WithMessage(localizer[SharedResourcesKey.NotificationIdRequired]);
    }
}
