namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Editorial lifecycle state for curriculum content entities (Subject, Unit, Lesson, QuizQuestion).
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> per the project no-free-text rule.
///
/// P7-05: Per-entity lifecycle (lead decision locked — not per-tree). Each entity carries its own
/// <see cref="LifecycleState"/> column, enabling granular draft/publish/archive control.
///
/// Backfill semantics (CRITICAL):
/// - The migration adds this column with <c>DEFAULT 2</c> (Published) so ALL existing rows are
///   immediately backfilled to <see cref="Published"/>, keeping live curriculum visible to students.
/// - The C# entity property initializer is set to <see cref="Draft"/> so that new entity instances
///   created by admin handlers default to <see cref="Draft"/> at the EF/application layer, overriding
///   the DB DEFAULT on insert. This ensures editorial workflow: admins draft first, then publish.
///
/// Members:
/// - <see cref="Draft"/>     = 1 — content is being edited; NOT served to students. New rows default here.
/// - <see cref="Published"/> = 2 — content is live; served to student-facing reads. Existing rows backfill here.
/// - <see cref="Archived"/>  = 3 — content is retired; hidden from both students and default admin reads.
/// </summary>
public enum LifecycleState
{
    Draft = 1,
    Published = 2,
    Archived = 3,
}
