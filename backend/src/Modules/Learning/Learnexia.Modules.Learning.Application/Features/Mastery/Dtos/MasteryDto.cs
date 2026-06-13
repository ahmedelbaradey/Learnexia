using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Mastery.Dtos;

/// <summary>
/// Read-model DTO for a single StudentSkillMastery row.
/// Returned by both GetStudentMastery (list) and GetSkillMastery (single) endpoints.
/// </summary>
public class MasteryDto
{
    /// <summary>The skill this mastery row tracks.</summary>
    public int SkillId { get; set; }

    /// <summary>
    /// Cumulative accuracy percentage [0, 100].
    /// Computed by <c>MasteryEngine.Compute</c> (formula: round(correct/total*100)).
    /// </summary>
    public int MasteryPercentage { get; set; }

    /// <summary>Current mastery status per the 4-state machine (Q2).</summary>
    public MasteryStatus Status { get; set; }

    /// <summary>Total completed attempts that contributed to this mastery row.</summary>
    public int AttemptsCount { get; set; }

    /// <summary>UTC timestamp of the most recent attempt that updated this row. Null when NotStarted.</summary>
    public DateTime? LastPracticedAt { get; set; }
}
