namespace Learnexia.Modules.Gamification.Domain.Enums;

// Lifecycle of a per-student mission instance. FR-GM-5.
public enum MissionStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Expired = 3,
}
