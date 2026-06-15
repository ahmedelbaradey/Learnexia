using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.GlobalSettings.Commands.UpdateGlobalSetting;
using Learnexia.Modules.Billing.Application.Features.GlobalSettings.Queries.GetGlobalSettings;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Admin-only endpoints for reading and updating platform-wide global settings.
///
/// <para>Routes:
/// <list type="bullet">
///   <item><c>GET  /api/Admin/GlobalSettings</c> — returns all 17 managed keys (value/type/audit).</item>
///   <item><c>PUT  /api/Admin/GlobalSettings/{key}</c> — updates a single key; invalidates cache.</item>
/// </list>
/// </para>
/// </summary>
[Route("api/Admin/GlobalSettings")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class GlobalSettingsController : AppControllerBase
{
    /// <summary>Returns all managed global settings (values, types, audit trail).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => NewResult(await Mediator.Send(new GetGlobalSettingsQuery()));

    /// <summary>
    /// Updates a single managed global setting. Validates key allowlist + type + range.
    /// Invalidates the in-memory cache so the new value is effective immediately.
    /// UpdatedBy is derived server-side from the JWT — not accepted from the request body.
    /// </summary>
    [HttpPut("{key}")]
    public async Task<IActionResult> Update([FromRoute] string key, [FromBody] UpdateGlobalSettingRequest body)
        => NewResult(await Mediator.Send(new UpdateGlobalSettingCommand
        {
            Key      = key,
            NewValue = body.NewValue,
        }));
}

/// <summary>Request body for <c>PUT /api/Admin/GlobalSettings/{key}</c>.</summary>
public sealed class UpdateGlobalSettingRequest
{
    public string NewValue { get; set; } = default!;
}
