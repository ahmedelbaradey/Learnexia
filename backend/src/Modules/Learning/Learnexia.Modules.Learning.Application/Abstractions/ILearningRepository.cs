using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Module-local generic repository seam for the Learning module. Mirrors Catalog's repository seam.
/// In a deferred-commit module, repository writes stage changes only — the per-module
/// UnitOfWorkBehavior owns the commit (ADR 0001/0002). Implementations must NOT call SaveChangesAsync
/// except via <see cref="FlushAsync"/> when a handler must obtain a DB-generated Id before
/// raising a domain event (P7-12 Bucket C fix). The flush rides the UoW's open transaction;
/// the UoW's own subsequent SaveChangesAsync call is then a no-op.
/// </summary>
public interface ILearningRepository : IGenericRepository
{
    // ── P7-12 Bucket C: mid-handler flush to obtain DB-assigned Id ──────────────────────────────

    /// <summary>
    /// Flushes staged changes to the database within the UoW's open transaction so that
    /// DB-generated Ids (e.g. entity.Id for a newly added row) are assigned BEFORE the handler
    /// raises a domain event that captures those Ids. The UoW calls SaveChangesAsync again after
    /// the handler returns; because no new changes are staged that second call is a no-op.
    ///
    /// Callers: Learning create handlers (AddSubjectCommandHandler, AddUnitCommandHandler,
    /// AddLessonCommandHandler) — must call FlushAsync AFTER AddAsync and BEFORE RaiseDomainEvent.
    /// Must NOT be called outside a UoW transaction (i.e. not in query handlers or background jobs).
    /// </summary>
    Task<int> FlushAsync(int userId, CancellationToken ct = default);

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

    // ── BL-03 BE-4: GetRelatedConcepts ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns nodes connected to the given nodeId via <see cref="EdgeRelationshipType.Related"/> edges
    /// in either direction (source or target). AsNoTracking.
    /// Used by <c>GetRelatedConceptsQueryHandler</c>.
    /// </summary>
    Task<List<KnowledgeNode>> GetRelatedNodesAsync(int nodeId, CancellationToken ct = default);

    // ── BL-03 polish: batch node load for GetRemediationPath ─────────────────────────────────────

    /// <summary>
    /// Returns all live (non-deleted) <see cref="KnowledgeNode"/> rows whose Id is in the given set.
    /// Single <c>WHERE Id IN (...)</c> round-trip — used by <c>GetRemediationPathQueryHandler</c>
    /// to batch-load all BFS result nodes instead of one query per node.
    /// AsNoTracking. Returns only nodes that exist (unresolved ids are simply absent).
    /// </summary>
    Task<List<KnowledgeNode>> GetNodesByIdsAsync(IReadOnlyCollection<int> nodeIds, CancellationToken ct = default);

