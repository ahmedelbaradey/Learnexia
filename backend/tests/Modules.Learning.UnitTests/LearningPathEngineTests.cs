using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="LearningPathEngine.ComputeStates"/>.
///
/// All fixtures are built via object initializers — no DbContext is required.
/// Node IDs, skill IDs and lesson IDs are deterministic integers (1, 2, 3, …).
///
/// Ten required cases:
/// 1.  EmptyGraph_ReturnsEmptyDictionary
/// 2.  LessonWithNullSkillId_IsAvailable
/// 3.  SinglePrereq_Mastered_LessonIsAvailable
/// 4.  SinglePrereq_NotMastered_LessonIsLocked_WithMissingPrereq
/// 5.  TwoPrereqs_OneUnmet_LessonIsLocked_MissingCountIsOne
/// 6.  TwoPrereqs_BothMastered_LessonIsAvailable
/// 7.  ZeroAnswers_LessonIsLocked
/// 8.  CompletedLesson_IsCompleted_RegardlessOfMastery
/// 9.  CycleDefense_EngineDoesNotInfiniteLoop
/// 10. MasteryJustBelowThreshold_LessonIsLocked
/// 11. MasteryAtExactThreshold_LessonIsAvailable  (bonus edge case)
/// 12. RelatedEdgesIgnored_LessonIsAvailable       (bonus — confirms Related edges are excluded)
/// </summary>
public sealed class LearningPathEngineTests
{
    // ── Fixture helpers ───────────────────────────────────────────────────────

    private static Skill MakeSkill(int id, string name, int masteryThreshold) =>
        new()
        {
            Id = id,
            Name = name,
            MasteryThreshold = masteryThreshold,
            EstimatedTimeMinutes = 10,
            ConceptId = 1 // not relevant for engine tests
        };

    private static KnowledgeNode MakeNode(int id, int skillId) =>
        new()
        {
            Id = id,
            Name = $"Node-{id}",
            NodeType = KnowledgeNodeType.Skill,
            SubjectId = 1,
            GradeId = 1,
            Difficulty = 1,
            SkillId = skillId
        };

    private static KnowledgeEdge PrereqEdge(int sourceNodeId, int targetNodeId) =>
        new()
        {
            Id = sourceNodeId * 100 + targetNodeId, // deterministic id
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            RelationshipType = EdgeRelationshipType.Prerequisite,
            Strength = 1.0m
        };

    private static KnowledgeEdge RelatedEdge(int sourceNodeId, int targetNodeId) =>
        new()
        {
            Id = sourceNodeId * 100 + targetNodeId,
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            RelationshipType = EdgeRelationshipType.Related,
            Strength = 1.0m
        };

    private static Lesson MakeLesson(int id, int? skillId) =>
        new()
        {
            Id = id,
            Name = $"Lesson-{id}",
            Difficulty = DifficultyLevel.Easy,
            SequenceOrder = id,
            IsLocked = false,
            UnitId = 1,
            SkillId = skillId
        };

    private static SkillMastery Mastery(int skillId, double accuracy, int totalAnswers) =>
        new(skillId, accuracy, totalAnswers);

    // ══════════════════════════════════════════════════════════════════════════
    // Case 1 — Empty inputs → empty dictionary, no throw
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void EmptyGraph_ReturnsEmptyDictionary()
    {
        // Arrange — no lessons, no nodes, no edges, no mastery
        var nodes = Array.Empty<KnowledgeNode>();
        var edges = Array.Empty<KnowledgeEdge>();
        var mastery = new Dictionary<int, SkillMastery>();
        var completed = new HashSet<int>();
        var lessons = Array.Empty<Lesson>();
        var skillsById = new Dictionary<int, Skill>();

        // Act
        var act = () => LearningPathEngine.ComputeStates(
            nodes, edges, mastery, completed, lessons, skillsById);

        // Assert
        act.Should().NotThrow();
        var result = LearningPathEngine.ComputeStates(
            nodes, edges, mastery, completed, lessons, skillsById);
        result.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 2 — Lesson with null SkillId → Available, empty MissingPrerequisites (Q3)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void LessonWithNullSkillId_IsAvailable()
    {
        // Arrange — lesson has no skill; graph is otherwise irrelevant
        var lesson = MakeLesson(id: 1, skillId: null);

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: Array.Empty<KnowledgeNode>(),
            edges: Array.Empty<KnowledgeEdge>(),
            masteryBySkillId: new Dictionary<int, SkillMastery>(),
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson },
            skillsById: new Dictionary<int, Skill>());

