using AutoMapper;
using Learnexia.Modules.Gamification.Application.Abstractions;
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
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IGamificationAdminService"/>.
/// All write methods stage changes via <see cref="IGamificationRepository"/>; UnitOfWorkBehavior commits.
/// </summary>
internal sealed class GamificationAdminService : BaseResponseHandler, IGamificationAdminService
{
    private readonly IGamificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GamificationAdminService(
        IGamificationRepository repository,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository  = repository;
        _currentUser = currentUser;
        _mapper      = mapper;
        _logger      = logger;
        _localizer   = localizer;
    }

    // ─────────────────────────────────────── Badge definition admin ─────────────────────────────

    public async Task<BaseResponse<int>> CreateBadgeDefinitionAsync(
        CreateBadgeDefinitionCommand request,
        CancellationToken ct = default)
    {
        try
        {
            if (await _repository.BadgeCodeExistsAsync(request.Code, ct))
                return BusinessValidation<int>(_localizer[SharedResourcesKey.GamificationBadgeCodeAlreadyExists]);

            var badge = BadgeDefinition.Create(
                request.Code,
                request.Name,
                request.Description,
                request.IconKey,
                request.Rarity,
                request.SortOrder,
                request.TriggerType,
                request.Threshold,
                request.RewardXp);

            await _repository.AddBadgeDefinitionAsync(badge, ct);

            badge.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.BadgeCreated,
                TargetEntityType: nameof(BadgeDefinition),
                TargetEntityId: badge.Id,
                Details: $"Code={request.Code}; Name={request.Name}"));

