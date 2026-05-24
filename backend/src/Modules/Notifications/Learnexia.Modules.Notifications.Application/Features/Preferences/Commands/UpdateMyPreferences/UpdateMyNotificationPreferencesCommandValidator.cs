using FluentValidation;
using Learnexia.Modules.Notifications.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Preferences.Commands.UpdateMyPreferences;

/// <summary>
/// Validates <see cref="UpdateMyNotificationPreferencesCommand"/> before the handler runs (P2-12 BE-1).
/// The command must supply only defined <see cref="NotificationCategory"/> values; duplicates are rejected
/// so the upsert cannot overwrite a row twice in the same request.
/// </summary>
public sealed class UpdateMyNotificationPreferencesCommandValidator
    : AbstractValidator<UpdateMyNotificationPreferencesCommand>
{
    public UpdateMyNotificationPreferencesCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.Preferences)
            .NotEmpty()
            .Must(prefs => prefs.All(p => Enum.IsDefined(typeof(NotificationCategory), p.Category)))
            .WithMessage(localizer[SharedResourcesKey.NotificationPreferenceInvalidCategory])
            .Must(prefs => prefs.Select(p => p.Category).Distinct().Count() == prefs.Count)
            .WithMessage(localizer[SharedResourcesKey.NotificationPreferenceDuplicateCategory]);
    }
}
