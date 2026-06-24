using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Modules.Learning.Application.Options;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetRemediationPath;

/// <summary>
/// Handles <see cref="GetRemediationPathQuery"/> (BL-03-BE-5).
///
/// <para>Algorithm: transitive BFS upstream through Prerequisite edges.
/// Edge direction: <c>SourceNodeId → TargetNodeId</c> = "Source is prerequisite of Target".
/// So to find prereqs of nodeId, we follow edges where <c>TargetNodeId == nodeId</c>.
/// </para>
///
/// <para>Cycle guard: a seen-set tracks visited node ids; any back-edge is silently skipped
/// (prevents infinite loops on malformed graphs that bypassed the acyclicity insert guard).</para>
///
/// <para>Depth limit: <see cref="KnowledgeGraphOptions.RemediationMaxDepth"/> (default 3).</para>
///
/// <para>Security (mirrors GetPrerequisitesQueryHandler):
/// Nodes wrapping an inactive skill are filtered out — students cannot discover hidden skills.</para>
/// </summary>
public class GetRemediationPathQueryHandler
    : BaseResponseHandler, IQueryHandler<GetRemediationPathQuery, BaseResponse<List<RemediationPathDto>>>
{
    private readonly ILearningServiceManager _service;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly int _maxDepth;

    public GetRemediationPathQueryHandler(
        ILearningServiceManager service,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IOptions<KnowledgeGraphOptions> options)
    {
        _service   = service;
        _logger    = logger;
        _localizer = localizer;
        _maxDepth  = options.Value.RemediationMaxDepth;
    }

    public async Task<BaseResponse<List<RemediationPathDto>>> Handle(
        GetRemediationPathQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // 404 guard on the starting node.
            var startNode = await _service.KnowledgeGraphService.GetNodeForRemediationAsync(request.NodeId, cancellationToken);
            if (startNode is null)
                return NotFound<List<RemediationPathDto>>(_localizer[SharedResourcesKey.KnowledgeNodeNotFound]);

            // Load the full prerequisite edge list once (materialized; BFS runs in-memory).
            var allPrereqEdges = await _service.KnowledgeGraphService.GetPrerequisiteEdgesForRemediationAsync(cancellationToken);

            // Build adjacency: targetNodeId → list of (sourceNodeId, strength).
            var adjacency = BuildAdjacency(allPrereqEdges);

            // BFS upstream.
            var result = BfsUpstream(request.NodeId, adjacency, _maxDepth);

            if (result.Count == 0)
                return EmptyCollection(new List<RemediationPathDto>());

            // Security: collect unique SkillIds from all result node ids so we can filter
            // inactive-skill nodes.  We need the node→skillId map — load nodes by ids.
            var nodeIds = result.Select(r => r.nodeId).Distinct().ToList();
            var prereqNodes = await LoadNodesAsync(nodeIds, allPrereqEdges, cancellationToken);

            var skillIds = prereqNodes.Values
                .Where(n => n.SkillId.HasValue)
                .Select(n => n.SkillId!.Value)
                .Distinct()
                .ToList();

            var activeSkillIds = await _service.KnowledgeGraphService.GetActiveSkillIdsAsync(skillIds, cancellationToken);

            // Filter inactive and build DTOs ordered by depth.
            var dtos = result
                .Where(r =>
                {
                    if (!prereqNodes.TryGetValue(r.nodeId, out var node))
                        return false; // node was not loadable — skip
                    // If node wraps a skill, it must be active.
                    return !node.SkillId.HasValue || activeSkillIds.Contains(node.SkillId.Value);
                })
                .OrderBy(r => r.depth)
                .Select(r =>
                {
                    var node = prereqNodes[r.nodeId];
                    return new RemediationPathDto(r.nodeId, node.Name, r.depth, r.strength);
                })
                .ToList();

            var response = Success(dtos);
            response.Message = _localizer[SharedResourcesKey.KnowledgeGraphRemediationPathRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetRemediationPathQuery");
            return ServerError<List<RemediationPathDto>>();
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────────────────────────

    /// <summary>Builds adjacency map: targetNodeId → list of (sourceNodeId, edgeStrength).</summary>
    private static Dictionary<int, List<(int sourceId, decimal strength)>> BuildAdjacency(
        List<KnowledgeEdge> edges)
    {
        var adj = new Dictionary<int, List<(int, decimal)>>();
        foreach (var edge in edges)
        {
            if (!adj.TryGetValue(edge.TargetNodeId, out var list))
            {
                list = new List<(int, decimal)>();
                adj[edge.TargetNodeId] = list;
            }
            list.Add((edge.SourceNodeId, edge.Strength));
        }
        return adj;
    }

    /// <summary>
    /// BFS upstream from startNodeId up to maxDepth levels.
    /// Returns a list of (nodeId, depth, strength) tuples — one entry per visited node.
    /// Cycle-guarded via a visited-node set.
    /// </summary>
    private static List<(int nodeId, int depth, decimal strength)> BfsUpstream(
        int startNodeId,
        Dictionary<int, List<(int sourceId, decimal strength)>> adjacency,
        int maxDepth)
    {
        var visited = new HashSet<int> { startNodeId };
        var result  = new List<(int nodeId, int depth, decimal strength)>();

        // Queue entries: (nodeId, currentDepth).
        var queue = new Queue<(int nodeId, int depth)>();
        queue.Enqueue((startNodeId, 0));

        while (queue.Count > 0)
        {
            var (currentNodeId, currentDepth) = queue.Dequeue();

            if (currentDepth >= maxDepth)
                continue; // Do not traverse deeper.

            if (!adjacency.TryGetValue(currentNodeId, out var prereqs))
                continue; // No prerequisites for this node.

            foreach (var (sourceId, strength) in prereqs)
            {
                if (visited.Contains(sourceId))
                    continue; // Cycle guard / already added.

                visited.Add(sourceId);
                var depth = currentDepth + 1;
                result.Add((sourceId, depth, strength));
                queue.Enqueue((sourceId, depth));
            }
        }

        return result;
    }

    /// <summary>
    /// Returns a dictionary of nodeId → KnowledgeNode for all node ids in the remediation result.
    /// Pulls from the already-materialized edge list's adjacency data where possible; loads
    /// missing nodes from the service (via GetNodeForRemediationAsync per id).
    /// </summary>
    private async Task<Dictionary<int, KnowledgeNode>> LoadNodesAsync(
        List<int> nodeIds,
        List<KnowledgeEdge> allEdges,
        CancellationToken ct)
    {
        // We need the actual KnowledgeNode rows (Name + SkillId). Load each.
        // The repo call is non-tracked and cached per this handler instance — cost is O(|result|) round-trips.
        // For the typical max-depth=3 case this is bounded to ≤7 nodes.
        var map = new Dictionary<int, KnowledgeNode>();
        foreach (var id in nodeIds)
        {
            var node = await _service.KnowledgeGraphService.GetNodeForRemediationAsync(id, ct);
            if (node is not null)
                map[id] = node;
        }
        return map;
    }
}
