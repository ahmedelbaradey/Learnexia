using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Logging;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IKnowledgeEdgeWriter"/> inside the Learning module's Infrastructure layer.
/// Called by the Curriculum approve handler (via the Shared.Contracts seam) to publish a single
/// approved KGSuggestion as a KnowledgeEdge, without any direct project reference from Curriculum.
///
/// <para><strong>Decision E chokepoint (BL-03):</strong> This is the SINGLE write path for new
/// KnowledgeEdge rows produced outside the Learning module. All four guards from
/// <see cref="Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Commands.AddEdge.AddKnowledgeEdgeCommandHandler"/>
/// are enforced here in the same order:
/// <list type="number">
///   <item>Node existence check (both source and target).</item>
///   <item>Cross-language guard (both nodes' Subjects must share the same Language).</item>
///   <item>Duplicate guard ((SourceNodeId, TargetNodeId, RelationshipType) must be unique).</item>
///   <item>Acyclic guard (Prerequisite edges only — <see cref="SkillGraphValidator.AssertAcyclic"/>).</item>
/// </list>
/// </para>
///
/// <para><strong>KNOWN RACE:</strong> same non-atomic check-then-insert as AddKnowledgeEdgeCommandHandler
/// (the accepted P7-03 Finding #3 race — single-admin-at-a-time phase). Not introduced here.</para>
///
/// <para>BL-03 seam-impl.</para>
/// </summary>
public sealed class KnowledgeEdgeWriterAdapter : IKnowledgeEdgeWriter
{
    private readonly LearningDbContext _db;
    private readonly ILoggerManager _logger;

    private const int SystemUserId = 0;

    public KnowledgeEdgeWriterAdapter(
        LearningDbContext db,
        ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EdgePublishResult> PublishApprovedEdgeAsync(
        int sourceNodeId,
        int targetNodeId,
        int relationshipType,
        decimal strength,
        CancellationToken ct = default)
    {
        // ── Guard 1: both nodes must exist ────────────────────────────────────────────────────────
        var sourceNode = await _db.KnowledgeNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == sourceNodeId, ct);

        if (sourceNode is null)
        {
            _logger.LogInfo(
                $"KnowledgeEdgeWriterAdapter: sourceNodeId={sourceNodeId} not found; publish rejected.");
            // Treat missing node as a CrossLanguage-style domain error (422) to caller.
            // Use CrossLanguage=false, Cycle=false; but we need a distinguishable outcome.
            // The brief says "cross-language → 422"; missing node is also a 422-class domain error.
            // Return CrossLanguage=true as the least-bad flag for now (the caller already validates
            // node ids exist when approving a KGSuggestion, so this path is defensive only).
            return new EdgePublishResult(false, false, false, true, null);
        }

        var targetNode = await _db.KnowledgeNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == targetNodeId, ct);

        if (targetNode is null)
        {
            _logger.LogInfo(
                $"KnowledgeEdgeWriterAdapter: targetNodeId={targetNodeId} not found; publish rejected.");
            return new EdgePublishResult(false, false, false, true, null);
        }

        // ── Guard 2: cross-language check ─────────────────────────────────────────────────────────
        var sourceSubject = await _db.Subjects
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sourceNode.SubjectId, ct);

        var targetSubject = await _db.Subjects
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == targetNode.SubjectId, ct);

        if (sourceSubject is null || targetSubject is null)
        {
            _logger.LogInfo(
                $"KnowledgeEdgeWriterAdapter: subject not resolvable for sourceNodeId={sourceNodeId} " +
                $"or targetNodeId={targetNodeId}; publish rejected (cross-language fail-closed).");
            return new EdgePublishResult(false, false, false, true, null);
        }

        if (sourceSubject.Language != targetSubject.Language)
        {
            _logger.LogInfo(
                $"KnowledgeEdgeWriterAdapter: cross-language edge rejected " +
                $"sourceNodeId={sourceNodeId} (lang={sourceSubject.Language}) " +
                $"targetNodeId={targetNodeId} (lang={targetSubject.Language}).");
            return new EdgePublishResult(false, false, false, true, null);
        }

        // ── Guard 3: duplicate check ───────────────────────────────────────────────────────────────
        var isDuplicate = await _db.KnowledgeEdges
            .AnyAsync(e =>
                e.SourceNodeId == sourceNodeId &&
                e.TargetNodeId == targetNodeId &&
                (int)e.RelationshipType == relationshipType,
                ct);

        if (isDuplicate)
        {
            _logger.LogInfo(
                $"KnowledgeEdgeWriterAdapter: duplicate edge sourceNodeId={sourceNodeId} " +
                $"targetNodeId={targetNodeId} relationshipType={relationshipType}; treating as no-op.");
            return new EdgePublishResult(false, true, false, false, null);
        }

        // ── Guard 4: acyclic check for Prerequisite edges ─────────────────────────────────────────
        var edgeRelType = (EdgeRelationshipType)relationshipType;

        if (edgeRelType == EdgeRelationshipType.Prerequisite)
        {
            var existingPrereqEdges = await _db.KnowledgeEdges
                .AsNoTracking()
                .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite)
                .ToListAsync(ct);

            var proposedEdge = new KnowledgeEdge
            {
                SourceNodeId     = sourceNodeId,
                TargetNodeId     = targetNodeId,
                RelationshipType = EdgeRelationshipType.Prerequisite,
                Strength         = strength,
            };

            var allEdgesForCheck = existingPrereqEdges.Append(proposedEdge);

            try
            {
                SkillGraphValidator.AssertAcyclic(allEdgesForCheck);
            }
            catch (InvalidOperationException cycleEx)
            {
                _logger.LogInfo(
                    $"KnowledgeEdgeWriterAdapter: acyclic guard rejected edge " +
                    $"sourceNodeId={sourceNodeId} targetNodeId={targetNodeId}. " +
                    $"Cycle detail: {cycleEx.Message}");
                return new EdgePublishResult(false, false, true, false, null);
            }
        }

        // ── Insert the new edge ───────────────────────────────────────────────────────────────────
        var edge = new KnowledgeEdge
        {
            SourceNodeId     = sourceNodeId,
            TargetNodeId     = targetNodeId,
            RelationshipType = edgeRelType,
            Strength         = strength,
        };

        _db.KnowledgeEdges.Add(edge);
        await _db.SaveChangesAsync(SystemUserId);

        _logger.LogInfo(
            $"KnowledgeEdgeWriterAdapter: published KnowledgeEdge id={edge.Id} " +
            $"sourceNodeId={sourceNodeId} targetNodeId={targetNodeId} " +
            $"relationshipType={edgeRelType} strength={strength}.");

        return new EdgePublishResult(true, false, false, false, edge.Id);
    }
}
