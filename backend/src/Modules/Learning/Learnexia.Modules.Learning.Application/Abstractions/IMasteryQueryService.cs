using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Service seam for mastery-query reads in the Learning module (P3-09).
/// Distinct from the cross-module <c>IMasteryService</c> (Application/Services) which exposes
/// projected DTOs for cross-module consumption. This interface returns materialized domain rows
/// for use within the Learning module's own query handlers.
/// All EF reads are encapsulated here — Application query handlers inject this interface only (Option C).
/// </summary>
public interface IMasteryQueryService
{
    /// <summary>
    /// Returns the <see cref="StudentSkillMastery"/> row for (studentId, skillId), or null when
    /// no mastery row exists (student has not yet attempted this skill). AsNoTracking.
    /// </summary>
    Task<StudentSkillMastery?> GetSkillMasteryForStudentAsync(
        int studentId, int skillId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="StudentSkillMastery"/> rows for the given student, ordered by SkillId
    /// ascending. Empty list when the student has no mastery data. AsNoTracking.
    /// </summary>
    Task<List<StudentSkillMastery>> GetAllMasteryForStudentAsync(
        int studentId, CancellationToken ct = default);
}
