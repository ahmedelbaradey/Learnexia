using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Common;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Application.Features.Events.Boost;
using Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardAnswerSubmittedXp;
using Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardLessonCompletedXp;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Modules.Gamification.Domain.Exceptions;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IXpService"/>.
/// Owns idempotency, practice-mode gate, profile load/upsert, XP math, row staging, and
/// (P4-12) timed-event participation accrual via <see cref="IGamificationRepository"/>.
/// Transactions are owned by UnitOfWorkBehavior.
/// </summary>
internal sealed class XpService : BaseResponseHandler, IXpService
{
    private readonly IGamificationRepository _repo;
    private readonly IOptions<HeartsOptions> _heartsOptions;
    private readonly IOptions<TimedEventParticipationOptions> _participationOptions;
    private readonly IActiveTimedEventsQuery _activeEvents;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;
    private readonly IXpBoostCalculator _boostCalc;

    public XpService(
        IGamificationRepository repo,
        IOptions<HeartsOptions> heartsOptions,
        IOptions<TimedEventParticipationOptions> participationOptions,
        IActiveTimedEventsQuery activeEvents,
        ISystemClock clock,
        ILoggerManager logger,
        IXpBoostCalculator boostCalc)
    {
        _repo = repo;
        _heartsOptions = heartsOptions;
        _participationOptions = participationOptions;
        _activeEvents = activeEvents;
        _clock = clock;
        _logger = logger;
        _boostCalc = boostCalc;
    }

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> AwardAnswerSubmittedXpAsync(
        AwardAnswerSubmittedXpCommand request,
        CancellationToken ct = default)
    {
        try
        {
            // Belt-and-suspenders: skip if this was a wrong answer.
            if (request.CorrectAnswerCount == 0)
                return Success(Unit.Value);

            // AC4 idempotency pre-check.
            bool alreadyAwarded = await _repo.HasXpAwardAsync(
                request.OriginEventId, XpReason.CorrectAnswer, ct);

            if (alreadyAwarded)
            {
                _logger.LogInfo(
                    $"P4-02: duplicate AnswerSubmitted event {request.OriginEventId} " +
                    $"for student {request.StudentId} — CorrectAnswer award already exists, skipping.");
                return Success(Unit.Value);
            }

            // Row-lock on profile row before read to prevent concurrent XP race (Q7).
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct)
                          ?? StudentXpProfile.CreateFor(request.StudentId);

            // Ensure the profile is tracked (new or existing) so EF can resolve FK at SaveChanges.
            _repo.UpsertXpProfile(profile);

            // ── P4-04 Practice-Mode gate ──────────────────────────────────────────────────────
            // Lazy-refill first so a student whose hearts have ticked back up is not falsely gated.
            var opts = _heartsOptions.Value;
            profile.RefreshHeartsAgainst(_clock.UtcNow, opts.Cap, opts.RefillIntervalMinutes);

            if (profile.InPracticeMode)
            {
                _logger.LogInfo(
                    $"P4-04: Practice Mode — AnswerSubmittedXP suspended (studentId={request.StudentId}).");
                return Success(Unit.Value);
            }

            int amount = await _boostCalc.GetEffectiveAmountAsync(
                GamificationConstants.XpRewards.CorrectAnswer,
                XpReason.CorrectAnswer,
                request.OccurredAtUtc,
                ct);

            var award = XpAward.Create(
                profile: profile,
                reason: XpReason.CorrectAnswer,
                xpAmount: amount,
                originEventId: request.OriginEventId,
                occurredAtUtc: request.OccurredAtUtc,
                lessonId: request.LessonId,
                skillId: request.SkillId);

            await _repo.AddXpAwardAsync(award, ct);

            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + amount);
            profile.ApplyAward(amount, newLevel, XpReason.CorrectAnswer, request.OccurredAtUtc);

            _logger.LogInfo(
                $"P4-02: CorrectAnswer +{amount} XP awarded to student {request.StudentId} " +
                $"(eventId={request.OriginEventId}, lessonId={request.LessonId}).");

            // ── P4-12: Timed-event participation accrual ─────────────────────────────────────
            // XP idempotency guard already ran above — if we reach this line, this is NOT a
            // duplicate delivery, so accrual is safe. Active events are re-fetched from the
            // cached IActiveTimedEventsQuery (cheap Redis-backed call; already warmed by _boostCalc).
            await AccrueTimedEventParticipationAsync(
                profile, request.OccurredAtUtc, ct);

