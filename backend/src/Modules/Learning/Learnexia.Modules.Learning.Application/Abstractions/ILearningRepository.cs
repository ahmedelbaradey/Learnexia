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

    // ── Dashboard (P2-09) ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the SubjectId for the most-recently-started <see cref="Attempt"/> by the given student,
    /// resolved via the join: Attempt.LessonId → Lesson.Unit.SubjectId.
    /// Returns null if the student has no Attempt rows. AsNoTracking.
    /// </summary>
    Task<int?> GetMostRecentActivitySubjectIdAsync(int studentId, CancellationToken ct = default);

    // ── P7-03 Knowledge-graph admin authoring ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="KnowledgeNode"/> whose <c>SkillId == skillId</c>, or null if none exists.
    /// Uses IgnoreQueryFilters because soft-deleted nodes must still be visible here (cascade-delete path).
    /// </summary>
    Task<KnowledgeNode?> GetNodeBySkillIdAsync(int skillId, CancellationToken ct = default);

    /// <summary>
    /// Returns all live (non-deleted) <see cref="KnowledgeEdge"/> rows where either
    /// <c>SourceNodeId == nodeId</c> or <c>TargetNodeId == nodeId</c>.
    /// Used by the skill soft-delete cascade to find edges that reference the skill's node.
    /// </summary>
    Task<List<KnowledgeEdge>> GetEdgesForNodeAsync(int nodeId, bool trackChanges, CancellationToken ct = default);

    /// <summary>
    /// Returns all live (non-deleted) Prerequisite-typed <see cref="KnowledgeEdge"/> rows.
    /// Used by the acyclic validator — must include ALL existing edges so the DFS is complete.
    /// </summary>
    Task<List<KnowledgeEdge>> GetAllPrerequisiteEdgesAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the <see cref="Subject"/> (with <c>Language</c> populated) that owns the given
    /// <see cref="KnowledgeNode"/> (via <c>KnowledgeNode.SubjectId</c>). Returns null if the
    /// node or its subject cannot be found. AsNoTracking, IgnoreQueryFilters on Subject so that
    /// subjects of any state can be resolved (language derivation is structural).
    /// </summary>
    Task<Subject?> GetSubjectForNodeAsync(int nodeId, CancellationToken ct = default);

    /// <summary>
    /// Returns a single <see cref="KnowledgeNode"/> by id (trackChanges=false or true).
    /// Returns null if the node does not exist (after soft-delete filter).
    /// </summary>
    Task<KnowledgeNode?> GetKnowledgeNodeByIdAsync(int nodeId, bool trackChanges, CancellationToken ct = default);

    /// <summary>
    /// Returns a single <see cref="KnowledgeEdge"/> by id (trackChanges determines EF tracking).
    /// Returns null if the edge does not exist (after soft-delete filter).
    /// </summary>
    Task<KnowledgeEdge?> GetKnowledgeEdgeByIdAsync(int edgeId, bool trackChanges, CancellationToken ct = default);

    /// <summary>
    /// Returns true if a live <see cref="KnowledgeEdge"/> with the same
    /// (SourceNodeId, TargetNodeId, RelationshipType) triple already exists.
    /// Used by AddKnowledgeEdgeCommandHandler to enforce the composite-unique constraint gracefully.
    /// </summary>
    Task<bool> KnowledgeEdgeDuplicateExistsAsync(int sourceNodeId, int targetNodeId, int relationshipType, CancellationToken ct = default);

    /// <summary>
    /// Returns all live <see cref="KnowledgeNode"/>s for the given subject, optionally including
    /// inactive skills' nodes (admin view). Used by <c>GetGraphQueryHandler</c>.
    /// </summary>
    Task<List<KnowledgeNode>> GetGraphNodesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all live <see cref="KnowledgeEdge"/>s where both source and target belong to the
    /// given subject. Used by <c>GetGraphQueryHandler</c>.
    /// </summary>
    Task<List<KnowledgeEdge>> GetGraphEdgesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the <see cref="Subject"/> for a <see cref="Concept"/> (via Concept.SubjectId).
    /// Used by the auto-create-node logic in AddSkillCommandHandler. AsNoTracking.
    /// </summary>
    Task<Subject?> GetSubjectByConceptIdAsync(int conceptId, CancellationToken ct = default);
}
