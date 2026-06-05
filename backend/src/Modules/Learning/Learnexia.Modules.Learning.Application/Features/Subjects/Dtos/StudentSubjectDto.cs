using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

/// <summary>
/// Student-facing subject summary returned by GetSubjectsForGradeQuery.
/// Intentionally separate from the admin <see cref="SubjectDto"/> to keep the two contracts independent.
///
/// P8-02: <see cref="SubjectCode"/> exposed so the FE can drive subject ordering and iconography
/// without string-matching on <see cref="Name"/> (which differs between language trees).
/// </summary>
public record StudentSubjectDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public int GradeNumber { get; init; }

    /// <summary>
    /// Stable curriculum identifier (MATH/SCIENCE/ARABIC/ENGLISH).
    /// Unblocks FE ordering and iconography (P8-02 lead decision §3, architecture §7).
    /// </summary>
    public SubjectCode SubjectCode { get; init; }
}
