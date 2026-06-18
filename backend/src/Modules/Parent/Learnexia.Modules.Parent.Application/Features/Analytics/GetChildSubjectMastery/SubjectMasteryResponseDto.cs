namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildSubjectMastery;

/// <summary>
/// E4 response DTO — per-subject mastery breakdown for the parent's subject-mastery ring chart.
/// Covers all fields from <c>SubjectMasteryStub[]</c> in the FE gold contract.
/// </summary>
public sealed class SubjectMasteryResponseDto
{
    /// <summary>Overall mastery percentage across all practiced subjects (0–100).</summary>
    public int OverallPercent { get; init; }

    /// <summary>
    /// One entry per curriculum subject. Always contains exactly the four MVP subjects
    /// (Math / Science / Arabic / English) even when the child has no data for a subject
    /// (that subject's Percent is 0).
    /// </summary>
    public IReadOnlyList<SubjectMasteryItemDto> BySubject { get; init; } = [];
}

/// <summary>Mastery percentage for a single subject.</summary>
public sealed class SubjectMasteryItemDto
{
    /// <summary>The subject code as int (0=MATH,1=SCIENCE,2=ARABIC,3=ENGLISH).</summary>
    public int SubjectCode { get; init; }

    /// <summary>Average mastery percentage for all skills in this subject (0–100).</summary>
    public int Percent { get; init; }
}
