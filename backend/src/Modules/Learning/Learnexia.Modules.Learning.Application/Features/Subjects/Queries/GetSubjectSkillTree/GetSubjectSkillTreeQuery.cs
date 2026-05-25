using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectSkillTree;

/// <summary>
/// Returns a subject's concept-and-skill tree, with a placeholder NodeState per skill
/// derived from Lesson.IsLocked (P2-02 static logic; replaced by real progress in P2-03/P2-04).
/// Student-facing read query — not auto-validated (IQuery, not ICommand).
/// Route: GET /api/learning/Subjects/{id}/SkillTree
/// </summary>
public record GetSubjectSkillTreeQuery : IQuery<BaseResponse<List<ConceptNodeDto>>>
{
    public int SubjectId { get; init; }
}
