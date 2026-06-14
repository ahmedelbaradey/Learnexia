namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Minimal lesson context carried across the module boundary via <see cref="ILessonContextContract"/>.
/// Contains only non-PII curriculum facts required by the AI helper handler (P3-04).
/// </summary>
/// <param name="LessonId">The lesson primary key.</param>
/// <param name="Title">Human-readable lesson title (used in refuse-and-redirect copy).</param>
/// <param name="SubjectId">The subject this lesson belongs to.</param>
/// <param name="GradeId">The grade this lesson targets.</param>
public sealed record LessonContextDto(
    int LessonId,
    string Title,
    int SubjectId,
    int GradeId);
