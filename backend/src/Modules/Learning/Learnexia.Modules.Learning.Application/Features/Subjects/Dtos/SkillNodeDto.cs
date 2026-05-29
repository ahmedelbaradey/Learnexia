using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

/// <summary>
/// Student-facing skill node nested inside <see cref="ConceptNodeDto"/>.
/// <see cref="State"/> is engine-derived per-student state from P2-04.
/// </summary>
public record SkillNodeDto
{
    public int SkillId { get; init; }
    public string Name { get; init; } = null!;
    public int MasteryThreshold { get; init; }
    public int EstimatedTimeMinutes { get; init; }
    public NodeState State { get; init; }
    public List<int> LessonIds { get; init; } = new();

    /// <summary>
    /// Explains unmet prerequisites when <see cref="State"/> is <see cref="NodeState.Locked"/>.
    /// Null when the state was derived anonymously (no JWT). Empty when Available or Completed.
    /// </summary>
    public IReadOnlyList<MissingPrerequisiteDto>? MissingPrerequisites { get; init; }
}
