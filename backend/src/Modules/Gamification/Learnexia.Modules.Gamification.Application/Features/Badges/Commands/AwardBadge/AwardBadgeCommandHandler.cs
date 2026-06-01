using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Common;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Application.Features.Badges.Commands.AwardBadge;

/// <summary>
/// Handles <see cref="AwardBadgeCommand"/> — awards a badge to a student (P4-05, FR-GM-4).
///
/// Steps (inside UnitOfWorkBehavior transaction):
///   1. Load the BadgeDefinition catalog row. If not found, log warning and short-circuit (defensive).
///   2. Row-lock on profile to serialize concurrent badge awards for the same student.
///   3. Load or create the StudentXpProfile.
///   4. Idempotency pre-check: if the student already holds a badge for this definition, skip.
///   5. Recompute new level from (profile.TotalXp + definition.RewardXp).
///   6. Domain mutation: profile.RecordBadgeEarned — applies XP, updates level, raises
///      BadgeEarnedDomainEvent (+ StudentLeveledUpDomainEvent if level crossed). Events are
///      dispatched AFTER commit by UnitOfWorkBehavior (ADR 0002 §2).
///   7. Persist the StudentBadge ledger row.
///   8. Persist the XpAward row (forensics + XP ledger, reason = BadgeEarned).
///   9. DbUpdateException narrowed to unique-constraint violation → idempotency race, return Success.
/// </summary>
public sealed class AwardBadgeCommandHandler
    : BaseResponseHandler, ICommandHandler<AwardBadgeCommand, BaseResponse<Unit>>
{
    private readonly IGamificationRepository _repo;
    private readonly ILoggerManager _logger;

    public AwardBadgeCommandHandler(IGamificationRepository repo, ILoggerManager logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(AwardBadgeCommand request, CancellationToken ct)
    {
        try
        {
            // ── 1. Load the BadgeDefinition catalog row (read-only) ──────────────────────────
            var definitions = await _repo.GetAllBadgeDefinitionsAsync(ct);
            var definition = definitions.FirstOrDefault(d => d.Id == request.BadgeDefinitionId);
            if (definition is null)
            {
                _logger.LogWarn(
                    $"P4-05: AwardBadgeCommand for unknown definitionId={request.BadgeDefinitionId}, " +
                    $"studentId={request.StudentId} — skipping.");
                return Success(Unit.Value);
            }

            // ── 2. Row-lock on profile to serialize concurrent badge awards ───────────────────
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            // ── 3. Load or create profile ─────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct)
                          ?? StudentXpProfile.CreateFor(request.StudentId);
            _repo.UpsertXpProfile(profile);

            // ── 4. Idempotency pre-check ──────────────────────────────────────────────────────
            // Note: profile.Id may be 0 for a brand-new profile in the same UoW — the unique
            // index is the backstop. Pre-check only runs when profile.Id > 0 (existing profile).
            if (profile.Id > 0)
            {
                bool already = await _repo.HasBadgeAsync(profile.Id, request.BadgeDefinitionId, ct);
                if (already)
                {
                    _logger.LogInfo(
                        $"P4-05: duplicate badge award {definition.Code} for student {request.StudentId} — skipping.");
                    return Success(Unit.Value);
                }
            }

            // ── 5. Recompute level (XP bonus may push past a threshold) ─────────────────────
            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + definition.RewardXp);

            // ── 6. Domain mutation — applies XP, updates level, raises events ─────────────────
            // BadgeEarnedDomainEvent (and optionally StudentLeveledUpDomainEvent if level crossed)
            // are enqueued on the aggregate; UnitOfWorkBehavior dispatches them AFTER commit.
            profile.RecordBadgeEarned(
                badgeDefinitionId: definition.Id,
                code: definition.Code,
                rarity: definition.Rarity,
                rewardXp: definition.RewardXp,
                newLevel: newLevel,
                awardedAtUtc: request.AwardedAtUtc);

            // ── 7. Persist the StudentBadge ledger row ────────────────────────────────────────
            // Attach the definition to the DbContext as Unchanged BEFORE creating the StudentBadge.
            // GetAllBadgeDefinitionsAsync uses AsNoTracking, so the entity is detached.
            // If we pass it as a navigation property to StudentBadge.Create without attaching first,
            // EF Core will treat it as a new entity and attempt a duplicate INSERT (PK violation).
            _repo.AttachBadgeDefinition(definition);
            var ledger = StudentBadge.Create(
                profile: profile,
                definition: definition,
                originEventId: request.OriginEventId,
                originEventType: request.OriginEventType,
                awardedAtUtc: request.AwardedAtUtc);
            await _repo.AddStudentBadgeAsync(ledger, ct);

            // ── 8. Persist the XpAward row (forensics + XP ledger) ───────────────────────────
            // Uses a fresh Guid so UX_XpAwards_OriginEventId_Reason is always unique per award.
            var xpAward = XpAward.Create(
                profile: profile,
                reason: XpReason.BadgeEarned,
                xpAmount: definition.RewardXp,
                originEventId: Guid.NewGuid(),
                occurredAtUtc: request.AwardedAtUtc,
                lessonId: null,
                skillId: null);
            await _repo.AddXpAwardAsync(xpAward, ct);

            _logger.LogInfo(
                $"P4-05: badge awarded {definition.Code} (rarity={definition.Rarity}, +{definition.RewardXp} XP) " +
                $"to studentId={request.StudentId}, eventId={request.OriginEventId}.");

            return Success(Unit.Value);
        }
        catch (DbUpdateException dbEx)
            when (dbEx.InnerException?.Message?.Contains("UX_StudentBadges_StudentXpProfileId_BadgeDefinitionId") == true
                  || dbEx.InnerException?.Message?.Contains("UX_XpAwards_OriginEventId_Reason") == true
                  || dbEx.InnerException?.Message?.Contains("23505") == true)
        {
            // Idempotency race: concurrent duplicate delivery. The unique index won; treat as already-applied.
            _logger.LogInfo(
                $"P4-05: unique-constraint violation on badge award for studentId={request.StudentId} " +
                $"(eventId={request.OriginEventId}, definitionId={request.BadgeDefinitionId}) — already applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-05: Unexpected error in AwardBadgeCommandHandler " +
                $"(studentId={request.StudentId}, definitionId={request.BadgeDefinitionId}, " +
                $"eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}
