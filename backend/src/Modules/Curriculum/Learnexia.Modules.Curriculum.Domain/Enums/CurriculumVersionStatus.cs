namespace Learnexia.Modules.Curriculum.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.CurriculumVersion"/>.
/// Stored as int in the database.
///
/// Rules (see curriculum-system-of-record.md §3):
/// - At most one <c>Active</c> version per (SubjectId, Language) — enforced by filtered unique index.
/// - Published content is immutable. Corrections create a new Draft version.
/// - Atomic switch: old Active → Archived, new Draft → Active in a single transaction.
/// </summary>
public enum CurriculumVersionStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2,
}
