using System.Threading.Tasks;
using Learnexia.Modules.Notifications.Api.Bases;
using Learnexia.Modules.Notifications.Application.Features.Notifications.Queries.List;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Notifications.Api.Controllers;

[Route("api/Notifications/[controller]")]
[ApiController]
public class NotificationsController : AppControllerBase
{
    // Admin/observability lookup (P4-01-BE-10): an admin reads the notifications produced for a given
    // Identity user (e.g. the welcome notification from the UserRegisteredIntegrationEvent consumer).
    // recipientUserId is the Identity integer user id (the event's UserId), matched against
    // Notification.RecipientExternalUserId. Locked behind the AdminOnly policy (Admin/SuperAdmin role):
    // anonymous/non-admin callers get 401/403, so the by-recipient lookup is no longer an IDOR.
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] int recipientUserId)
        => NewResult(await Mediator.Send(new ListQuery { RecipientUserId = recipientUserId }));
}
