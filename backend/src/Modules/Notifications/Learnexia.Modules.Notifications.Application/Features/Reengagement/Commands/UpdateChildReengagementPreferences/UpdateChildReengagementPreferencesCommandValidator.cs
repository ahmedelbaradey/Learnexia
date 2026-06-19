using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.UpdateChildReengagementPreferences;

/// <summary>
/// Validates <see cref="UpdateChildReengagementPreferencesCommand"/> (P4-09 B4-3 / AC3).
/// Rules:
/// <list type="bullet">
///   <item><c>DailyCap</c> ∈ [0, 10] when provided.</item>
///   <item><c>TimeZoneId</c> must be a valid IANA / Windows timezone ID when provided.</item>
///   <item>Quiet-hours: both <c>Start</c> + <c>End</c> must be set together, or both null.</item>
///   <item>Quiet-hours: <c>Start</c> and <c>End</c> must not be equal — equal values mean permanent silence (F-11).</item>
/// </list>
/// </summary>
public sealed class UpdateChildReengagementPreferencesCommandValidator
    : AbstractValidator<UpdateChildReengagementPreferencesCommand>
{
    public UpdateChildReengagementPreferencesCommandValidator(
        IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.DailyCap)
            .InclusiveBetween(0, 10)
            .When(x => x.DailyCap.HasValue)
            .WithMessage(localizer[SharedResourcesKey.InvalidDailyCapRange]);

        // P9-07: global daily push budget — parent-configurable, [1, 20] when provided.
        RuleFor(x => x.GlobalDailyPushBudget)
            .InclusiveBetween(1, 20)
            .When(x => x.GlobalDailyPushBudget.HasValue)
            .WithMessage(localizer[SharedResourcesKey.InvalidGlobalDailyPushBudgetRange]);

        RuleFor(x => x.TimeZoneId)
            .Must(BeAValidTimeZone!)
            .When(x => !string.IsNullOrWhiteSpace(x.TimeZoneId))
            .WithMessage(localizer[SharedResourcesKey.InvalidTimeZoneId]);

        RuleFor(x => x)
            .Must(x => (x.QuietHoursStartLocal == null) == (x.QuietHoursEndLocal == null))
            .WithMessage(localizer[SharedResourcesKey.QuietHoursBothOrNeither]);

        // F-11: equal start and end would create a silent window spanning the entire day.
        RuleFor(x => x.QuietHoursStartLocal)
            .NotEqual(x => x.QuietHoursEndLocal)
            .When(x => x.QuietHoursStartLocal.HasValue && x.QuietHoursEndLocal.HasValue)
            .WithMessage(localizer[SharedResourcesKey.QuietHoursStartEndMustDiffer]);
    }

    private static bool BeAValidTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
