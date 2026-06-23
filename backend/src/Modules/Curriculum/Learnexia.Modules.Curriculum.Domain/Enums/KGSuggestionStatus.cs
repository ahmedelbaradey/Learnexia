namespace Learnexia.Modules.Curriculum.Domain.Enums;

/// <summary>
/// Review/approval status of a <see cref="Entities.KGSuggestion"/>.
/// Stored as int in the database.
/// </summary>
public enum KGSuggestionStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}
