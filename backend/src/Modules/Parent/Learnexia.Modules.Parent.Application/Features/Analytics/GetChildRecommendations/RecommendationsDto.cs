using Learnexia.Shared.Contracts.Learning;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildRecommendations;

/// <summary>
/// E10 response DTO — daily recommendation set displayed on the parent dashboard
/// "Recommendations" panel (<c>RecommendationsCard.tsx</c>).
/// An empty list means "no recommendations computed yet" — never an error (cold-start safe).
/// </summary>
public sealed class RecommendationsDto
{
    /// <summary>
    /// Ordered recommendation items (ranked by severity then mastery deficit descending).
    /// Empty list = cold-start / no weak areas / job not yet run. Never null.
    /// </summary>
    public IReadOnlyList<RecommendationItemDto> Items { get; init; } = [];
}

/// <summary>
/// A single recommendation item in the parent dashboard set.
/// All text fields are i18n keys — the FE resolves them via its localiser (EN/AR).
/// </summary>
public sealed class RecommendationItemDto
{
    /// <summary>
    /// Intra-Learning skill id this recommendation targets.
    /// 0 for cold-start / generic encouraging items (no skill targeted).
    /// </summary>
    public int SkillId { get; init; }

    /// <summary>Subject code as int (0=MATH, 1=SCIENCE, 2=ARABIC, 3=ENGLISH).</summary>
    public int SubjectCode { get; init; }

    /// <summary>i18n resource key for the recommendation title.</summary>
    public string TitleKey { get; init; } = string.Empty;

    /// <summary>i18n resource key for the recommendation body text.</summary>
    public string BodyKey { get; init; } = string.Empty;

    /// <summary>i18n resource key for the call-to-action label.</summary>
    public string CtaKey { get; init; } = string.Empty;

    /// <summary>Severity as int: 1=Low, 2=Medium, 3=High.</summary>
    public int Severity { get; init; }

    /// <summary>
    /// Action type as int (matches <see cref="RecommendationActionType"/>):
    /// 1=Practice, 2=Review, 3=KeepStreak, 4=Celebrate.
    /// </summary>
    public int ActionType { get; init; }

    /// <summary>
    /// Target difficulty for the recommended practice session as int
    /// (1=Easy, 2=Medium, 3=Hard).
    /// </summary>
    public int TargetDifficulty { get; init; }
}
