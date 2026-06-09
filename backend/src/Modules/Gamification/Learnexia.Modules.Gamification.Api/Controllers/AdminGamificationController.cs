using Learnexia.Modules.Gamification.Api.Bases;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.ActivateTimedEvent;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateBadgeDefinition;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateMissionDefinition;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateTimedEvent;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.ExpireTimedEvent;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.GrantStreakFreeze;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.OverrideLeagueTier;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.SetBadgeDefinitionActive;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.SetMissionDefinitionActive;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateBadgeDefinition;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateMissionDefinition;
using Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateTimedEvent;
using Learnexia.Modules.Gamification.Application.Features.Admin.Queries.GetAdminBadgeDefinitions;
using Learnexia.Modules.Gamification.Application.Features.Admin.Queries.GetAdminMissionDefinitions;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Gamification.Api.Controllers;

/// <summary>
/// Admin write surface for the Gamification module (P7-13).
/// All endpoints require <c>AdminOnly</c> policy.
/// Route: <c>api/Admin/Gamification</c>
/// </summary>
[Route("api/Admin/Gamification")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminGamificationController : AppControllerBase
{
    // ---------------------------------------------------------------------------
    // League tier override
    // ---------------------------------------------------------------------------

    /// <summary>
    /// POST api/Admin/Gamification/children/{childId}/league-tier
    /// Overrides the student's current league tier. Requires confirm = true.
    /// </summary>
    [HttpPost("children/{childId:int}/league-tier")]
    public async Task<IActionResult> OverrideLeagueTier(
        int childId,
        [FromBody] OverrideLeagueTierBody body,
        CancellationToken ct)
        => NewResult(await Mediator.Send(
            new OverrideLeagueTierCommand(childId, body.NewTier, body.Reason, body.Confirm), ct));

    // ---------------------------------------------------------------------------
    // Streak freeze grant
    // ---------------------------------------------------------------------------

    /// <summary>
    /// POST api/Admin/Gamification/children/{childId}/streak-freeze
    /// Grants up to Count streak freezes (clamped at MaxFreezes). Requires confirm = true.
    /// </summary>
    [HttpPost("children/{childId:int}/streak-freeze")]
    public async Task<IActionResult> GrantStreakFreeze(
        int childId,
        [FromBody] GrantStreakFreezeBody body,
        CancellationToken ct)
        => NewResult(await Mediator.Send(
            new GrantStreakFreezeCommand(childId, body.Count, body.Reason, body.Confirm), ct));

    // ---------------------------------------------------------------------------
    // Badge catalog
    // ---------------------------------------------------------------------------

    /// <summary>GET api/Admin/Gamification/Badges — list all (active + inactive).</summary>
    [HttpGet("Badges")]
    public async Task<IActionResult> ListBadges(CancellationToken ct)
        => NewResult(await Mediator.Send(new GetAdminBadgeDefinitionsQuery(), ct));

    /// <summary>POST api/Admin/Gamification/Badges — create a new badge definition.</summary>
    [HttpPost("Badges")]
    public async Task<IActionResult> CreateBadge(
        [FromBody] CreateBadgeDefinitionCommand command,
        CancellationToken ct)
        => NewResult(await Mediator.Send(command, ct));

    /// <summary>PUT api/Admin/Gamification/Badges/{id} — update mutable fields.</summary>
    [HttpPut("Badges/{id:int}")]
    public async Task<IActionResult> UpdateBadge(
        int id,
        [FromBody] UpdateBadgeDefinitionBody body,
        CancellationToken ct)
        => NewResult(await Mediator.Send(
            new UpdateBadgeDefinitionCommand(
                id,
                body.Name,
                body.Description,
                body.IconKey,
                body.Rarity,
                body.SortOrder,
                body.TriggerType,
                body.Threshold,
                body.RewardXp), ct));

    /// <summary>PATCH api/Admin/Gamification/Badges/{id}/active — activate or deactivate.</summary>
    [HttpPatch("Badges/{id:int}/active")]
    public async Task<IActionResult> SetBadgeActive(
        int id,
        [FromBody] SetActiveBody body,
        CancellationToken ct)
        => NewResult(await Mediator.Send(new SetBadgeDefinitionActiveCommand(id, body.IsActive), ct));

    // ---------------------------------------------------------------------------
    // Mission catalog
    // ---------------------------------------------------------------------------

    /// <summary>GET api/Admin/Gamification/Missions — list all (active + inactive).</summary>
    [HttpGet("Missions")]
    public async Task<IActionResult> ListMissions(CancellationToken ct)
        => NewResult(await Mediator.Send(new GetAdminMissionDefinitionsQuery(), ct));

    /// <summary>POST api/Admin/Gamification/Missions — create a new mission definition.</summary>
    [HttpPost("Missions")]
    public async Task<IActionResult> CreateMission(
        [FromBody] CreateMissionDefinitionCommand command,
        CancellationToken ct)
        => NewResult(await Mediator.Send(command, ct));

    /// <summary>PUT api/Admin/Gamification/Missions/{id} — update mutable fields.</summary>
    [HttpPut("Missions/{id:int}")]
    public async Task<IActionResult> UpdateMission(
        int id,
        [FromBody] UpdateMissionDefinitionBody body,
        CancellationToken ct)
        => NewResult(await Mediator.Send(
            new UpdateMissionDefinitionCommand(
                id,
                body.IconKey,
                body.TitleKey,
                body.Cadence,
                body.TargetType,
                body.Target,
                body.RewardXp,
                body.SortOrder), ct));

    /// <summary>PATCH api/Admin/Gamification/Missions/{id}/active — activate or deactivate.</summary>
    [HttpPatch("Missions/{id:int}/active")]
    public async Task<IActionResult> SetMissionActive(
        int id,
        [FromBody] SetActiveBody body,
        CancellationToken ct)
        => NewResult(await Mediator.Send(new SetMissionDefinitionActiveCommand(id, body.IsActive), ct));

    // ---------------------------------------------------------------------------
    // Timed events
    // ---------------------------------------------------------------------------

    /// <summary>POST api/Admin/Gamification/TimedEvents — create a new timed event.</summary>
    [HttpPost("TimedEvents")]
    public async Task<IActionResult> CreateTimedEvent(
        [FromBody] CreateTimedEventCommand command,
        CancellationToken ct)
        => NewResult(await Mediator.Send(command, ct));

    /// <summary>PUT api/Admin/Gamification/TimedEvents/{id} — update mutable fields.</summary>
    [HttpPut("TimedEvents/{id:int}")]
    public async Task<IActionResult> UpdateTimedEvent(
        int id,
        [FromBody] UpdateTimedEventBody body,
        CancellationToken ct)
        => NewResult(await Mediator.Send(
            new UpdateTimedEventCommand(
                id,
                body.NameEn,
                body.NameAr,
                body.DescriptionEn,
                body.DescriptionAr,
                body.StartUtc,
                body.EndUtc,
                body.Multiplier,
                body.Scope), ct));

    /// <summary>POST api/Admin/Gamification/TimedEvents/{id}/activate — manually activate.</summary>
    [HttpPost("TimedEvents/{id:int}/activate")]
    public async Task<IActionResult> ActivateTimedEvent(int id, CancellationToken ct)
        => NewResult(await Mediator.Send(new ActivateTimedEventCommand(id), ct));

    /// <summary>POST api/Admin/Gamification/TimedEvents/{id}/expire — manually expire.</summary>
    [HttpPost("TimedEvents/{id:int}/expire")]
    public async Task<IActionResult> ExpireTimedEvent(int id, CancellationToken ct)
        => NewResult(await Mediator.Send(new ExpireTimedEventCommand(id), ct));
}

// ---------------------------------------------------------------------------
// Request body records (thin shells — route id injected separately)
// ---------------------------------------------------------------------------

/// <summary>Body for <c>OverrideLeagueTierCommand</c> (childId comes from route).</summary>
public sealed record OverrideLeagueTierBody(
    Learnexia.Modules.Gamification.Domain.Enums.LeagueTier NewTier,
    string Reason,
    bool Confirm);

/// <summary>Body for <c>GrantStreakFreezeCommand</c> (childId comes from route).</summary>
public sealed record GrantStreakFreezeBody(int Count, string Reason, bool Confirm);

/// <summary>Body for <c>UpdateBadgeDefinitionCommand</c> (id comes from route).</summary>
public sealed record UpdateBadgeDefinitionBody(
    string Name,
    string Description,
    string IconKey,
    Learnexia.Modules.Gamification.Domain.Enums.BadgeRarity Rarity,
    int SortOrder,
    Learnexia.Modules.Gamification.Domain.Enums.BadgeTriggerType TriggerType,
    int? Threshold,
    int RewardXp);

/// <summary>Body for <c>UpdateMissionDefinitionCommand</c> (id comes from route).</summary>
public sealed record UpdateMissionDefinitionBody(
    string IconKey,
    string TitleKey,
    Learnexia.Modules.Gamification.Domain.Enums.MissionType Cadence,
    Learnexia.Modules.Gamification.Domain.Enums.MissionTargetType TargetType,
    int Target,
    int RewardXp,
    int SortOrder);

/// <summary>Generic activate/deactivate body shared by badge and mission set-active actions.</summary>
public sealed record SetActiveBody(bool IsActive);

/// <summary>Body for <c>UpdateTimedEventCommand</c> (id comes from route).</summary>
public sealed record UpdateTimedEventBody(
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    DateTime StartUtc,
    DateTime EndUtc,
    decimal Multiplier,
    Learnexia.Modules.Gamification.Domain.Enums.TimedEventScope Scope);
