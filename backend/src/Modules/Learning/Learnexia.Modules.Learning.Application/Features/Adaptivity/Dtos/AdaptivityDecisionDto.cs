using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Adaptivity.Dtos;

/// <summary>
/// Read-model DTO for the adaptive difficulty decision returned by the inspection endpoint (BE-5)
/// and consumable by P3-11 / P3-06 in-process callers.
/// </summary>
public class AdaptivityDecisionDto
{
    /// <summary>The recommended difficulty for the student's next quiz/question set on this skill.</summary>
    public DifficultyLevel Difficulty { get; set; }

    /// <summary>
    /// True when the decision is the cold-start default (Medium), not computed from real signals.
    /// Consumers (P3-11) may apply their own fallback policy when this is true.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Normalized weighted performance score ∈ [0, 1] that drove the banding decision.
    /// 0 when IsDefault=true. Exposed for diagnostics and the optional "why is this hard?" UI.
    /// </summary>
    public double Score { get; set; }
}
