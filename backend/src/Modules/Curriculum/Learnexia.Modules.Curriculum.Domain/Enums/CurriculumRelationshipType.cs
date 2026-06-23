namespace Learnexia.Modules.Curriculum.Domain.Enums;

/// <summary>
/// Relationship type on a <see cref="Entities.KGSuggestion"/>.
/// Stored as int in the database.
///
/// Module isolation (BL-04 Q-B decision): this is a LOCAL mirror of
/// <c>learning.EdgeRelationshipType</c>. The curriculum module MUST NOT reference
/// the learning module's enum directly. Member values are intentionally aligned so
/// a plain int mapping works across modules.
/// </summary>
public enum CurriculumRelationshipType
{
    Prerequisite = 0,
    Related = 1,
}
