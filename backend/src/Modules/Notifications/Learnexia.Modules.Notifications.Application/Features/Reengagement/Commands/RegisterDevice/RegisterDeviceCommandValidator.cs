using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.RegisterDevice;

public sealed class RegisterDeviceCommandValidator
    : AbstractValidator<RegisterDeviceCommand>
{
    private static readonly string[] ValidPlatforms = ["ios", "android", "web"];

    public RegisterDeviceCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.ExpoPushToken)
            .NotEmpty()
            .WithMessage(localizer[SharedResourcesKey.DeviceTokenRequired]);

        RuleFor(x => x.Platform)
            .NotEmpty()
            .Must(p => ValidPlatforms.Contains(p?.ToLowerInvariant()))
            .WithMessage(localizer[SharedResourcesKey.DevicePlatformInvalid]);
    }
}
