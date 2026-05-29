using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="SkillGraphValidator.AssertAcyclic"/>.
/// Tests construct <see cref="KnowledgeEdge"/> instances directly via object initializers
/// (no DbContext required). The four required cases are:
/// 1. Acyclic chain A→B→C   — must NOT throw.
/// 2. Single cycle A→B→C→A  — must throw <see cref="InvalidOperationException"/>.
/// 3. Self-loop A→A          — must throw <see cref="InvalidOperationException"/>.
/// 4. Related-only "cycle"   — must NOT throw (Related edges are excluded from the constraint).
/// </summary>
public sealed class SkillGraphValidatorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Prerequisite-typed <see cref="KnowledgeEdge"/> directly via object initializer.
    /// </summary>
    private static KnowledgeEdge Prerequisite(int sourceId, int targetId) =>
        new()
        {
            SourceNodeId = sourceId,
            TargetNodeId = targetId,
            RelationshipType = EdgeRelationshipType.Prerequisite,
            Strength = 1.0m
        };

    /// <summary>
    /// Creates a Related-typed <see cref="KnowledgeEdge"/> directly via object initializer.
    /// </summary>
    private static KnowledgeEdge Related(int sourceId, int targetId) =>
        new()
        {
            SourceNodeId = sourceId,
            TargetNodeId = targetId,
            RelationshipType = EdgeRelationshipType.Related,
            Strength = 1.0m
        };

    // ══════════════════════════════════════════════════════════════════════════════
    // Case 1 — Acyclic chain A→B→C must NOT throw
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AssertAcyclic_AcyclicChain_DoesNotThrow()
    {
        // A(1) → B(2) → C(3): perfectly acyclic directed chain.
        var edges = new[]
        {
            Prerequisite(sourceId: 1, targetId: 2),
            Prerequisite(sourceId: 2, targetId: 3)
        };

        var act = () => SkillGraphValidator.AssertAcyclic(edges);

        act.Should().NotThrow();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Case 2 — Single cycle A→B→C→A must throw InvalidOperationException
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AssertAcyclic_SingleCycle_ThrowsInvalidOperationException()
    {
        // A(1)→B(2)→C(3)→A(1): cycle back to the start.
        var edges = new[]
        {
            Prerequisite(sourceId: 1, targetId: 2),
            Prerequisite(sourceId: 2, targetId: 3),
            Prerequisite(sourceId: 3, targetId: 1)
        };

        var act = () => SkillGraphValidator.AssertAcyclic(edges);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cycle detected*");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Case 3 — Self-loop A→A must throw InvalidOperationException
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AssertAcyclic_SelfLoop_ThrowsInvalidOperationException()
    {
        // A(1)→A(1): trivial self-loop.
        var edges = new[]
        {
            Prerequisite(sourceId: 1, targetId: 1)
        };

        var act = () => SkillGraphValidator.AssertAcyclic(edges);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cycle detected*");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Case 4 — Related-only "cycle" R-A→R-B→R-A must NOT throw
    //          Related edges are excluded from the acyclic constraint.
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AssertAcyclic_RelatedOnlyCycle_DoesNotThrow()
    {
        // RA(10)→RB(11)→RA(10): a "cycle" formed entirely by Related edges.
        // The validator must ignore these edges; no exception should be raised.
        var edges = new[]
        {
            Related(sourceId: 10, targetId: 11),
            Related(sourceId: 11, targetId: 10)
        };

        var act = () => SkillGraphValidator.AssertAcyclic(edges);

        act.Should().NotThrow();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Bonus — Empty edge set must NOT throw
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AssertAcyclic_EmptyEdgeSet_DoesNotThrow()
    {
        var act = () => SkillGraphValidator.AssertAcyclic(Array.Empty<KnowledgeEdge>());

        act.Should().NotThrow();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Bonus — Mixed Related and Prerequisite where only Prerequisite forms a cycle
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AssertAcyclic_MixedEdges_PrerequisiteCycleStillThrows()
    {
        // Related "cycle" between nodes 10↔11 is present but must be ignored.
        // Prerequisite cycle 1→2→3→1 must still be caught.
        var edges = new[]
        {
            Related(sourceId: 10, targetId: 11),
            Related(sourceId: 11, targetId: 10),
            Prerequisite(sourceId: 1, targetId: 2),
            Prerequisite(sourceId: 2, targetId: 3),
            Prerequisite(sourceId: 3, targetId: 1)
        };

        var act = () => SkillGraphValidator.AssertAcyclic(edges);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cycle detected*");
    }
}
