using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;

/// <summary>
/// Publication coverage for a single (SubjectCode, Language) slot within a grade.
/// Returned as part of <see cref="PublicationCoverageReportDto"/>.
/// </summary>
public class PublicationCoverageSlotDto
{
    public SubjectCode SubjectCode { get; set; }
    public ContentLanguage Language { get; set; }

    /// <summary>True when a Subject row exists for this (SubjectCode, Language) in the grade.</summary>
    public bool Exists { get; set; }

    /// <summary>Subject PK when present; null when absent.</summary>
    public int? SubjectId { get; set; }

    /// <summary>Subject name when present; null when absent.</summary>
    public string? Name { get; set; }

    /// <summary>Whether the subject is active for student reads.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Current LifecycleState of the subject (Draft/Published/Archived).</summary>
    public LifecycleState? LifecycleState { get; set; }

    /// <summary>
    /// True when the subject exists AND has LifecycleState == Published.
    /// The key coverage flag: admins can see at a glance which trees are live to students.
    /// </summary>
    public bool IsPublished => Exists && LifecycleState == Domain.Enums.LifecycleState.Published;
}

/// <summary>
/// Per-grade publication coverage report for all (SubjectCode, Language) slots.
/// Returned by <c>GetPublicationCoverageQuery</c>. AdminOnly.
/// </summary>
public class PublicationCoverageReportDto
{
    public int GradeId { get; set; }
    public int GradeNumber { get; set; }
    public List<PublicationCoverageSlotDto> Coverage { get; set; } = new();
}
