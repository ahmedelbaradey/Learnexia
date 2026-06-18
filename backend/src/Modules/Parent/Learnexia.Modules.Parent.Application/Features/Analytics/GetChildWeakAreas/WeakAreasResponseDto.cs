namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildWeakAreas;

/// <summary>
/// E5 response DTO — focus areas displayed on the parent dashboard "Areas to focus" panel.
/// Covers all fields from <c>FocusAreaStub[]</c> in the FE gold contract.
/// An empty list means "no weak areas currently detected" — never an error (AC3).
/// </summary>
public sealed class WeakAreasResponseDto
{
    public IReadOnlyList<WeakAreaItemDto> Areas { get; init; } = [];
}

/// <summary>A single focus area for a child.</summary>
public sealed class WeakAreaItemDto
{
    public int SkillId { get; init; }
    public string SkillName { get; init; } = string.Empty;

    /// <summary>Subject code as int (0=MATH,1=SCIENCE,2=ARABIC,3=ENGLISH).</summary>
    public int SubjectCode { get; init; }

    public int MasteryPercent { get; init; }

    /// <summary>Severity as int: 1=Low, 2=Medium, 3=High.</summary>
    public int Severity { get; init; }

    /// <summary>Localisation key for the suggested next action (resolved by caller if displayed).</summary>
    public string SuggestedNextAction { get; init; } = string.Empty;
}
