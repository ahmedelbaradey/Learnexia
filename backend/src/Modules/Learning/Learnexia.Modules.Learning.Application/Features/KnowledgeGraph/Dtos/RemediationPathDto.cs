namespace Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;

/// <summary>
/// A single node in the remediation prerequisite chain (BL-03-BE-5).
///
/// <para>Returned by <c>GetRemediationPathQuery</c>, ordered by ascending <see cref="Depth"/>
/// so the student sees the most fundamental prereq first (depth 1 = direct prereq,
/// depth 2 = prereq of prereq, etc.).</para>
///
/// <para>Strength is the edge weight for the edge that connects this node to its parent in the
/// BFS traversal (direct or transitive). Nil on the root chain entry.</para>
/// </summary>
public record RemediationPathDto(
    int     NodeId,
    string  Name,
    int     Depth,
    decimal Strength
);
