using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="SpacedRepetitionEngine"/> (P3-10 BE-3).
///
/// Verifies:
///   1. Ladder advance — repetition 0 → 1 (index 0 → 1, interval 1d → 3d)
///   2. Ladder advance — repetition 1 → 2 (index 1 → 2, interval 3d → 7d)
///   3. Ladder advance clamped at max (repetition at last index stays at max)
///   4. Failure resets to index 0 regardless of current position
///   5. IsDue — NeedsReview is always due
///   6. IsDue — Mastered + past due date → due
///   7. IsDue — Mastered + future due date → not due
///   8. IsDue — NotStarted → not due
///   9. IsDue — Mastered + null NextReviewDueAt → not due (not yet scheduled)
///  10. Idempotency — same inputs → same output (ComputeNextReview)
///  11. Idempotency — same inputs → same output (IsDue)
/// </summary>
public sealed class SpacedRepetitionEngineTests
{
    // ── Default options (mirrors appsettings.json defaults) ──────────────────────────────────────

    private static SpacedRepetitionOptions DefaultOptions() => new SpacedRepetitionOptions
    {
        IntervalLadderDays    = [1, 3, 7, 14, 30],
        NeedsReviewIntervalDays = 1,
        JobCronExpression     = "0 0 * * *",
    };

    private static readonly DateTime UtcNow = new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);

    // ── Case 1: Ladder advance — index 0 → 1 ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeNextReview_Improved_FromIndex0_AdvancesToIndex1()
    {
        var (intervalDays, nextRepetition) = SpacedRepetitionEngine.ComputeNextReview(
            repetitionNumber: 0,
            improved: true,
            options: DefaultOptions());

        intervalDays.Should().Be(3);     // ladder[1]
        nextRepetition.Should().Be(1);
    }

    // ── Case 2: Ladder advance — index 1 → 2 ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeNextReview_Improved_FromIndex1_AdvancesToIndex2()
    {
        var (intervalDays, nextRepetition) = SpacedRepetitionEngine.ComputeNextReview(
            repetitionNumber: 1,
            improved: true,
            options: DefaultOptions());

        intervalDays.Should().Be(7);     // ladder[2]
        nextRepetition.Should().Be(2);
    }

    // ── Case 3: Ladder advance clamped at max ────────────────────────────────────────────────────

    [Fact]
    public void ComputeNextReview_Improved_AtLastIndex_StaysAtMax()
    {
        // Last index = 4 (ladder has 5 elements: 0..4)
        var (intervalDays, nextRepetition) = SpacedRepetitionEngine.ComputeNextReview(
            repetitionNumber: 4,
            improved: true,
            options: DefaultOptions());

        intervalDays.Should().Be(30);    // ladder[4] — clamped
        nextRepetition.Should().Be(4);
    }

    // ── Case 4: Failure resets to index 0 ────────────────────────────────────────────────────────

    [Fact]
    public void ComputeNextReview_NotImproved_ResetsToIndex0()
    {
        // Start from index 3 (14-day interval) — failure resets to index 0 (1 day)
        var (intervalDays, nextRepetition) = SpacedRepetitionEngine.ComputeNextReview(
            repetitionNumber: 3,
            improved: false,
            options: DefaultOptions());

        intervalDays.Should().Be(1);     // ladder[0] — reset
        nextRepetition.Should().Be(0);
    }

    // ── Case 5: IsDue — NeedsReview is always due ────────────────────────────────────────────────

    [Fact]
    public void IsDue_NeedsReview_AlwaysDue()
    {
        // Regardless of nextReviewDueAt, NeedsReview status → always due.
        var dueNull    = SpacedRepetitionEngine.IsDue(MasteryStatus.NeedsReview, null,                        UtcNow);
        var dueFuture  = SpacedRepetitionEngine.IsDue(MasteryStatus.NeedsReview, UtcNow.AddDays(30),         UtcNow);
        var duePast    = SpacedRepetitionEngine.IsDue(MasteryStatus.NeedsReview, UtcNow.AddDays(-7),         UtcNow);

        dueNull.Should().BeTrue();
        dueFuture.Should().BeTrue();
        duePast.Should().BeTrue();
    }

    // ── Case 6: IsDue — Mastered + past due date → due ───────────────────────────────────────────

    [Fact]
    public void IsDue_Mastered_PastDueDate_IsDue()
    {
        var pastDue = UtcNow.AddDays(-1);   // yesterday — elapsed
        var result  = SpacedRepetitionEngine.IsDue(MasteryStatus.Mastered, pastDue, UtcNow);

        result.Should().BeTrue();
    }

    // ── Case 7: IsDue — Mastered + future due date → not due ────────────────────────────────────

    [Fact]
    public void IsDue_Mastered_FutureDueDate_NotDue()
    {
        var futureDue = UtcNow.AddDays(5);  // not elapsed yet
        var result    = SpacedRepetitionEngine.IsDue(MasteryStatus.Mastered, futureDue, UtcNow);

        result.Should().BeFalse();
    }

    // ── Case 8: IsDue — NotStarted → not due ─────────────────────────────────────────────────────

    [Fact]
    public void IsDue_NotStarted_NeverDue()
    {
        var result = SpacedRepetitionEngine.IsDue(MasteryStatus.NotStarted, null, UtcNow);

        result.Should().BeFalse();
    }

    // ── Case 9: IsDue — Mastered + null NextReviewDueAt → not due ────────────────────────────────

    [Fact]
    public void IsDue_Mastered_NullNextReviewDueAt_NotDue()
    {
        // Mastered but no schedule set yet (P3-10 not yet run for this row) → not due.
        var result = SpacedRepetitionEngine.IsDue(MasteryStatus.Mastered, null, UtcNow);

        result.Should().BeFalse();
    }

    // ── Case 10: Idempotency — ComputeNextReview same inputs → same output ───────────────────────

    [Fact]
    public void ComputeNextReview_SameInputsTwice_ReturnsSameOutput()
    {
        var opts = DefaultOptions();

        var first  = SpacedRepetitionEngine.ComputeNextReview(2, improved: true,  opts);
        var second = SpacedRepetitionEngine.ComputeNextReview(2, improved: true,  opts);

        first.Should().Be(second);
    }

    // ── Case 11: Idempotency — IsDue same inputs → same output ───────────────────────────────────

    [Fact]
    public void IsDue_SameInputsTwice_ReturnsSameOutput()
    {
        var due = UtcNow.AddDays(-2);

        var first  = SpacedRepetitionEngine.IsDue(MasteryStatus.Mastered, due, UtcNow);
        var second = SpacedRepetitionEngine.IsDue(MasteryStatus.Mastered, due, UtcNow);

        first.Should().Be(second);
    }

    // ── Boundary: IsDue — due date exactly equals utcNow (on the boundary → due) ─────────────────

    [Fact]
    public void IsDue_Mastered_ExactlyAtDueDate_IsDue()
    {
        // utcNow >= nextReviewDueAt when they are equal.
        var result = SpacedRepetitionEngine.IsDue(MasteryStatus.Mastered, UtcNow, UtcNow);

        result.Should().BeTrue();
    }

    // ── InProgress → not due (only NeedsReview and Mastered-stale are due) ───────────────────────

    [Fact]
    public void IsDue_InProgress_NeverDue()
    {
        var result = SpacedRepetitionEngine.IsDue(MasteryStatus.InProgress, UtcNow.AddDays(-10), UtcNow);

        result.Should().BeFalse();
    }
}