            return Success(Unit.Value);
        }
        catch (GamificationUniqueConstraintException ucEx)
        {
            // Defense-in-depth: unique-constraint violation from a concurrent duplicate delivery.
            _logger.LogError(ucEx,
                $"P4-02: unique-constraint violation awarding CorrectAnswer XP for student {request.StudentId} " +
                $"(eventId={request.OriginEventId}) — likely idempotency race, treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-02: Unexpected error in XpService.AwardAnswerSubmittedXpAsync " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> AwardLessonCompletedXpAsync(
        AwardLessonCompletedXpCommand request,
        CancellationToken ct = default)
    {
        try
        {
            // ── LessonComplete award ─────────────────────────────────────────────────────────
            bool lessonAlreadyAwarded = await _repo.HasXpAwardAsync(
                request.OriginEventId, XpReason.LessonCompleted, ct);

            if (lessonAlreadyAwarded)
            {
                _logger.LogInfo(
                    $"P4-02: duplicate LessonCompleted event {request.OriginEventId} " +
                    $"for student {request.StudentId} — LessonComplete award already exists, skipping.");
                return Success(Unit.Value);
            }

            // Row-lock on profile row before read to prevent concurrent XP race (Q7).
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct)
                          ?? StudentXpProfile.CreateFor(request.StudentId);

            // Ensure the profile is tracked (new or existing) so EF can resolve FK at SaveChanges.
            _repo.UpsertXpProfile(profile);

            // ── P4-04 Practice-Mode gate ──────────────────────────────────────────────────────
            // Lazy-refill first so a student whose hearts have ticked back up is not falsely gated.
            var opts = _heartsOptions.Value;
            profile.RefreshHeartsAgainst(_clock.UtcNow, opts.Cap, opts.RefillIntervalMinutes);

            if (profile.InPracticeMode)
            {
                _logger.LogInfo(
                    $"P4-04: Practice Mode — LessonCompletedXP suspended (studentId={request.StudentId}).");
                return Success(Unit.Value);
            }

            int lessonAmount = await _boostCalc.GetEffectiveAmountAsync(
                GamificationConstants.XpRewards.LessonComplete,
                XpReason.LessonCompleted,
                request.OccurredAtUtc,
                ct);

            var lessonAward = XpAward.Create(
                profile: profile,
                reason: XpReason.LessonCompleted,
                xpAmount: lessonAmount,
                originEventId: request.OriginEventId,
                occurredAtUtc: request.OccurredAtUtc,
                lessonId: request.LessonId,
                skillId: request.SkillId);

            await _repo.AddXpAwardAsync(lessonAward, ct);

            int newLevel = LevelCurve.LevelForXp(profile.TotalXp + lessonAmount);
            profile.ApplyAward(lessonAmount, newLevel, XpReason.LessonCompleted, request.OccurredAtUtc);

            _logger.LogInfo(
                $"P4-02: LessonComplete +{lessonAmount} XP awarded to student {request.StudentId} " +
                $"(eventId={request.OriginEventId}, lessonId={request.LessonId}).");

            // ── P4-12: Timed-event participation accrual (LessonComplete action) ────────────
            // XP idempotency guard already ran above — this is not a duplicate delivery.
            await AccrueTimedEventParticipationAsync(
                profile, request.OccurredAtUtc, ct);

            // ── QuizPass award (accuracy threshold) ─────────────────────────────────────────
            if (request.AccuracyPercentage >= GamificationConstants.XpRewards.QuizPassAccuracyThreshold)
            {
                bool quizAlreadyAwarded = await _repo.HasXpAwardAsync(
                    request.OriginEventId, XpReason.QuizCompleted, ct);

                if (quizAlreadyAwarded)
                {
                    _logger.LogInfo(
                        $"P4-02: duplicate LessonCompleted event {request.OriginEventId} " +
                        $"for student {request.StudentId} — QuizPass award already exists, skipping.");
                }
                else
                {
                    int quizAmount = await _boostCalc.GetEffectiveAmountAsync(
                        GamificationConstants.XpRewards.QuizPass,
                        XpReason.QuizCompleted,
                        request.OccurredAtUtc,
                        ct);

                    var quizAward = XpAward.Create(
                        profile: profile,
                        reason: XpReason.QuizCompleted,
                        xpAmount: quizAmount,
                        originEventId: request.OriginEventId,
                        occurredAtUtc: request.OccurredAtUtc,
                        lessonId: request.LessonId,
                        skillId: request.SkillId);

                    await _repo.AddXpAwardAsync(quizAward, ct);

                    int levelAfterQuiz = LevelCurve.LevelForXp(profile.TotalXp + quizAmount);
                    profile.ApplyAward(quizAmount, levelAfterQuiz, XpReason.QuizCompleted, request.OccurredAtUtc);

                    _logger.LogInfo(
                        $"P4-02: QuizPass +{quizAmount} XP awarded to student {request.StudentId} " +
                        $"(eventId={request.OriginEventId}).");
                }
            }

            return Success(Unit.Value);
        }
        catch (GamificationUniqueConstraintException ucEx)
        {
            // Defense-in-depth: unique-constraint violation from a concurrent duplicate delivery.
            _logger.LogError(ucEx,
                $"P4-02: unique-constraint violation awarding LessonCompleted XP for student {request.StudentId} " +
                $"(eventId={request.OriginEventId}) — likely idempotency race, treated as already-applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-02: Unexpected error in XpService.AwardLessonCompletedXpAsync " +
                $"(studentId={request.StudentId}, eventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }

    // ---------------------------------------------------------------------------
    // P4-12: Timed-event participation accrual (private helper)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Accrues +1 progress for every currently-active, AllXp-scoped timed event whose window
    /// covers <paramref name="occurredAtUtc"/>. Called from both XP award methods after the XP
    /// row is staged (idempotency already confirmed). Fail-soft per event — a single broken
    /// event does not interrupt the caller.
    ///
    /// Lifecycle:
    /// <list type="number">
    ///   <item>Re-reads active events from the cached <see cref="IActiveTimedEventsQuery"/>
    ///         (warm call — <c>_boostCalc</c> already fetched them moments before).</item>
    ///   <item>For each AllXp event in window: lazy-create or fetch the participation row
    ///         (profile is already row-locked — race-safe; unique index is backstop).</item>
    ///   <item>Calls <c>ApplyProgress(+1, ...)</c>. On <c>completedNow</c>: grants the
    ///         completion reward via <c>RecordTimedEventCompleted</c> (the single XP chokepoint),
    ///         writes an <c>XpAward</c> ledger row, and the domain event is raised on the profile.</item>
    ///   <item>Raises <see cref="TimedEventParticipationProgressDomainEvent"/> when the halfway
    ///         threshold is first crossed (one-time milestone). Latch: checked by comparing
    ///         <c>previousProgress</c> to <c>newProgress</c> vs the threshold.</item>
    ///   <item>Stage only — UnitOfWorkBehavior commits the outer transaction.</item>
    /// </list>
    /// </summary>
    private async Task AccrueTimedEventParticipationAsync(
        StudentXpProfile profile,
        DateTime occurredAtUtc,
        CancellationToken ct)
    {
        IReadOnlyList<ActiveTimedEventSnapshot> events;
        try
        {
            events = await _activeEvents.GetActiveAtAsync(occurredAtUtc, ct);
        }
        catch (Exception ex)
        {
            // Fail-soft: cache/DB failure must not block the XP award already staged.
            _logger.LogError(ex,
                $"P4-12: IActiveTimedEventsQuery failed during participation accrual for " +
                $"studentId={profile.StudentId} — skipping timed-event accrual this action.");
            return;
        }

        if (events.Count == 0) return;

        var opts = _participationOptions.Value;

        // Only AllXp scope is wired in Phase 3 (mirrors XpBoostCalculator).
        var applicable = events
            .Where(e => e.Scope == TimedEventScopeDto.AllXp
                     && e.StartUtc <= occurredAtUtc
                     && e.EndUtc > occurredAtUtc)
            .ToList();

        if (applicable.Count == 0) return;

        foreach (var snapshot in applicable)
        {
            try
            {
                await AccrueSingleEventAsync(profile, snapshot, opts, occurredAtUtc, ct);
            }
            catch (GamificationUniqueConstraintException ucEx)
                when (ucEx.ConstraintHint.Contains("UX_TimedEventParticipations_TimedEventId_StudentXpProfileId"))
            {
                // Concurrent lazy-create race: another concurrent request just inserted the row.
                // The unique index won — treat as already-joined and skip.
                _logger.LogInfo(
                    $"P4-12: unique-constraint race on participation for " +
                    $"studentId={profile.StudentId}, timedEventId={snapshot.Id} — treated as already-joined.");
            }
            catch (Exception ex)
            {
                // Fail-soft per event — a single event failure must not abort others.
                _logger.LogError(ex,
                    $"P4-12: Unexpected error accruing timed-event participation for " +
                    $"studentId={profile.StudentId}, timedEventId={snapshot.Id}.");
            }
        }
    }

    private async Task AccrueSingleEventAsync(
        StudentXpProfile profile,
        ActiveTimedEventSnapshot snapshot,
        TimedEventParticipationOptions opts,
        DateTime occurredAtUtc,
        CancellationToken ct)
    {
        // Resolve target: per-event override (not available on the snapshot — need to load from DB)
        // or config default. We load the TimedEvent row only on lazy-create (fast path is the
        // existing-row read below).
        var participation = await _repo.GetParticipationAsync(snapshot.Id, profile.Id, ct);

        if (participation is null)
        {
            // Lazy-create: load the TimedEvent to resolve ParticipationTarget.
            var timedEvent = await _repo.GetTimedEventByIdAsync(snapshot.Id, ct);
            int resolvedTarget = timedEvent?.ParticipationTarget ?? opts.DefaultTarget;

            participation = TimedEventParticipation.Create(
                profile:      profile,
                timedEventId: snapshot.Id,
                target:       resolvedTarget,
                eventEndUtc:  snapshot.EndUtc,
                joinedUtc:    occurredAtUtc);

            await _repo.AddParticipationAsync(participation, ct);

            _logger.LogInfo(
                $"P4-12: lazy-created participation for studentId={profile.StudentId}, " +
                $"timedEventId={snapshot.Id} (code={snapshot.Code}), target={resolvedTarget}.");
        }
        else
        {
            // Attach so mutations are tracked by EF.
            _repo.AttachParticipation(participation);
        }

        int previousProgress = participation.Progress;
        participation.ApplyProgress(+1, occurredAtUtc, out bool completedNow);
        int newProgress = participation.Progress;

        _logger.LogInfo(
            $"P4-12: progress applied (timedEventId={snapshot.Id}, code={snapshot.Code}, " +
            $"studentId={profile.StudentId}, progress={newProgress}/{participation.Target}, " +
            $"completed={completedNow}).");

        // ── Halfway milestone (one-time) ────────────────────────────────────────────────────
        double halfwayThreshold = participation.Target * (opts.HalfwayThresholdPercent / 100.0);
        if (previousProgress < halfwayThreshold && newProgress >= halfwayThreshold
            && !completedNow)
        {
            profile.RaiseDomainEvent(new TimedEventParticipationProgressDomainEvent(
                StudentId:    profile.StudentId,
                TimedEventId: snapshot.Id,
                Code:         snapshot.Code,
                Progress:     newProgress,
                Target:       participation.Target));
        }

        // ── Completion reward ───────────────────────────────────────────────────────────────
        if (completedNow)
        {
            int rewardXp = await _boostCalc.GetEffectiveAmountAsync(
                opts.CompletionRewardXp,
                XpReason.TimedEventCompleted,
                occurredAtUtc,
                ct);

            int rewardLevel = LevelCurve.LevelForXp(profile.TotalXp + rewardXp);

            profile.RecordTimedEventCompleted(
                timedEventId:   snapshot.Id,
                code:           snapshot.Code,
                rewardXp:       rewardXp,
                newLevel:       rewardLevel,
                completedAtUtc: occurredAtUtc);

            var xpAward = XpAward.Create(
                profile:        profile,
                reason:         XpReason.TimedEventCompleted,
                xpAmount:       rewardXp,
                originEventId:  Guid.NewGuid(),
                occurredAtUtc:  occurredAtUtc,
                lessonId:       null,
                skillId:        null);
            await _repo.AddXpAwardAsync(xpAward, ct);

            _logger.LogInfo(
                $"P4-12: timed-event participation completed (timedEventId={snapshot.Id}, " +
                $"code={snapshot.Code}, studentId={profile.StudentId}, rewardXp={rewardXp}).");
        }
    }
}
