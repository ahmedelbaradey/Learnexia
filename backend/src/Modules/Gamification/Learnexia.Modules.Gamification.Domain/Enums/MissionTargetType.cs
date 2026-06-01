namespace Learnexia.Modules.Gamification.Domain.Enums;

/// <summary>
/// Identifies which type of student activity a mission is tracking. Used by
/// <c>MissionProgressUpdater</c> to dispatch the correct increment on each event.
/// FR-GM-5. Values MUST stay in sync with <c>MissionTargetTypeDto</c> in Shared.Contracts.
/// </summary>
public enum MissionTargetType
{
    CompleteLessons = 1,
    CorrectAnswers  = 2,
    MaintainStreak  = 3,
}
