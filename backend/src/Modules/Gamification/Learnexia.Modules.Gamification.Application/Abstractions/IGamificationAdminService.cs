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
using Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;
using Learnexia.Modules.Gamification.Application.Features.TimedEvents.Queries.ListTimedEvents;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Admin workflow service — badge definitions, mission definitions, timed events,
/// streak freeze grants, and league tier overrides (P7-13).
/// All write methods stage changes only; UnitOfWorkBehavior commits.
/// Read methods are READ-ONLY.
/// </summary>
public interface IGamificationAdminService
{
    // ── Badge definition admin ──────────────────────────────────────────────────────────────────

    Task<BaseResponse<int>> CreateBadgeDefinitionAsync(
        CreateBadgeDefinitionCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<bool>> UpdateBadgeDefinitionAsync(
        UpdateBadgeDefinitionCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<bool>> SetBadgeDefinitionActiveAsync(
        SetBadgeDefinitionActiveCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<List<BadgeDefinitionDto>>> GetAdminBadgeDefinitionsAsync(
        CancellationToken ct = default);

    // ── Mission definition admin ────────────────────────────────────────────────────────────────

    Task<BaseResponse<int>> CreateMissionDefinitionAsync(
        CreateMissionDefinitionCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<bool>> UpdateMissionDefinitionAsync(
        UpdateMissionDefinitionCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<bool>> SetMissionDefinitionActiveAsync(
        SetMissionDefinitionActiveCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<List<MissionDefinitionDto>>> GetAdminMissionDefinitionsAsync(
        CancellationToken ct = default);

    // ── Timed event admin ───────────────────────────────────────────────────────────────────────

    Task<BaseResponse<int>> CreateTimedEventAsync(
        CreateTimedEventCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<bool>> UpdateTimedEventAsync(
        UpdateTimedEventCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<bool>> ActivateTimedEventAsync(
        ActivateTimedEventCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<bool>> ExpireTimedEventAsync(
        ExpireTimedEventCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<IReadOnlyList<TimedEventListItemDto>>> ListTimedEventsAsync(
        CancellationToken ct = default);

    // ── Profile admin ───────────────────────────────────────────────────────────────────────────

    Task<BaseResponse<bool>> GrantStreakFreezeAsync(
        GrantStreakFreezeCommand command,
        CancellationToken ct = default);

    Task<BaseResponse<bool>> OverrideLeagueTierAsync(
        OverrideLeagueTierCommand command,
        CancellationToken ct = default);
}
