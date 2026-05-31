using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.LoseHeart;

/// <summary>
/// Handles <see cref="LoseHeartCommand"/> — decrements Hearts by 1 for a wrong answer (P4-04).
///
/// Steps (inside UnitOfWorkBehavior transaction):
///   1. Out-of-order guard: if event is older than one full refill interval behind the last refill
///      timestamp, log warning and short-circuit (stale event replay defense — D10).
///   2. Idempotency pre-check: skip if HeartLoss row already exists for this OriginEventId.
///   3. Row-lock on the profile row (prevent concurrent loss race for the same student).
///   4. Load or create profile.
///   5. Lazy-refill FIRST (LoseHeart() calls LazyHeartRefiller internally). A student idle for
///      30+ min sees hearts restored BEFORE the decrement — avoids permanent Practice Mode lock.
///   6. If already in Practice Mode after refill: log + write audit row (wrong answer happened),
///      return Success (no further heart loss — Hearts stays at 0 per D3).
///   7. profile.LoseHeart() — decrements Hearts, resets refill clock, raises HeartsDepletedDomainEvent
///      when transitioning to 0.
///   8. Append HeartLoss audit row. Upsert profile.
///   9. DbUpdateException catch narrowed to UX_HeartLosses_OriginEventId constraint violation →
///      idempotency race, return Success.
/// </summary>
public class LoseHeartCommandHandler
    : BaseResponseHandler, ICommandHandler<LoseHeartCommand, BaseResponse<Unit>>
{
    private readonly IGamificationRepository _repo;
    private readonly IOptions<HeartsOptions> _heartsOptions;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public LoseHeartCommandHandler(
        IGamificationRepository repo,
        IOptions<HeartsOptions> heartsOptions,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _repo = repo;
        _heartsOptions = heartsOptions;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        LoseHeartCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var opts = _heartsOptions.Value;

            // ── 1. Out-of-order guard ─────────────────────────────────────────────────────────
            // If the event is older than one refill interval behind now, treat as stale replay.
            // This is a lightweight guard; the idempotency unique index is the primary defense.
            if (request.OccurredAtUtc.AddMinutes(opts.RefillIntervalMinutes) < _clock.UtcNow)
            {
                _logger.LogWarn(
                    $"P4-04: stale heart-loss event ignored (eventId={request.OriginEventId}, " +
                    $"occurredAt={request.OccurredAtUtc:O}, now={_clock.UtcNow:O}).");
                return Success(Unit.Value);
            }

            // ── 2. Idempotency pre-check ──────────────────────────────────────────────────────
            bool alreadyRecorded = await _repo.HasHeartLossAsync(request.OriginEventId, cancellationToken);

            if (alreadyRecorded)
            {
                _logger.LogInfo(
                    $"P4-04: duplicate HeartLoss event {request.OriginEventId} " +
                    $"for student {request.StudentId} — already recorded, skipping.");
                return Success(Unit.Value);
            }

            // ── 3. Row-lock ───────────────────────────────────────────────────────────────────
            await _repo.AcquireProfileLockAsync(request.StudentId, cancellationToken);

            // ── 4. Load profile (lazy creation: brand-new students start at Hearts=5 by default;
            //       no profile row yet means no losses have been recorded — we create the profile
            //       on first loss so hearts are properly tracked from the start) ─────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, cancellationToken);

            // Brand-new student with no profile yet — create one so we can track hearts.
            // This differs from the XP path where a wrong answer creates no profile (P4-02 design):
            // a wrong answer IS a hearts event, so the profile must exist to persist the loss.
            bool isNewProfile = profile is null;
            profile ??= StudentXpProfile.CreateFor(request.StudentId);
            _repo.UpsertXpProfile(profile);

            // ── 5. Lose heart (lazy-refill FIRST is internal to LoseHeart()) ──────────────────
            // When already in Practice Mode after refill, LoseHeart() is a no-op for the decrement
            // (Math.Max 0) but we still write the audit row (D7 — wrong answer happened).
            var wasInPracticeMode = profile.InPracticeMode;

            profile.LoseHeart(request.OccurredAtUtc, opts.Cap, opts.RefillIntervalMinutes);

            if (wasInPracticeMode)
            {
                _logger.LogInfo(
                    $"P4-04: already in Practice Mode — heart count unchanged; audit row written " +
                    $"(studentId={request.StudentId}, heartsAfter={profile.Hearts}).");
            }

            // ── 6. Append audit row ───────────────────────────────────────────────────────────
            var loss = HeartLoss.Create(
                profile: profile,
                originEventId: request.OriginEventId,
                heartsAfter: profile.Hearts,
                occurredAtUtc: request.OccurredAtUtc,
                lessonId: request.LessonId,
                skillId: request.SkillId);

            await _repo.AddHeartLossAsync(loss, cancellationToken);

            // ── 7. Log ────────────────────────────────────────────────────────────────────────
            _logger.LogInfo(
                $"P4-04: HeartLost (studentId={request.StudentId}, " +
                $"heartsAfter={profile.Hearts}, depleted={profile.InPracticeMode}).");

            return Success(Unit.Value);
        }
        catch (DbUpdateException dbEx)
            when (dbEx.InnerException?.Message?.Contains("UX_HeartLosses_OriginEventId") == true
                  || dbEx.InnerException?.Message?.Contains("23505") == true)
        {
            // Narrowed: only the unique-constraint violation on UX_HeartLosses_OriginEventId is treated
            // as an idempotency race (concurrent duplicate delivery). Any other DbUpdateException
            // falls through to the outer catch and returns ServerError.
            _logger.LogInfo(
                $"P4-04: unique-constraint violation on HeartLosses — concurrent duplicate delivery " +
                $"for student {request.StudentId} (eventId={request.OriginEventId}), treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-04: Unexpected error in LoseHeartCommandHandler " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}
