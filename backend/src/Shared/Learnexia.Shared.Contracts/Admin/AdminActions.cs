namespace Learnexia.Shared.Contracts.Admin;

/// <summary>
/// Well-known action-string constants published in <see cref="AdminActionPerformedEvent.Action"/>.
/// Using string constants (not an enum) so new actions can be added per story without touching
/// this file in Shared.Contracts — only the emitting handler adds a new constant in its story.
/// </summary>
public static class AdminActions
{
    // ── P7-01 Subject ────────────────────────────────────────────────────────
    public const string SubjectCreated    = "Subject.Created";
    public const string SubjectUpdated    = "Subject.Updated";
    public const string SubjectDeleted    = "Subject.Deleted";
    public const string SubjectReordered  = "Subject.Reordered";
    public const string SubjectActivated  = "Subject.Activated";
    public const string SubjectDeactivated = "Subject.Deactivated";

    // ── P7-01 Unit ───────────────────────────────────────────────────────────
    public const string UnitCreated      = "Unit.Created";
    public const string UnitUpdated      = "Unit.Updated";
    public const string UnitDeleted      = "Unit.Deleted";
    public const string UnitReordered    = "Unit.Reordered";
    public const string UnitActivated    = "Unit.Activated";
    public const string UnitDeactivated  = "Unit.Deactivated";

    // ── P7-02 Lesson ─────────────────────────────────────────────────────────
    public const string LessonCreated    = "Lesson.Created";
    public const string LessonUpdated    = "Lesson.Updated";
    public const string LessonDeleted    = "Lesson.Deleted";
    public const string LessonReordered  = "Lesson.Reordered";
    public const string LessonActivated  = "Lesson.Activated";
    public const string LessonDeactivated = "Lesson.Deactivated";

    // ── P7-02 ContentBlock ────────────────────────────────────────────────────
    public const string ContentBlockAdded    = "ContentBlock.Added";
    public const string ContentBlockUpdated  = "ContentBlock.Updated";
    public const string ContentBlockDeleted  = "ContentBlock.Deleted";
    public const string ContentBlockReordered = "ContentBlock.Reordered";

    // ── P7-03 Skill ───────────────────────────────────────────────────────────
    public const string SkillCreated     = "Skill.Created";
    public const string SkillUpdated     = "Skill.Updated";
    public const string SkillDeleted     = "Skill.Deleted";
    public const string SkillActivated   = "Skill.Activated";
    public const string SkillDeactivated = "Skill.Deactivated";

    // ── P7-03 KnowledgeEdge ───────────────────────────────────────────────────
    public const string KnowledgeEdgeAdded   = "KnowledgeEdge.Added";
    public const string KnowledgeEdgeRemoved = "KnowledgeEdge.Removed";

    // ── P7-04 QuizQuestion ────────────────────────────────────────────────────
    public const string QuizQuestionAdded       = "QuizQuestion.Added";
    public const string QuizQuestionUpdated     = "QuizQuestion.Updated";
    public const string QuizQuestionDeleted     = "QuizQuestion.Deleted";
    public const string QuizQuestionReordered   = "QuizQuestion.Reordered";
    public const string QuizQuestionActivated   = "QuizQuestion.Activated";
    public const string QuizQuestionDeactivated = "QuizQuestion.Deactivated";

    // ── P7-05 Content Lifecycle ───────────────────────────────────────────────
    public const string ContentPublished   = "Content.Published";
    public const string ContentArchived    = "Content.Archived";
    public const string ContentUnpublished = "Content.Unpublished";
    public const string ContentRolledBack  = "Content.RolledBack";

    // ── P7-06 Admin User Search & Inspect ────────────────────────────────────
    public const string UserSearched = "User.Searched";
    public const string UserViewed   = "User.Viewed";

    // ── P7-07 Account Lifecycle ───────────────────────────────────────────────
    public const string AccountSuspended   = "Account.Suspended";
    public const string AccountReactivated = "Account.Reactivated";
    public const string AccountDeleted     = "Account.Deleted";

    // ── P7-08 Child Profile & Grade Override ─────────────────────────────────
    public const string ChildProfileUpdated        = "Child.ProfileUpdated";
    public const string ChildGradeOverridden       = "Child.GradeOverridden";
    public const string ChildLearningLanguageChanged = "Child.LearningLanguageChanged";

    // ── P7-13 Gamification Admin Overrides ────────────────────────────────────
    public const string GamificationLeagueTierOverridden = "Gamification.LeagueTierOverridden";
    public const string GamificationStreakFreezeGranted  = "Gamification.StreakFreezeGranted";

    public const string BadgeCreated     = "Badge.Created";
    public const string BadgeUpdated     = "Badge.Updated";
    public const string BadgeActivated   = "Badge.Activated";
    public const string BadgeDeactivated = "Badge.Deactivated";

    public const string MissionCreated     = "Mission.Created";
    public const string MissionUpdated     = "Mission.Updated";
    public const string MissionActivated   = "Mission.Activated";
    public const string MissionDeactivated = "Mission.Deactivated";

    public const string TimedEventCreated    = "TimedEvent.Created";
    public const string TimedEventUpdated    = "TimedEvent.Updated";
    public const string TimedEventActivated  = "TimedEvent.Activated";
    public const string TimedEventExpired    = "TimedEvent.Expired";
}
