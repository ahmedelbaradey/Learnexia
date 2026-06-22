namespace Learnexia.Modules.Parent.Application.Features.TransitionChildGrade;

/// <summary>
/// The grade transition outcome returned after a successful (or no-op) Transition-Child-Grade call (P5-06).
/// All history (XP, badges, streaks, mastery) is preserved — this response reflects only the grade change.
/// </summary>
public record TransitionedChildGradeResponse
{
    public int ChildId { get; set; }
    public int OldGrade { get; set; }
    public int NewGrade { get; set; }

    /// <summary>
    /// <c>true</c> when the target grade already matched the child's current grade (idempotent no-op).
    /// No event was published and no mutation was made; the call still succeeds.
    /// </summary>
    public bool IsNoOp { get; set; }
}
