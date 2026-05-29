using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Module-local generic repository seam for the Learning module. Mirrors Catalog's repository seam.
/// In a deferred-commit module, repository writes stage changes only — the per-module
/// UnitOfWorkBehavior owns the commit (ADR 0001/0002). Implementations must NOT call SaveChangesAsync.
/// </summary>
public interface ILearningRepository : IGenericRepository
{
    // ── Skill dependency graph (P2-11 BE-5) ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the set of <see cref="KnowledgeNode"/> rows that are <em>sources</em> of
    /// <c>Prerequisite</c> edges whose <c>TargetNodeId == nodeId</c>
    /// (i.e. "what must be mastered before this node").
    /// </summary>
    Task<List<KnowledgeNode>> GetPrerequisiteNodesAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns the set of <see cref="KnowledgeNode"/> rows that are <em>targets</em> of
    /// <c>Prerequisite</c> edges whose <c>SourceNodeId == nodeId</c>
    /// (i.e. "what does mastering this node unlock").
    /// </summary>
    Task<List<KnowledgeNode>> GetUnlockedByNodeAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if a <see cref="KnowledgeNode"/> with the given <paramref name="nodeId"/> exists.
    /// </summary>
    Task<bool> KnowledgeNodeExistsAsync(int nodeId, CancellationToken ct = default);

    // ── Learning Path Engine (P2-04) ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all <see cref="KnowledgeNode"/>s whose <c>SubjectId == subjectId</c>.
    /// Used by <c>LearningPathEngine</c> to build the prerequisite graph for a subject.
    /// AsNoTracking.
    /// </summary>
    Task<IReadOnlyList<KnowledgeNode>> GetSubjectKnowledgeNodesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="KnowledgeEdge"/>s where both the source and target node belong to
    /// <paramref name="subjectId"/> (i.e. <c>SourceNode.SubjectId == subjectId AND TargetNode.SubjectId == subjectId</c>).
    /// AsNoTracking.
    /// </summary>
    Task<IReadOnlyList<KnowledgeEdge>> GetSubjectKnowledgeEdgesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Aggregates <c>StudentAnswer</c> rows for the given student and all skills in the given subject.
    /// Groups by <c>Question.SkillId</c> (null SkillId rows are excluded). Computes per-skill
    /// <see cref="SkillMastery"/> (AccuracyPercentage = Math.Round(correct/total*100, 2), TotalAnswers).
    /// Returns an entry for EVERY skill in the subject — skills with no answers receive
    /// <c>AccuracyPercentage = 0</c> and <c>TotalAnswers = 0</c> so <c>LearningPathEngine</c>
    /// has the skill identity for every node without a separate look-up. AsNoTracking.
    /// </summary>
    Task<IReadOnlyDictionary<int, SkillMastery>> GetSkillMasteryForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns the set of distinct LessonIds where the student has at least one <c>Attempt</c>
    /// with <c>Status == AttemptStatus.Completed</c> AND <c>Lesson.Unit.SubjectId == subjectId</c>.
    /// AsNoTracking.
    /// </summary>
    Task<IReadOnlySet<int>> GetCompletedLessonIdsForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="Lesson"/>s whose <c>Unit.SubjectId == subjectId</c>.
    /// Used by <c>LearningPathEngine</c> callers that need the flat lesson list for the subject.
    /// AsNoTracking.
    /// </summary>
    Task<IReadOnlyList<Lesson>> GetSubjectLessonsAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns the SkillId of the given lesson, or null if the lesson has no skill assigned
    /// (or does not exist). AsNoTracking.
    /// </summary>
    Task<int?> GetLessonSkillIdAsync(int lessonId, CancellationToken ct = default);
}
