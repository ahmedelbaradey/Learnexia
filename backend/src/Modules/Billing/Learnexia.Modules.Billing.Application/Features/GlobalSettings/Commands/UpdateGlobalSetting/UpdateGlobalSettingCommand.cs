using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.GlobalSettings.Commands.UpdateGlobalSetting;

/// <summary>
/// Admin-only command: updates a single managed <c>GlobalSetting</c> value.
/// Triggers DB update → AdminActionPerformedEvent (audit) → in-memory cache invalidation.
/// Auto-validated by <c>ValidationBehavior</c> via <see cref="UpdateGlobalSettingValidator"/>.
///
/// <para><c>UpdatedBy</c> is NOT part of the request body — it is derived server-side from
/// the JWT claims in the handler via <c>ICurrentUserService</c> (security: prevents spoofing).</para>
/// </summary>
public sealed class UpdateGlobalSettingCommand : ICommand<BaseResponse<bool>>
{
    /// <summary>The managed key to update (must be in <c>GlobalSettingKeys.ManagedKeys</c>).</summary>
    public string Key { get; set; } = default!;

    /// <summary>New value as a string; parsed to the key's declared type during validation.</summary>
    public string NewValue { get; set; } = default!;
}
