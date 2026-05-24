using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Account.Queries.GetMySessions;

/// <summary>
/// Returns the active sessions for the authenticated caller (P2-12 SEC-2). Self-scoped: UserId is
/// resolved from the JWT inside the handler. Queries are NOT auto-validated (CONVENTIONS §4).
/// </summary>
public sealed record GetMySessionsQuery : IQuery<BaseResponse<List<SessionInfo>>>;