            return Created(badge.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in CreateBadgeDefinitionCommand");
            return ServerError<int>();
        }
    }

    public async Task<BaseResponse<bool>> UpdateBadgeDefinitionAsync(
        UpdateBadgeDefinitionCommand request,
        CancellationToken ct = default)
    {
        try
        {
            var badge = await _repository.GetBadgeDefinitionByIdAsync(request.Id, ct);
            if (badge is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationBadgeNotFound]);

            badge.AdminUpdate(
                request.Name,
                request.Description,
                request.IconKey,
                request.Rarity,
                request.SortOrder,
                request.TriggerType,
                request.Threshold,
                request.RewardXp);

            badge.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.BadgeUpdated,
                TargetEntityType: nameof(BadgeDefinition),
                TargetEntityId: badge.Id,
                Details: $"Code={badge.Code}; Name={request.Name}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateBadgeDefinitionCommand");
            return ServerError<bool>();
        }
    }

    public async Task<BaseResponse<bool>> SetBadgeDefinitionActiveAsync(
        SetBadgeDefinitionActiveCommand request,
        CancellationToken ct = default)
    {
        try
        {
            var badge = await _repository.GetBadgeDefinitionByIdAsync(request.Id, ct);
            if (badge is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationBadgeNotFound]);

            if (badge.IsActive == request.IsActive)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationBadgeAlreadyInRequestedState]);

            badge.SetActive(request.IsActive);

            var action = request.IsActive ? AdminActions.BadgeActivated : AdminActions.BadgeDeactivated;

            badge.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: action,
                TargetEntityType: nameof(BadgeDefinition),
                TargetEntityId: badge.Id,
                Details: $"Code={badge.Code}; IsActive={request.IsActive}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetBadgeDefinitionActiveCommand");
            return ServerError<bool>();
        }
    }

    public async Task<BaseResponse<List<BadgeDefinitionDto>>> GetAdminBadgeDefinitionsAsync(
        CancellationToken ct = default)
    {
        try
        {
            var definitions = await _repository.GetAllBadgeDefinitionsAdminAsync(ct);
            var dtos = _mapper.Map<List<BadgeDefinitionDto>>(definitions);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetAdminBadgeDefinitionsQuery");
            return ServerError<List<BadgeDefinitionDto>>();
        }
    }

    // ─────────────────────────────────────── Mission definition admin ────────────────────────────

    public async Task<BaseResponse<int>> CreateMissionDefinitionAsync(
        CreateMissionDefinitionCommand request,
        CancellationToken ct = default)
    {
        try
        {
            if (await _repository.MissionCodeExistsAsync(request.Code, ct))
                return BusinessValidation<int>(_localizer[SharedResourcesKey.GamificationMissionCodeAlreadyExists]);

            var mission = MissionDefinition.Create(
                request.Code,
                request.IconKey,
                request.TitleKey,
                request.Cadence,
                request.TargetType,
                request.Target,
                request.RewardXp,
                request.SortOrder);

            await _repository.AddMissionDefinitionAsync(mission, ct);

            mission.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.MissionCreated,
                TargetEntityType: nameof(MissionDefinition),
                TargetEntityId: mission.Id,
                Details: $"Code={request.Code}; Cadence={request.Cadence}"));

            return Created(mission.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in CreateMissionDefinitionCommand");
            return ServerError<int>();
        }
    }

    public async Task<BaseResponse<bool>> UpdateMissionDefinitionAsync(
        UpdateMissionDefinitionCommand request,
        CancellationToken ct = default)
    {
        try
        {
            var mission = await _repository.GetMissionDefinitionByIdAsync(request.Id, ct);
            if (mission is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationMissionNotFound]);

            mission.AdminUpdate(
                request.IconKey,
                request.TitleKey,
                request.Cadence,
                request.TargetType,
                request.Target,
                request.RewardXp,
                request.SortOrder);

            mission.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.MissionUpdated,
                TargetEntityType: nameof(MissionDefinition),
                TargetEntityId: mission.Id,
                Details: $"Code={mission.Code}; Cadence={request.Cadence}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateMissionDefinitionCommand");
            return ServerError<bool>();
        }
    }

    public async Task<BaseResponse<bool>> SetMissionDefinitionActiveAsync(
        SetMissionDefinitionActiveCommand request,
        CancellationToken ct = default)
    {
        try
        {
            var mission = await _repository.GetMissionDefinitionByIdAsync(request.Id, ct);
            if (mission is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationMissionNotFound]);

            if (mission.IsActive == request.IsActive)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationMissionAlreadyInRequestedState]);

            mission.SetActive(request.IsActive);

            var action = request.IsActive ? AdminActions.MissionActivated : AdminActions.MissionDeactivated;

            mission.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: action,
                TargetEntityType: nameof(MissionDefinition),
                TargetEntityId: mission.Id,
                Details: $"Code={mission.Code}; IsActive={request.IsActive}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in SetMissionDefinitionActiveCommand");
            return ServerError<bool>();
        }
    }

    public async Task<BaseResponse<List<MissionDefinitionDto>>> GetAdminMissionDefinitionsAsync(
        CancellationToken ct = default)
    {
        try
        {
            var definitions = await _repository.GetAllMissionDefinitionsAdminAsync(ct);
            var dtos = _mapper.Map<List<MissionDefinitionDto>>(definitions);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetAdminMissionDefinitionsQuery");
            return ServerError<List<MissionDefinitionDto>>();
        }
    }

    // ─────────────────────────────────────── Timed event admin ──────────────────────────────────

    public async Task<BaseResponse<int>> CreateTimedEventAsync(
        CreateTimedEventCommand request,
        CancellationToken ct = default)
    {
        try
        {
            if (await _repository.TimedEventCodeExistsAsync(request.Code, ct))
                return BusinessValidation<int>(_localizer[SharedResourcesKey.GamificationTimedEventCodeAlreadyExists]);

            var timedEvent = TimedEvent.Create(
                request.Code,
                request.NameEn,
                request.NameAr,
                request.DescriptionEn,
                request.DescriptionAr,
                request.StartUtc,
                request.EndUtc,
                request.Multiplier,
                request.Scope);

            await _repository.AddTimedEventAsync(timedEvent, ct);

            timedEvent.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.TimedEventCreated,
                TargetEntityType: nameof(TimedEvent),
                TargetEntityId: timedEvent.Id,
                Details: $"Code={request.Code}; Window={request.StartUtc:O}→{request.EndUtc:O}; Multiplier={request.Multiplier}"));

            return Created(timedEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in CreateTimedEventCommand");
            return ServerError<int>();
        }
    }

    public async Task<BaseResponse<bool>> UpdateTimedEventAsync(
        UpdateTimedEventCommand request,
        CancellationToken ct = default)
    {
        try
        {
            var timedEvent = await _repository.GetTimedEventByIdAsync(request.Id, ct);
            if (timedEvent is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationTimedEventNotFound]);

            timedEvent.AdminUpdate(
                request.NameEn,
                request.NameAr,
                request.DescriptionEn,
                request.DescriptionAr,
                request.StartUtc,
                request.EndUtc,
                request.Multiplier,
                request.Scope);

            timedEvent.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.TimedEventUpdated,
                TargetEntityType: nameof(TimedEvent),
                TargetEntityId: timedEvent.Id,
                Details: $"Code={timedEvent.Code}; Window={request.StartUtc:O}→{request.EndUtc:O}; Multiplier={request.Multiplier}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateTimedEventCommand");
            return ServerError<bool>();
        }
    }

    public async Task<BaseResponse<bool>> ActivateTimedEventAsync(
        ActivateTimedEventCommand request,
        CancellationToken ct = default)
    {
        try
        {
            var timedEvent = await _repository.GetTimedEventByIdAsync(request.Id, ct);
            if (timedEvent is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationTimedEventNotFound]);

            if (timedEvent.IsActive)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationTimedEventAlreadyActive]);

            timedEvent.Activate();

            timedEvent.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.TimedEventActivated,
                TargetEntityType: nameof(TimedEvent),
                TargetEntityId: timedEvent.Id,
                Details: $"Code={timedEvent.Code}; IsActive=true"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ActivateTimedEventCommand");
            return ServerError<bool>();
        }
    }

    public async Task<BaseResponse<bool>> ExpireTimedEventAsync(
        ExpireTimedEventCommand request,
        CancellationToken ct = default)
    {
        try
        {
            var timedEvent = await _repository.GetTimedEventByIdAsync(request.Id, ct);
            if (timedEvent is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationTimedEventNotFound]);

            if (!timedEvent.IsActive)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationTimedEventAlreadyInactive]);

            timedEvent.Deactivate();

            timedEvent.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.TimedEventExpired,
                TargetEntityType: nameof(TimedEvent),
                TargetEntityId: timedEvent.Id,
                Details: $"Code={timedEvent.Code}; IsActive=false"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ExpireTimedEventCommand");
            return ServerError<bool>();
        }
    }

    public async Task<BaseResponse<IReadOnlyList<TimedEventListItemDto>>> ListTimedEventsAsync(
        CancellationToken ct = default)
    {
        try
        {
            var rows = await _repository.GetAllTimedEventsAsync(ct);
            var dtos = rows
                .Select(e => new TimedEventListItemDto(
                    Id:         e.Id,
                    Code:       e.Code,
                    NameEn:     e.NameEn,
                    NameAr:     e.NameAr,
                    Multiplier: e.Multiplier,
                    StartUtc:   e.StartUtc,
                    EndUtc:     e.EndUtc,
                    IsActive:   e.IsActive,
                    Scope:      e.Scope.ToString()))
                .ToList();

            return Success((IReadOnlyList<TimedEventListItemDto>)dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P4-11-B4: Error in ListTimedEventsQuery.");
            return ServerError<IReadOnlyList<TimedEventListItemDto>>();
        }
    }

    // ─────────────────────────────────────── Profile admin ──────────────────────────────────────

    public async Task<BaseResponse<bool>> GrantStreakFreezeAsync(
        GrantStreakFreezeCommand request,
        CancellationToken ct = default)
    {
        try
        {
            if (!request.Confirm)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationConfirmRequired]);

            var profile = await _repository.GetProfileByStudentIdTrackedAsync(request.ChildId, ct);

            if (profile is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationProfileNotFound]);

            int balanceBefore = profile.FreezeBalance;
            int granted = profile.GrantFreezes(request.Count, DateTime.UtcNow);

            if (granted == 0)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationFreezeBalanceAtMax]);

            profile.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.GamificationStreakFreezeGranted,
                TargetEntityType: nameof(StudentXpProfile),
                TargetEntityId: profile.Id,
                Details: $"ChildId={request.ChildId}; FreezeBalance: {balanceBefore}→{profile.FreezeBalance}; count={granted}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GrantStreakFreezeCommand");
            return ServerError<bool>();
        }
    }

    public async Task<BaseResponse<bool>> OverrideLeagueTierAsync(
        OverrideLeagueTierCommand request,
        CancellationToken ct = default)
    {
        try
        {
            if (!request.Confirm)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationConfirmRequired]);

            var profile = await _repository.GetProfileByStudentIdTrackedAsync(request.ChildId, ct);

            if (profile is null)
                return NotFound<bool>(_localizer[SharedResourcesKey.GamificationProfileNotFound]);

            if (profile.CurrentTier == request.NewTier)
                return BadRequest<bool>(_localizer[SharedResourcesKey.GamificationLeagueTierNoOp]);

            var oldTier = profile.CurrentTier;
            profile.UpdateTier(request.NewTier);

            profile.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.GamificationLeagueTierOverridden,
                TargetEntityType: nameof(StudentXpProfile),
                TargetEntityId: profile.Id,
                Details: $"ChildId={request.ChildId}; Tier: {oldTier}→{request.NewTier}"));

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in OverrideLeagueTierCommand");
            return ServerError<bool>();
        }
    }
}
