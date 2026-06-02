using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Queries.GetChildReengagementPreferences;

/// <summary>
/// Returns the per-child re-engagement preferences for all 3 schedulable categories.
/// Callers receive defaults for any category without a persisted row (nothing is written on read).
/// Requires a parent JWT; handler enforces <c>IsParentOfChildAsync</c> check (AC3).
/// Queries are NOT auto-validated (CONVENTIONS §4).
/// </summary>
public sealed record GetChildReengagementPreferencesQuery(int ChildId)
    : IQuery<BaseResponse<List<ChildReengagementPreferenceDto>>>;
