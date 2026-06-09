using Learnexia.Modules.Moderation.Api.Bases;
using Learnexia.Modules.Moderation.Application.Features.AuditLog.Queries.GetAuditLog;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Moderation.Api.Controllers;

/// <summary>
/// Admin-only, read-only audit log API.
///
/// Security constraints (P7-12 brief §Piece 3 + security lessons):
/// - All endpoints require <see cref="AuthorizationPolicies.AdminOnly"/> — non-admin → 403.
/// - No mutation endpoints exist: the log is immutable/append-only.
/// - DB-side pagination only: no full-table load.
/// - Results contain ids + enum/state values only (PII-safe).
/// </summary>
[Route("api/Admin/Audit")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AuditController : AppControllerBase
{
    /// <summary>
    /// Returns a paginated, filterable list of admin audit log entries.
    /// All filtering (adminUserId, actionType, targetEntityType, dateFrom, dateTo) is applied DB-side.
    /// Results are ordered newest-first.
    /// </summary>
    /// <param name="query">Query parameters (all optional).</param>
    [HttpGet("Log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLog([FromQuery] GetAuditLogQuery query)
        => NewResult(await Mediator.Send(query));
}