        // Assert
        result.Should().ContainKey(1);
        result[1].State.Should().Be(NodeState.Available);
        result[1].MissingPrerequisites.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 3 — Single prereq, mastered → Available, no missing prerequisites
    // Graph: Node-1 (Skill-1) → Node-2 (Skill-2)
    //   Lesson-2 teaches Skill-2; student has mastered Skill-1 at 85% (threshold 80)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SinglePrereq_Mastered_LessonIsAvailable()
    {
        // Arrange
        var skill1 = MakeSkill(id: 1, name: "Count to 1000",     masteryThreshold: 80);
        var skill2 = MakeSkill(id: 2, name: "Compare Numbers",   masteryThreshold: 80);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);

        // Node-1 is prereq for Node-2
        var edges = new[] { PrereqEdge(sourceNodeId: 1, targetNodeId: 2) };

        var lesson2 = MakeLesson(id: 2, skillId: 2);

        var mastery = new Dictionary<int, SkillMastery>
        {
            [1] = Mastery(skillId: 1, accuracy: 85.0, totalAnswers: 10)
        };

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson2 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2 });

        // Assert
        result.Should().ContainKey(2);
        result[2].State.Should().Be(NodeState.Available);
        result[2].MissingPrerequisites.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 4 — Single prereq, NOT mastered → Locked, MissingPrerequisites.Count == 1
    //          Checks all 5 fields of MissingPrerequisiteDto (AC3 / Q9)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SinglePrereq_NotMastered_LessonIsLocked_WithMissingPrereq()
    {
        // Arrange
        var skill1 = MakeSkill(id: 1, name: "Count to 1000", masteryThreshold: 80);
        var skill2 = MakeSkill(id: 2, name: "Compare Numbers", masteryThreshold: 80);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);

        var edges = new[] { PrereqEdge(sourceNodeId: 1, targetNodeId: 2) };

        var lesson2 = MakeLesson(id: 2, skillId: 2);

        // Student has 60% on Skill-1 (below threshold 80) with 5 answers
        var mastery = new Dictionary<int, SkillMastery>
        {
            [1] = Mastery(skillId: 1, accuracy: 60.0, totalAnswers: 5)
        };

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson2 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2 });

        // Assert — state is Locked with exactly one missing prereq
        result.Should().ContainKey(2);
        result[2].State.Should().Be(NodeState.Locked);
        result[2].MissingPrerequisites.Should().HaveCount(1);

        var mp = result[2].MissingPrerequisites[0];
        mp.PrereqSkillId.Should().Be(1);
        mp.PrereqSkillName.Should().Be("Count to 1000");
        mp.PrereqNodeId.Should().Be(1);
        mp.RequiredAccuracy.Should().Be(80);
        mp.CurrentAccuracy.Should().Be(60.0);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 5 — Two prereqs, only 1 mastered → Locked, MissingPrerequisites.Count == 1
    // Graph: Node-1 → Node-3, Node-2 → Node-3
    //   Skill-1 mastered (90%), Skill-2 not mastered (40%)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TwoPrereqs_OneUnmet_LessonIsLocked_MissingCountIsOne()
    {
        // Arrange
        var skill1 = MakeSkill(id: 1, name: "Add Single-Digit",  masteryThreshold: 75);
        var skill2 = MakeSkill(id: 2, name: "Subtract Within 10", masteryThreshold: 75);
        var skill3 = MakeSkill(id: 3, name: "Add Two-Digit",     masteryThreshold: 80);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);
        var node3 = MakeNode(id: 3, skillId: 3);

        var edges = new[]
        {
            PrereqEdge(sourceNodeId: 1, targetNodeId: 3),
            PrereqEdge(sourceNodeId: 2, targetNodeId: 3)
        };

        var lesson3 = MakeLesson(id: 3, skillId: 3);

        var mastery = new Dictionary<int, SkillMastery>
        {
            [1] = Mastery(skillId: 1, accuracy: 90.0, totalAnswers: 8),  // mastered (90 >= 75)
            [2] = Mastery(skillId: 2, accuracy: 40.0, totalAnswers: 5)   // NOT mastered (40 < 75)
        };

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2, node3 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson3 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2, [3] = skill3 });

        // Assert — Locked; only the unmet prereq is listed
        result[3].State.Should().Be(NodeState.Locked);
        result[3].MissingPrerequisites.Should().HaveCount(1);
        result[3].MissingPrerequisites[0].PrereqSkillId.Should().Be(2);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 6 — Two prereqs, both mastered → Available
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TwoPrereqs_BothMastered_LessonIsAvailable()
    {
        // Arrange
        var skill1 = MakeSkill(id: 1, name: "Add Single-Digit",  masteryThreshold: 75);
        var skill2 = MakeSkill(id: 2, name: "Subtract Within 10", masteryThreshold: 75);
        var skill3 = MakeSkill(id: 3, name: "Add Two-Digit",     masteryThreshold: 80);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);
        var node3 = MakeNode(id: 3, skillId: 3);

        var edges = new[]
        {
            PrereqEdge(sourceNodeId: 1, targetNodeId: 3),
            PrereqEdge(sourceNodeId: 2, targetNodeId: 3)
        };

        var lesson3 = MakeLesson(id: 3, skillId: 3);

        var mastery = new Dictionary<int, SkillMastery>
        {
            [1] = Mastery(skillId: 1, accuracy: 90.0, totalAnswers: 8),  // mastered
            [2] = Mastery(skillId: 2, accuracy: 80.0, totalAnswers: 5)   // mastered (80 >= 75)
        };

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2, node3 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson3 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2, [3] = skill3 });

        // Assert
        result[3].State.Should().Be(NodeState.Available);
        result[3].MissingPrerequisites.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 7 — Zero answers (TotalAnswers == 0) → not mastered → Locked (Q2 guard)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ZeroAnswers_LessonIsLocked()
    {
        // Arrange
        var skill1 = MakeSkill(id: 1, name: "Count to 1000",  masteryThreshold: 70);
        var skill2 = MakeSkill(id: 2, name: "Compare Numbers", masteryThreshold: 70);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);

        var edges = new[] { PrereqEdge(sourceNodeId: 1, targetNodeId: 2) };

        var lesson2 = MakeLesson(id: 2, skillId: 2);

        // TotalAnswers == 0 → must NOT be treated as mastered even if threshold is 0
        var mastery = new Dictionary<int, SkillMastery>
        {
            [1] = Mastery(skillId: 1, accuracy: 0.0, totalAnswers: 0)
        };

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson2 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2 });

        // Assert
        result[2].State.Should().Be(NodeState.Locked);
        result[2].MissingPrerequisites.Should().HaveCount(1);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 8 — Lesson in completedLessonIds → Completed regardless of mastery (Q5)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CompletedLesson_IsCompleted_RegardlessOfMastery()
    {
        // Arrange — prereq is NOT mastered; but the lesson itself is completed
        var skill1 = MakeSkill(id: 1, name: "Count to 1000",  masteryThreshold: 80);
        var skill2 = MakeSkill(id: 2, name: "Compare Numbers", masteryThreshold: 80);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);

        var edges = new[] { PrereqEdge(sourceNodeId: 1, targetNodeId: 2) };

        var lesson2 = MakeLesson(id: 2, skillId: 2);

        // No answers at all → prereq would normally lock lesson 2
        var mastery = new Dictionary<int, SkillMastery>();

        // But lesson 2 is in the completed set
        var completed = new HashSet<int> { 2 };

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: completed,
            lessons: new[] { lesson2 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2 });

        // Assert — Completed state wins; no missing prerequisites
        result[2].State.Should().Be(NodeState.Completed);
        result[2].MissingPrerequisites.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 9 — Cycle defense: A→B→A (cycle) — engine must return without
    //          infinite-looping. Neither exception nor stack overflow. (Q10)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CycleDefense_EngineDoesNotInfiniteLoop()
    {
        // Arrange — construct a deliberate A(Node-1, Skill-1) ↔ B(Node-2, Skill-2) cycle
        var skill1 = MakeSkill(id: 1, name: "Skill A", masteryThreshold: 80);
        var skill2 = MakeSkill(id: 2, name: "Skill B", masteryThreshold: 80);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);

        // Mutual prerequisite cycle: Node-1 → Node-2 AND Node-2 → Node-1
        var edges = new[]
        {
            PrereqEdge(sourceNodeId: 1, targetNodeId: 2),
            PrereqEdge(sourceNodeId: 2, targetNodeId: 1)
        };

        var lesson1 = MakeLesson(id: 1, skillId: 1);
        var lesson2 = MakeLesson(id: 2, skillId: 2);

        var mastery = new Dictionary<int, SkillMastery>();

        // Act — must complete in finite time (no infinite loop, no StackOverflowException)
        var act = () => LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson1, lesson2 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2 });

        act.Should().NotThrow("cycle defense must prevent infinite loops");

        var result = act();
        result.Should().ContainKey(1);
        result.Should().ContainKey(2);
        // In a cycle both nodes are unmastered (sentinel = false) → both lessons Locked
        result[1].State.Should().Be(NodeState.Locked);
        result[2].State.Should().Be(NodeState.Locked);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 10 — Mastery just below threshold (79.99 < 80) → NOT mastered → Locked
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MasteryJustBelowThreshold_LessonIsLocked()
    {
        // Arrange
        var skill1 = MakeSkill(id: 1, name: "Count to 1000",  masteryThreshold: 80);
        var skill2 = MakeSkill(id: 2, name: "Compare Numbers", masteryThreshold: 80);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);

        var edges = new[] { PrereqEdge(sourceNodeId: 1, targetNodeId: 2) };

        var lesson2 = MakeLesson(id: 2, skillId: 2);

        // 79.99 < 80 → NOT mastered (>= is the rule, not >)
        var mastery = new Dictionary<int, SkillMastery>
        {
            [1] = Mastery(skillId: 1, accuracy: 79.99, totalAnswers: 100)
        };

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson2 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2 });

        // Assert
        result[2].State.Should().Be(NodeState.Locked);
        result[2].MissingPrerequisites.Should().HaveCount(1);
        result[2].MissingPrerequisites[0].CurrentAccuracy.Should().Be(79.99);
        result[2].MissingPrerequisites[0].RequiredAccuracy.Should().Be(80);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 11 — Mastery at exact threshold (80.0 == 80, TotalAnswers=1) → mastered → Available
    //           (>= rule: boundary value must pass)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MasteryAtExactThreshold_LessonIsAvailable()
    {
        // Arrange
        var skill1 = MakeSkill(id: 1, name: "Count to 1000",  masteryThreshold: 80);
        var skill2 = MakeSkill(id: 2, name: "Compare Numbers", masteryThreshold: 80);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);

        var edges = new[] { PrereqEdge(sourceNodeId: 1, targetNodeId: 2) };

        var lesson2 = MakeLesson(id: 2, skillId: 2);

        // 80.0 >= 80 AND TotalAnswers == 1 → mastered
        var mastery = new Dictionary<int, SkillMastery>
        {
            [1] = Mastery(skillId: 1, accuracy: 80.0, totalAnswers: 1)
        };

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson2 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2 });

        // Assert
        result[2].State.Should().Be(NodeState.Available);
        result[2].MissingPrerequisites.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Case 12 — Related edges are ignored: a Related-only "prereq" does NOT lock
    //           the lesson (engine must ignore non-Prerequisite edges entirely)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void RelatedEdgesOnly_LessonIsAvailable()
    {
        // Arrange — only a Related edge between Node-1 and Node-2; no Prerequisite edge
        var skill1 = MakeSkill(id: 1, name: "Count to 1000",  masteryThreshold: 80);
        var skill2 = MakeSkill(id: 2, name: "Compare Numbers", masteryThreshold: 80);

        var node1 = MakeNode(id: 1, skillId: 1);
        var node2 = MakeNode(id: 2, skillId: 2);

        // Related edge — must NOT participate in lock/unlock rules
        var edges = new[] { RelatedEdge(sourceNodeId: 1, targetNodeId: 2) };

        var lesson2 = MakeLesson(id: 2, skillId: 2);

        // Student has no mastery — but since there are no Prerequisite edges, lesson must be Available
        var mastery = new Dictionary<int, SkillMastery>();

        // Act
        var result = LearningPathEngine.ComputeStates(
            nodes: new[] { node1, node2 },
            edges: edges,
            masteryBySkillId: mastery,
            completedLessonIds: new HashSet<int>(),
            lessons: new[] { lesson2 },
            skillsById: new Dictionary<int, Skill> { [1] = skill1, [2] = skill2 });

        // Assert — no Prerequisite edges → lesson is Available (Q4 root rule)
        result[2].State.Should().Be(NodeState.Available);
        result[2].MissingPrerequisites.Should().BeEmpty();
    }
}
