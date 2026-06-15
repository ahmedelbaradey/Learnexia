using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.LoseHeart;
using Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.RegainHeartFromPractice;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Exceptions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IHeartService"/>.
/// Owns idempotency, lazy refill, profile load/upsert, heart mutation, and audit row staging
/// via <see cref="IGamificationRepository"/>. Transactions are owned by UnitOfWorkBehavior.
/// </summary>
internal sealed class HeartService : BaseResponseHandler, IHeartService
{
    private readonly IGamificationRepository _repo;
    private readonly IOptions<HeartsOptions> _heartsOptions;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public HeartService(
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

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> LoseHeartAsync(
        LoseHeartCommand request,
        CancellationToken ct = default)
    {
        try
        {
            var opts = _heartsOptions.Value;

            // ── 1. Out-of-order guard ─────────────────────────────────────────────────────────
            if (request.OccurredAtUtc.AddMinutes(opts.RefillIntervalMinutes) < _clock.UtcNow)
            {
                _logger.LogWarn(
                    $"P4-04: stale heart-loss event ignored (eventId={request.OriginEventId}, " +
                    $"occurredAt={request.OccurredAtUtc:O}, now={_clock.UtcNow:O}).");
                return Success(Unit.Value);
            }

            // ── 2. Idempotency pre-check ──────────────────────────────────────────────────────
            bool alreadyRecorded = await _repo.HasHeartLossAsync(request.OriginEventId, ct);

            if (alreadyRecorded)
            {
                _logger.LogInfo(
                    $"P4-04: duplicate HeartLoss event {request.OriginEventId} " +
                    $"for student {request.StudentId} — already recorded, skipping.");
                return Success(Unit.Value);
            }

            // ── 3. Row-lock ───────────────────────────────────────────────────────────────────
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            // ── 4. Load profile ───────────────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct);

            bool isNewProfile = profile is null;
            profile ??= StudentXpProfile.CreateFor(request.StudentId);
            _repo.UpsertXpProfile(profile);

            // ── 5. Lose heart (lazy-refill FIRST is internal to LoseHeart()) ──────────────────
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

            await _repo.AddHeartLossAsync(loss, ct);

            _logger.LogInfo(
                $"P4-04: HeartLost (studentId={request.StudentId}, " +
                $"heartsAfter={profile.Hearts}, depleted={profile.InPracticeMode}).");

            return Success(Unit.Value);
        }
        catch (GamificationUniqueConstraintException ucEx)
            when (ucEx.ConstraintHint.Contains("UX_HeartLosses_OriginEventId")
                  || ucEx.ConstraintHint.Contains("23505"))
        {
            _logger.LogInfo(
                $"P4-04: unique-constraint violation on HeartLosses — concurrent duplicate delivery " +
                $"for student {request.StudentId} (eventId={request.OriginEventId}), treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-04: Unexpected error in HeartService.LoseHeartAsync " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> RegainHeartFromPracticeAsync(
        RegainHeartFromPracticeCommand request,
        CancellationToken ct = default)
    {
        try
        {
            var opts = _heartsOptions.Value;

            // ── 1. Row-lock ───────────────────────────────────────────────────────────────────
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            // ── 2. Load profile ───────────────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct);

            if (profile is null)
            {
                // Brand-new student — no profile row yet, Hearts = 5 by default. Not in Practice Mode.
                return Success(Unit.Value);
            }

            // Time-based refill before the practice-mode check.
            profile.RefreshHeartsAgainst(_clock.UtcNow, opts.Cap, opts.RefillIntervalMinutes);

            // ── 3. Guard: only regen while at 0 ──────────────────────────────────────────────
            if (profile.Hearts > 0)
            {
                _logger.LogInfo(
                    $"P4-04: regen no-op — student not in Practice Mode " +
                    $"(studentId={request.StudentId}, heartsNow={profile.Hearts}).");
                return Success(Unit.Value);
            }

            // ── 4. Restore +1 heart ───────────────────────────────────────────────────────────
            _repo.UpsertXpProfile(profile);
            profile.RegainHeartFromPractice(opts.Cap);

            _logger.LogInfo(
                $"P4-04: heart regenerated from practice (studentId={request.StudentId}, heartsAfter=1).");

            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-04: Unexpected error in HeartService.RegainHeartFromPracticeAsync " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}
