using Learnexia.Modules.Notifications.Application.Features.Preferences.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Preferences.Queries.GetMyPreferences;

/// <summary>
/// Reads the authenticated caller's notification preferences (P2-12). Self-scoped: the user is resolved
/// from the JWT inside the handler (ICurrentUserService), never from a parameter. When no rows exist yet,
/// the handler returns sensible DEFAULTS for ALL categories — it does NOT 404 and does NOT persist on read.
/// Queries are NOT auto-validated (CONVENTIONS §4).
/// </summary>
public sealed record GetMyNotificationPreferencesQuery : IQuery<BaseResponse<NotificationPreferencesResponse>>;
