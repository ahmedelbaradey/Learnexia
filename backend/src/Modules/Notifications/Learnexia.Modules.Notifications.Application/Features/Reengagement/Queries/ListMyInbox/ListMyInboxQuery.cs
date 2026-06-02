using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Queries.ListMyInbox;

/// <summary>
/// Returns the authenticated user's in-app notification inbox, newest-first, with pagination.
/// Self-scoped: the recipient user id is resolved from the JWT — never a query parameter.
/// Queries are NOT auto-validated (CONVENTIONS §4).
/// </summary>
public sealed record ListMyInboxQuery(int Take = 20, int Skip = 0)
    : IQuery<BaseResponse<PagedInboxResult>>;
