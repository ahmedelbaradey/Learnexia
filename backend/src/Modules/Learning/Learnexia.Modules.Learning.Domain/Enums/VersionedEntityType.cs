namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Identifies which curriculum entity type a <c>ContentVersion</c> snapshot belongs to.
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> per the project no-free-text rule.
///
/// P7-05: Used in <c>ContentVersion.EntityType</c> together with <c>ContentVersion.EntityId</c>
/// (a plain int) to form the loose polymorphic reference to the versioned entity.  No cross-entity
/// FK is created — the (EntityType, EntityId) pair is resolved by the application layer.
///
/// Members align 1-to-1 with the scoped lifecycle entities:
/// - <see cref="Subject"/>      = 1 — Subject entity.
/// - <see cref="Unit"/>         = 2 — Unit entity.
/// - <see cref="Lesson"/>       = 3 — Lesson entity.
/// - <see cref="QuizQuestion"/> = 4 — QuizQuestion entity.
/// </summary>
public enum VersionedEntityType
{
    Subject = 1,
    Unit = 2,
    Lesson = 3,
    QuizQuestion = 4,
}
