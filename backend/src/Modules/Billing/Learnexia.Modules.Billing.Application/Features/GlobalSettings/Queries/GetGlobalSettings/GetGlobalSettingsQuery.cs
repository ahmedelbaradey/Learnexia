using Learnexia.Modules.Billing.Application.Features.GlobalSettings.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.GlobalSettings.Queries.GetGlobalSettings;

/// <summary>
/// Admin-only query: returns all managed <c>GlobalSetting</c> rows with current value/type/audit fields.
/// Not auto-validated (<c>IQuery&lt;T&gt;</c> — CONVENTIONS rule 4).
/// </summary>
public sealed class GetGlobalSettingsQuery : IQuery<BaseResponse<IReadOnlyList<GlobalSettingDto>>>
{
}
