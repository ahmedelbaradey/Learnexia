using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

/// <summary>
/// Student-facing skill node nested inside <see cref="ConceptNodeDto"/>.
/// <see cref="State"/> is a static placeholder in P2-02; real per-student progress arrives in P2-03/P2-04.
/// </summary>
public record SkillNodeDto
{
    public int SkillId { get; init; }
    public string Name { get; init; } = null!;
    public int MasteryThreshold { get; init; }
    public int EstimatedTimeMinutes { get; init; }
    public NodeState State { get; init; }
    public List<int> LessonIds { get; init; } = new();
}
