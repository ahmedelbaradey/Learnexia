using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Validates that a set of <see cref="KnowledgeEdge"/> records form an acyclic directed graph over
/// <see cref="EdgeRelationshipType.Prerequisite"/>-typed edges.
/// <see cref="EdgeRelationshipType.Related"/> edges are excluded from the acyclic constraint.
/// Throws <see cref="InvalidOperationException"/> with a clear message when a cycle is detected.
/// </summary>
public static class SkillGraphValidator
{
    /// <summary>
    /// Checks all prerequisite edges for cycles using a three-color DFS (White/Gray/Black).
    /// Call this method before persisting any new edge to ensure the graph remains acyclic.
    /// </summary>
    /// <param name="allEdges">
    /// All edges (existing and proposed) to validate. Non-Prerequisite edges are ignored.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a cycle is detected in the prerequisite edge subgraph, including self-loops.
    /// The message includes the offending source and target node IDs.
    /// </exception>
    public static void AssertAcyclic(IEnumerable<KnowledgeEdge> allEdges)
    {
        // Filter to Prerequisite-typed edges only; Related edges are excluded from the acyclic constraint.
        var prerequisiteEdges = allEdges
            .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite)
            .ToList();

        // Build adjacency list: sourceNodeId -> list of (targetNodeId, sourceNodeId_for_error_msg)
        var adjacency = new Dictionary<int, List<(int TargetId, int SourceId)>>();

        foreach (var edge in prerequisiteEdges)
        {
            if (!adjacency.ContainsKey(edge.SourceNodeId))
                adjacency[edge.SourceNodeId] = new List<(int, int)>();

            adjacency[edge.SourceNodeId].Add((edge.TargetNodeId, edge.SourceNodeId));

            // Ensure target node is present in the graph (even with no outgoing edges)
            if (!adjacency.ContainsKey(edge.TargetNodeId))
                adjacency[edge.TargetNodeId] = new List<(int, int)>();
        }

        // Three-color DFS state: 0 = White (unvisited), 1 = Gray (in-stack), 2 = Black (done)
        var color = new Dictionary<int, int>();
        foreach (var nodeId in adjacency.Keys)
            color[nodeId] = 0;

        foreach (var nodeId in adjacency.Keys)
        {
            if (color[nodeId] == 0)
                DfsVisit(nodeId, adjacency, color);
        }
    }

    /// <summary>
    /// Recursively visits a node in the DFS traversal using three-color marking.
    /// Gray nodes represent nodes currently on the recursion stack (cycle detection).
    /// </summary>
    private static void DfsVisit(
        int nodeId,
        Dictionary<int, List<(int TargetId, int SourceId)>> adjacency,
        Dictionary<int, int> color)
    {
        // Mark as Gray (currently being explored / on recursion stack)
        color[nodeId] = 1;

        if (adjacency.TryGetValue(nodeId, out var neighbors))
        {
            foreach (var (targetId, sourceId) in neighbors)
            {
                if (!color.TryGetValue(targetId, out var targetColor))
                    targetColor = 0;

                if (targetColor == 1)
                {
                    // Target is Gray → back edge → cycle detected
                    throw new InvalidOperationException(
                        $"Cycle detected in prerequisite graph. Edge {sourceId} → {targetId} creates a cycle.");
                }

                if (targetColor == 0)
                {
                    DfsVisit(targetId, adjacency, color);
                }
                // targetColor == 2 (Black) → already fully explored, no cycle through this path
            }
        }

        // Mark as Black (fully explored)
        color[nodeId] = 2;
    }
}
