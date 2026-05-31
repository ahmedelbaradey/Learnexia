using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.RegainHeartFromPractice;

/// <summary>
/// Handles <see cref="RegainHeartFromPracticeCommand"/> — restores +1 heart on correct answer
/// in Practice Mode (P4-04, D2 / Q3 = yes).
///
/// Steps (inside UnitOfWorkBehavior transaction):
///   1. Row-lock on the profile row.
///   2. Load or create profile. Apply time-based lazy refill first.
///   3. If NOT in Practice Mode (Hearts > 0): log + return Success (regen only fires at Hearts == 0).
///      Multiple redeliveries after first regen no-op here — bounded natural idempotency.
///   4. profile.RegainHeartFromPractice(cap): increments Hearts to 1, raises HeartsRefilledDomainEvent.
///   5. Upsert profile. UoW commits.
/// </summary>
public class RegainHeartFromPracticeCommandHandler
    : BaseResponseHandler, ICommandHandler<RegainHeartFromPracticeCommand, BaseResponse<Unit>>
{
    private readonly IGamificationRepository _repo;
    private readonly IOptions<HeartsOptions> _heartsOptions;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public RegainHeartFromPracticeCommandHandler(
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
        RegainHeartFromPracticeCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var opts = _heartsOptions.Value;

            // ── 1. Row-lock ───────────────────────────────────────────────────────────────────
            await _repo.AcquireProfileLockAsync(request.StudentId, cancellationToken);

            // ── 2. Load profile (no create-if-missing — if there's no profile, the student
            //       has never lost a heart and cannot be in Practice Mode) ────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, cancellationToken);

            if (profile is null)
            {
                // Brand-new student — no profile row yet, Hearts = 5 by default. Not in Practice Mode.
                return Success(Unit.Value);
            }

            // Time-based refill before the practice-mode check so a student who has ticked back up
            // through the timer is NOT inadvertently re-granted hearts via the practice path.
            profile.RefreshHeartsAgainst(_clock.UtcNow, opts.Cap, opts.RefillIntervalMinutes);

            // ── 3. Guard: only regen while at 0 ──────────────────────────────────────────────
            if (profile.Hearts > 0)
            {
                _logger.LogInfo(
                    $"P4-04: regen no-op — student not in Practice Mode " +
                    $"(studentId={request.StudentId}, heartsNow={profile.Hearts}).");
                return Success(Unit.Value);
            }

            // ── 4. Restore +1 heart (profile was loaded, needs to be tracked for save) ───────
            _repo.UpsertXpProfile(profile);
            profile.RegainHeartFromPractice(opts.Cap);

            // ── 5. Log ────────────────────────────────────────────────────────────────────────
            _logger.LogInfo(
                $"P4-04: heart regenerated from practice (studentId={request.StudentId}, heartsAfter=1).");

            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-04: Unexpected error in RegainHeartFromPracticeCommandHandler " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}