    // ── P7-05 Content lifecycle / versioning ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the maximum VersionNumber for a given (EntityType, EntityId) partition, or 0 when no
    /// version rows exist yet. Used by <c>PublishContentCommandHandler</c> to assign the next version number.
    /// </summary>
    Task<int> GetMaxVersionNumberAsync(VersionedEntityType entityType, int entityId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="ContentVersion"/> rows for the given (EntityType, EntityId), ordered
    /// descending by VersionNumber (newest first). Used by <c>GetVersionHistoryQueryHandler</c>.
    /// </summary>
    Task<List<ContentVersion>> GetVersionHistoryAsync(VersionedEntityType entityType, int entityId, CancellationToken ct = default);

    /// <summary>
    /// Returns a single <see cref="ContentVersion"/> row by (EntityType, EntityId, VersionNumber),
    /// or null when not found. Used by <c>RollbackToVersionCommandHandler</c>.
    /// </summary>
    Task<ContentVersion?> GetVersionAsync(VersionedEntityType entityType, int entityId, int versionNumber, CancellationToken ct = default);

    /// <summary>
    /// Returns the <see cref="Subject"/> that owns the entity hierarchy for the given entity.
    /// Used to resolve the <see cref="ContentLanguage"/> tag to stamp on the new <see cref="ContentVersion"/>.
    /// Walks the chain: for Subject → self; for Unit → Unit.Subject; for Lesson → Lesson.Unit.Subject;
    /// for QuizQuestion → QuizQuestion.Lesson.Unit.Subject. AsNoTracking. Returns null when not resolvable.
    /// </summary>
    Task<Subject?> GetOwningSubjectAsync(VersionedEntityType entityType, int entityId, CancellationToken ct = default);

    /// <summary>
    /// Returns all published <see cref="Subject"/> rows for the given GradeId, grouped as a coverage
    /// report across (SubjectCode, Language) slots. Used by <c>GetPublicationCoverageQueryHandler</c>.
    /// Admin read — no IsActive filter applied; global IsDeleted filter is still active.
    /// </summary>
    Task<List<Subject>> GetSubjectsForCoverageAsync(int gradeId, CancellationToken ct = default);

    // ── Adaptivity signal aggregation (P3-08) ─────────────────────────────────────────────────────────

    /// <summary>
    /// Aggregates the four FR-AD-1 signals from <c>StudentAnswer</c> rows for the given
    /// (studentId, skillId) pair. Returns a <see cref="SkillAdaptivityAggregate"/> with:
    ///   <c>TotalAnswers</c>, <c>CorrectAnswers</c>, <c>AvgTimeSeconds</c>, <c>HintCount</c>,
    ///   <c>RetryCount</c> (= TotalAnswers - 1, floored at 0, as extra attempts beyond the first).
    /// Returns a zeroed aggregate (TotalAnswers = 0) when no answers exist — signals cold-start.
    /// AsNoTracking.
    /// </summary>
    Task<SkillAdaptivityAggregate> GetAdaptivitySignalsAsync(
        int studentId, int skillId, CancellationToken ct = default);

    // ── Mastery (P3-09) ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all <see cref="StudentSkillMastery"/> rows where <c>SkillId</c> is one of the given
    /// <paramref name="skillIds"/> and <c>StudentId == studentId</c>. Used by the write path to fetch
    /// existing mastery rows before upsert (one query for all skills touched in the attempt).
    /// AsNoTracking — the caller fetches, mutates a local copy, and calls UpsertStudentSkillMasteryAsync.
    /// </summary>
    Task<List<StudentSkillMastery>> GetSkillMasteryRowsAsync(
        int studentId, IReadOnlyCollection<int> skillIds, CancellationToken ct = default);

    /// <summary>
    /// Upserts a <see cref="StudentSkillMastery"/> row for the given (studentId, skillId) pair.
    /// If an existing tracked row is provided, it is updated in-place (EF tracks the change).
    /// If no existing row is found (insert path), a new one is added to the DbSet.
    /// Stages the change only — UnitOfWorkBehavior owns the commit (ADR 0001).
    /// </summary>
    Task UpsertStudentSkillMasteryAsync(StudentSkillMastery mastery, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="StudentSkillMastery"/> rows for the given <paramref name="studentId"/>,
    /// ordered by <c>SkillId</c> ascending. Used by the "all my mastery" query endpoint.
    /// AsNoTracking.
    /// </summary>
    Task<List<StudentSkillMastery>> GetAllMasteryForStudentAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Returns the <see cref="StudentSkillMastery"/> row for the given (studentId, skillId) pair,
    /// or <c>null</c> if no row exists (student has not yet attempted this skill).
    /// Callers should treat <c>null</c> as <c>NotStarted</c> with zero mastery percentage.
    /// AsNoTracking.
    /// </summary>
    Task<StudentSkillMastery?> GetSkillMasteryForStudentAsync(int studentId, int skillId, CancellationToken ct = default);

    // ── Spaced Repetition (P3-10) ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all <see cref="StudentSkillMastery"/> rows that are due for spaced-repetition review
    /// at <paramref name="utcNow"/>, per the <see cref="SpacedRepetitionEngine.IsDue"/> rule:
    /// <list type="bullet">
    ///   <item><c>Status == NeedsReview</c> — always due.</item>
    ///   <item><c>Status == Mastered AND NextReviewDueAt &lt;= utcNow</c> — mastered-but-stale (forgetting check).</item>
    /// </list>
    /// AsNoTracking — used by the sweep job for a read-then-write pattern where the write uses
    /// <see cref="UpdateSpacedRepetitionFieldsAsync"/> (targeted update, not a full entity reload).
    ///
    /// UTC discipline: <paramref name="utcNow"/> must be <c>DateTime.UtcNow</c> (never <c>DateTime.Now</c>).
    /// </summary>
    Task<IReadOnlyList<StudentSkillMastery>> GetDueMasteryRowsAsync(DateTime utcNow, CancellationToken ct = default);

    /// <summary>
    /// Issues a targeted UPDATE on the <c>StudentSkillMasteries</c> table for the row with
    /// <c>Id == id</c>, writing only the three SR scheduling fields
    /// (<c>ReviewIntervalDays</c>, <c>RepetitionNumber</c>, <c>NextReviewDueAt</c>).
    ///
    /// Commits immediately via <c>ExecuteUpdateAsync</c> (targeted SQL — NOT routed through the
    /// UoW behavior, which tracks entity changes). This is intentional: the sweep job operates
    /// OUTSIDE an HTTP command pipeline and has no UoW behavior wrapping it.
    ///
    /// Callers within a UoW command (BE-6 completion hook) must NOT use this method; they should
    /// mutate the tracked entity directly and let UoW commit.
    ///
    /// UTC discipline: <paramref name="nextDueAt"/> must be UTC-kind.
    /// </summary>
    Task UpdateSpacedRepetitionFieldsAsync(int id, int intervalDays, int repetitionNumber, DateTime nextDueAt, CancellationToken ct = default);

    // ── Student Profile (P3-13) ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the aggregated answer signals needed by <c>StudentProfileEngine</c> for the given
    /// student. Returns:
    /// <list type="bullet">
    ///   <item>Per-<c>QuestionType</c> correct/total counts (from StudentAnswer ⋈ QuizQuestion).</item>
    ///   <item>Per-skill wrong-answer counts (StudentAnswer rows where IsCorrect = false, joined on SkillId).</item>
    ///   <item>Per-minute-bucket accuracy sequence (answers bucketed by cumulative TimeSpentSeconds within attempts).</item>
    ///   <item>Overall accuracy and total-answer count across all answers.</item>
    ///   <item>Per-type hint-usage counts.</item>
    /// </list>
    /// AsNoTracking; UTC timestamps throughout.
    /// </summary>
    Task<StudentSignals> GetStudentAnswerSignalsAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Returns the existing <see cref="StudentLearningProfile"/> for the given student, or creates
    /// and stages a default profile row if none exists yet (insert path — UoW commits).
    /// Used by <c>StudentProfileService.RecomputeProfile</c> to ensure a row always exists before upsert.
    /// </summary>
    Task<StudentLearningProfile> GetOrCreateStudentLearningProfileAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Upserts the full <see cref="StudentLearningProfile"/> row for the student. If a tracked
    /// row exists, mutates it in-place; otherwise adds a new row. Stages only — UoW commits.
    /// </summary>
    Task UpsertStudentLearningProfileAsync(StudentLearningProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Returns the <see cref="StudentLearningProfile"/> for the given student, or null when none
    /// exists yet. Used by the inspection query (<c>GET /api/Learning/Profile</c>). AsNoTracking.
    /// </summary>
    Task<StudentLearningProfile?> GetStudentLearningProfileAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="StudentLearningProfile"/> rows where <c>LastRecomputedAt</c> is
    /// older than the given <paramref name="cutoffUtc"/> or is null (never recomputed).
    /// Used by <c>StudentProfileRecomputeJob</c> to identify stale profiles.
    /// AsNoTracking; grade-agnostic (no grade filter — grade transitions must not reset the profile).
    /// </summary>
    Task<List<StudentLearningProfile>> GetStaleProfilesAsync(DateTime cutoffUtc, CancellationToken ct = default);

    // ── Student Recommendations (P5-09) ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all distinct active student IDs that have at least one
    /// <see cref="StudentLearningProfile"/> row (proxy for "has data, worth computing").
    /// Used by <c>RecommendationRecomputeJob</c> to sweep all students that have a profile.
    /// Returns an empty list when no profiles exist.
    /// AsNoTracking.
    /// </summary>
    Task<List<int>> GetStudentIdsWithProfileAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the most recently persisted <see cref="StudentRecommendation"/> row for the given
    /// student (ordered by <c>RecommendationDate</c> descending), or null when no row exists.
    /// Used by <c>IStudentRecommendationsQuery</c> to read the latest daily set.
    /// AsNoTracking.
    /// </summary>
    Task<StudentRecommendation?> GetLatestRecommendationAsync(int studentId, CancellationToken ct = default);

    /// <summary>
    /// Returns the <see cref="StudentRecommendation"/> row for the given
    /// <c>(studentId, date)</c> pair, or null when none exists.
    /// Used by <c>RecommendationService</c> to check whether an upsert is an insert or update.
    /// Tracking — the caller may mutate the returned row and rely on EF change tracking.
    /// </summary>
    Task<StudentRecommendation?> GetRecommendationForDateAsync(int studentId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Stages an upsert of a <see cref="StudentRecommendation"/> row.
    /// If an existing tracked row is supplied, it is mutated in-place (EF tracks the change).
    /// If no existing row is found (insert path), a new row is added to the DbSet.
    /// Stages the change only — the caller (background job) commits explicitly via
    /// <c>LearningDbContext.SaveChangesAsync(userId:0)</c>.
    /// </summary>
    Task UpsertStudentRecommendationAsync(StudentRecommendation recommendation, CancellationToken ct = default);
}
