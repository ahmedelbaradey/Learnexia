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
}
