namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Describes the relationship type on a <see cref="Entities.KnowledgeEdge"/>.
/// Only <see cref="Prerequisite"/> edges are included in acyclicity checks.
/// Stored as int in the database.
/// </summary>
public enum EdgeRelationshipType
{
    Prerequisite = 0,
    Related = 1
}
