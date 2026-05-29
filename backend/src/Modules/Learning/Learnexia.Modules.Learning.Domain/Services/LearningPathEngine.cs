using System.Diagnostics;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Deterministic, reproducible learning path engine. Computes per-lesson <see cref="NodeState"/>
/// and missing-prerequisite explanations for a student in a subject.
///
/// CONTRACT:
/// - Pure static — no database, no DI. Caller pre-fetches all inputs.
/// - Called once per subject per request (bulk, not per-lesson). Q8.
/// - <see cref="SkillMastery.AccuracyPercentage"/> and <see cref="Skill.MasteryThreshold"/> share
///   the same scale: both are 0..100 representing a percentage.
/// - Thread-safe: no shared mutable state.
/// - Cycle defense: a <see cref="Dictionary{TKey,TValue}"/> sentinel ("not mastered") is written
///   before recursing so any cycle is broken at the first back-edge. Q10.
/// - Caller must supply a <see cref="SkillMastery"/> entry (with <c>TotalAnswers = 0</c>) for every
///   skill in the subject even if the student has zero answers, so the engine has the SkillId
///   identity for look-ups. This is a caller contract, not enforced inside the engine.
/// </summary>
public static class LearningPathEngine
{
    /// <summary>
    /// Computes the unlock state for every lesson in <paramref name="lessons"/>.
    /// </summary>
    /// <param name="nodes">All <see cref="KnowledgeNode"/>s for the subject.</param>
    /// <param name="edges">All <see cref="KnowledgeEdge"/>s for the subject (both Prerequisite and Related).</param>
    /// <param name="masteryBySkillId">
    ///     Pre-aggregated per-skill mastery for this student.
    ///     Skills absent from the dictionary are treated as having zero answers (not mastered).
    /// </param>
    /// <param name="completedLessonIds">
    ///     Set of lesson IDs where the student has at least one <c>Attempt</c> with
    ///     <c>Status == AttemptStatus.Completed</c>. Q5.
    /// </param>
    /// <param name="lessons">All lessons for the subject (must include <c>Id</c> and <c>SkillId</c>).</param>
    /// <param name="skillsById">
    ///     Lookup of <see cref="Skill"/> by Id, needed for <c>MasteryThreshold</c> and
    ///     <c>Name</c> fields on each <see cref="MissingPrerequisiteDto"/>.
    /// </param>
    /// <returns>
    ///     A dictionary keyed by <c>Lesson.Id</c> containing the computed
    ///     <see cref="LessonUnlockStateDto"/> for each lesson.
    /// </returns>
    public static IReadOnlyDictionary<int, LessonUnlockStateDto> ComputeStates(
        IReadOnlyList<KnowledgeNode> nodes,
        IReadOnlyList<KnowledgeEdge> edges,
        IReadOnlyDictionary<int, SkillMastery> masteryBySkillId,
        IReadOnlySet<int> completedLessonIds,
        IReadOnlyList<Lesson> lessons,
        IReadOnlyDictionary<int, Skill> skillsById)
    {
        // Defense-in-depth guard: MasteryThreshold must be in 0..100 percentage range.
        // Debug.Assert is a no-op in Release builds — does not throw in production.
        Debug.Assert(skillsById.Values.All(s => s.MasteryThreshold >= 0 && s.MasteryThreshold <= 100),
            "Skill.MasteryThreshold must be in the range 0..100 (percentage).");

        // ── Build lookup: SkillId → KnowledgeNode ───────────────────────────────────────────────
        // Only nodes that wrap a skill participate in unlock rules.
        var nodeBySkillId = nodes
            .Where(n => n.SkillId.HasValue)
            .ToDictionary(n => n.SkillId!.Value, n => n);

        // ── Build adjacency: targetNodeId → list of sourceNodeIds (Prerequisite edges only) ─────
        // Only Prerequisite-typed edges participate in unlock rules (Q1). Related edges are ignored.
        var prereqSourcesByTargetNodeId = edges
            .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite)
            .GroupBy(e => e.TargetNodeId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.SourceNodeId).ToList());

        // ── Build fast look-up: nodeId → KnowledgeNode ──────────────────────────────────────────
        var nodeById = nodes.ToDictionary(n => n.Id, n => n);

        // ── Per-call memoization: nodeId → isMastered ───────────────────────────────────────────
        // Serves double duty: avoids re-computation AND breaks cycles (Q10). A sentinel value of
        // false is written before computing a node so any back-edge resolves to "not mastered."
        var masteryMemo = new Dictionary<int, bool>();

        var result = new Dictionary<int, LessonUnlockStateDto>(lessons.Count);

        foreach (var lesson in lessons)
        {
            // Rule Q5: Completed state wins over all other states (completion is terminal).
            if (completedLessonIds.Contains(lesson.Id))
            {
                result[lesson.Id] = new LessonUnlockStateDto(
                    lesson.Id,
                    NodeState.Completed,
                    Array.Empty<MissingPrerequisiteDto>());
                continue;
            }

            // Rule Q3: Lessons with no linked skill are always Available (no skill to gate on).
            if (lesson.SkillId is null)
            {
                result[lesson.Id] = new LessonUnlockStateDto(
                    lesson.Id,
                    NodeState.Available,
                    Array.Empty<MissingPrerequisiteDto>());
                continue;
            }

            var skillId = lesson.SkillId.Value;

            // Rule Q4: If the skill has no KnowledgeNode in the graph, treat as root → Available.
            if (!nodeBySkillId.TryGetValue(skillId, out var node))
            {
                result[lesson.Id] = new LessonUnlockStateDto(
                    lesson.Id,
                    NodeState.Available,
                    Array.Empty<MissingPrerequisiteDto>());
                continue;
            }

            // Rule Q4: If this node has no inbound Prerequisite edges, it is a root → Available.
            if (!prereqSourcesByTargetNodeId.TryGetValue(node.Id, out var prereqSourceNodeIds)
                || prereqSourceNodeIds.Count == 0)
            {
                result[lesson.Id] = new LessonUnlockStateDto(
                    lesson.Id,
                    NodeState.Available,
                    Array.Empty<MissingPrerequisiteDto>());
                continue;
            }

            // Rule Q1 (AND logic): ALL immediate prerequisite nodes must be mastered.
            // Collect unmet ones for AC3/Q9 explainability.
            var missing = new List<MissingPrerequisiteDto>();

            foreach (var prereqNodeId in prereqSourceNodeIds)
            {
                if (!nodeById.TryGetValue(prereqNodeId, out var prereqNode))
                {
                    // Edge references a node that is not in the input — skip defensively.
                    continue;
                }

                var prereqIsMastered = IsNodeMastered(
                    prereqNode,
                    masteryBySkillId,
                    skillsById,
                    masteryMemo);

                if (!prereqIsMastered)
                {
                    BuildMissingPrerequisiteDto(
                        prereqNode,
                        prereqNodeId,
                        masteryBySkillId,
                        skillsById,
                        missing);
                }
            }

            var state = missing.Count == 0 ? NodeState.Available : NodeState.Locked;
            result[lesson.Id] = new LessonUnlockStateDto(
                lesson.Id,
                state,
                missing.Count > 0
                    ? (IReadOnlyList<MissingPrerequisiteDto>)missing
                    : Array.Empty<MissingPrerequisiteDto>());
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Private helpers
    // ════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns <c>true</c> if the skill wrapped by <paramref name="node"/> is mastered for the student.
    /// Uses <paramref name="memo"/> to avoid re-computation and to prevent infinite loops on cyclic
    /// edges that bypassed seed-time <see cref="SkillGraphValidator"/> checks (Q10 cycle defense).
    /// A sentinel <c>false</c> is written before computation so any back-edge in a cycle resolves
    /// to "not mastered" — the safe default.
    /// </summary>
    private static bool IsNodeMastered(
        KnowledgeNode node,
        IReadOnlyDictionary<int, SkillMastery> masteryBySkillId,
        IReadOnlyDictionary<int, Skill> skillsById,
        Dictionary<int, bool> memo)
    {
        if (memo.TryGetValue(node.Id, out var cached))
            return cached;

        // Write sentinel before computing — breaks any cycle a back-edge would otherwise create.
        memo[node.Id] = false;

        if (!node.SkillId.HasValue)
        {
            // Non-skill node (concept / review checkpoint): no mastery signal → not mastered.
            return false;
        }

        var skillId = node.SkillId.Value;

        if (!skillsById.TryGetValue(skillId, out var skill))
        {
            // Skill referenced by the node is not in the input — treat as not mastered defensively.
            return false;
        }

        if (!masteryBySkillId.TryGetValue(skillId, out var mastery))
        {
            // No answers recorded for this skill → TotalAnswers == 0 → not mastered (Q2 guard).
            return false;
        }

        // Q2: mastered iff AccuracyPercentage >= MasteryThreshold AND TotalAnswers >= 1.
        var mastered = mastery.TotalAnswers >= 1
                    && mastery.AccuracyPercentage >= skill.MasteryThreshold;

        memo[node.Id] = mastered;
        return mastered;
    }

    /// <summary>
    /// Appends a <see cref="MissingPrerequisiteDto"/> to <paramref name="missing"/> for an unmet
    /// prerequisite node. Resolves skill name and required accuracy from <paramref name="skillsById"/>.
    /// When the prereq node has no skill (concept / review node), a placeholder entry is appended
    /// using the node name as the skill name and <c>RequiredAccuracy = 100</c>.
    /// </summary>
    private static void BuildMissingPrerequisiteDto(
        KnowledgeNode prereqNode,
        int prereqNodeId,
        IReadOnlyDictionary<int, SkillMastery> masteryBySkillId,
        IReadOnlyDictionary<int, Skill> skillsById,
        List<MissingPrerequisiteDto> missing)
    {
        if (prereqNode.SkillId.HasValue
            && skillsById.TryGetValue(prereqNode.SkillId.Value, out var prereqSkill))
        {
            var currentAccuracy = masteryBySkillId.TryGetValue(prereqNode.SkillId.Value, out var sm)
                ? sm.AccuracyPercentage
                : 0.0;

            missing.Add(new MissingPrerequisiteDto(
                PrereqSkillId: prereqNode.SkillId.Value,
                PrereqSkillName: prereqSkill.Name,
                PrereqNodeId: prereqNodeId,
                RequiredAccuracy: prereqSkill.MasteryThreshold,
                CurrentAccuracy: currentAccuracy));
        }
        else
        {
            // Prereq node wraps no skill (concept / review node) — placeholder entry.
            missing.Add(new MissingPrerequisiteDto(
                PrereqSkillId: 0,
                PrereqSkillName: prereqNode.Name,
                PrereqNodeId: prereqNodeId,
                RequiredAccuracy: 100,
                CurrentAccuracy: 0.0));
        }
    }
}
