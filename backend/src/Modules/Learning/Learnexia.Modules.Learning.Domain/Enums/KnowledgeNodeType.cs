namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Classifies the type of a <see cref="Entities.KnowledgeNode"/> in the skill dependency graph.
/// Stored as int in the database.
/// </summary>
public enum KnowledgeNodeType
{
    Skill = 0,
    Concept = 1,
    Review = 2
}
