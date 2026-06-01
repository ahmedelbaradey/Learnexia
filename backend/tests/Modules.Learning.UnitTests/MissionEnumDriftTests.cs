using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Contracts.Gamification;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Compile-time parity assertions for the mirrored enum types introduced in P4-06.
///
/// The Shared.Contracts assembly ships mirror enums (<c>MissionTypeDto</c>, <c>MissionStatusDto</c>,
/// <c>MissionTargetTypeDto</c>) whose integer values MUST remain identical to their Domain counterparts
/// (<c>MissionType</c>, <c>MissionStatus</c>, <c>MissionTargetType</c>). Cross-module casts rely on
/// these values being equal:
///   <c>(MissionStatusDto)(int)m.Status</c> — used in <c>StudentMissionsQuery.ToSummary()</c>.
///   <c>(MissionTargetTypeDto)(int)m.TargetType</c> — used in the same projection.
///
/// If someone edits either enum without updating the other, these assertions catch the drift at
/// compile time (the test will fail at run time, surfacing the mismatch before it reaches production).
///
/// Mirrors <c>BadgePredicateEvaluatorTests.EnumDrift_BadgeRarityDto_MatchesBadgeRarity</c> (P4-05).
///
/// Coverage map (8 assertions):
///   MissionType.Daily == MissionTypeDto.Daily (both = 1)
///   MissionType.Weekly == MissionTypeDto.Weekly (both = 2)
///   MissionTargetType.CompleteLessons == MissionTargetTypeDto.CompleteLessons (both = 1)
///   MissionTargetType.CorrectAnswers  == MissionTargetTypeDto.CorrectAnswers  (both = 2)
///   MissionTargetType.MaintainStreak  == MissionTargetTypeDto.MaintainStreak  (both = 3)
///   MissionStatus.NotStarted == MissionStatusDto.NotStarted (both = 0)
///   MissionStatus.InProgress == MissionStatusDto.InProgress (both = 1)
///   MissionStatus.Completed  == MissionStatusDto.Completed  (both = 2)
///   MissionStatus.Expired    == MissionStatusDto.Expired    (both = 3)
/// </summary>
public sealed class MissionEnumDriftTests
{
    // =========================================================================
    // MissionType ↔ MissionTypeDto
    // =========================================================================

    [Fact(DisplayName = "P4-06-EnumDrift MissionType.Daily == MissionTypeDto.Daily (both = 1)")]
    public void MissionType_Daily_MatchesDto()
    {
        ((int)MissionType.Daily).Should().Be((int)MissionTypeDto.Daily,
            "MissionTypeDto.Daily must equal MissionType.Daily (1) — cross-module cast safety");
    }

    [Fact(DisplayName = "P4-06-EnumDrift MissionType.Weekly == MissionTypeDto.Weekly (both = 2)")]
    public void MissionType_Weekly_MatchesDto()
    {
        ((int)MissionType.Weekly).Should().Be((int)MissionTypeDto.Weekly,
            "MissionTypeDto.Weekly must equal MissionType.Weekly (2) — cross-module cast safety");
    }

    // =========================================================================
    // MissionTargetType ↔ MissionTargetTypeDto
    // =========================================================================

    [Fact(DisplayName = "P4-06-EnumDrift MissionTargetType.CompleteLessons == MissionTargetTypeDto.CompleteLessons (both = 1)")]
    public void MissionTargetType_CompleteLessons_MatchesDto()
    {
        ((int)MissionTargetType.CompleteLessons).Should().Be((int)MissionTargetTypeDto.CompleteLessons,
            "MissionTargetTypeDto.CompleteLessons must equal domain value (1)");
    }

    [Fact(DisplayName = "P4-06-EnumDrift MissionTargetType.CorrectAnswers == MissionTargetTypeDto.CorrectAnswers (both = 2)")]
    public void MissionTargetType_CorrectAnswers_MatchesDto()
    {
        ((int)MissionTargetType.CorrectAnswers).Should().Be((int)MissionTargetTypeDto.CorrectAnswers,
            "MissionTargetTypeDto.CorrectAnswers must equal domain value (2)");
    }

    [Fact(DisplayName = "P4-06-EnumDrift MissionTargetType.MaintainStreak == MissionTargetTypeDto.MaintainStreak (both = 3)")]
    public void MissionTargetType_MaintainStreak_MatchesDto()
    {
        ((int)MissionTargetType.MaintainStreak).Should().Be((int)MissionTargetTypeDto.MaintainStreak,
            "MissionTargetTypeDto.MaintainStreak must equal domain value (3)");
    }

    // =========================================================================
    // MissionStatus ↔ MissionStatusDto
    // =========================================================================

    [Fact(DisplayName = "P4-06-EnumDrift MissionStatus.NotStarted == MissionStatusDto.NotStarted (both = 0)")]
    public void MissionStatus_NotStarted_MatchesDto()
    {
        ((int)MissionStatus.NotStarted).Should().Be((int)MissionStatusDto.NotStarted,
            "MissionStatusDto.NotStarted must equal domain value (0)");
    }

    [Fact(DisplayName = "P4-06-EnumDrift MissionStatus.InProgress == MissionStatusDto.InProgress (both = 1)")]
    public void MissionStatus_InProgress_MatchesDto()
    {
        ((int)MissionStatus.InProgress).Should().Be((int)MissionStatusDto.InProgress,
            "MissionStatusDto.InProgress must equal domain value (1)");
    }

    [Fact(DisplayName = "P4-06-EnumDrift MissionStatus.Completed == MissionStatusDto.Completed (both = 2)")]
    public void MissionStatus_Completed_MatchesDto()
    {
        ((int)MissionStatus.Completed).Should().Be((int)MissionStatusDto.Completed,
            "MissionStatusDto.Completed must equal domain value (2)");
    }

    [Fact(DisplayName = "P4-06-EnumDrift MissionStatus.Expired == MissionStatusDto.Expired (both = 3)")]
    public void MissionStatus_Expired_MatchesDto()
    {
        ((int)MissionStatus.Expired).Should().Be((int)MissionStatusDto.Expired,
            "MissionStatusDto.Expired must equal domain value (3)");
    }
}
