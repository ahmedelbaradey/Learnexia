using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetLanguageCoverage;

/// <summary>
/// Reports the language-coverage status of a single (SubjectCode, Language) slot for a grade.
/// </summary>
public record SubjectLanguageCoverageDto
{
    /// <summary>Stable curriculum code.</summary>
    public SubjectCode SubjectCode { get; init; }

    /// <summary>Language of the slot.</summary>
    public ContentLanguage Language { get; init; }

    /// <summary>true = subject tree exists (not soft-deleted); false = gap.</summary>
    public bool Exists { get; init; }

    /// <summary>
    /// The subject Id when <see cref="Exists"/> is true; null when it is a gap.
    /// </summary>
    public int? SubjectId { get; init; }

    /// <summary>
    /// The subject name when <see cref="Exists"/> is true; null for gaps.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Whether the existing subject tree is active (visible to students).
    /// null when <see cref="Exists"/> is false.
    /// </summary>
    public bool? IsActive { get; init; }
}

/// <summary>
/// Per-grade language coverage report for all expected (SubjectCode, Language) slots.
/// </summary>
public record SubjectLanguageCoverageReportDto
{
    public int GradeId { get; init; }
    public int GradeNumber { get; init; }

    /// <summary>
    /// All expected + present coverage slots.
    /// Includes gaps (Exists=false) so the admin can see which trees are missing.
    /// </summary>
    public List<SubjectLanguageCoverageDto> Coverage { get; init; } = new();

    /// <summary>Count of slots with Exists=false.</summary>
    public int GapCount => Coverage.Count(c => !c.Exists);
}
