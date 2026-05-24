namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Indicates the source of a quiz question. Stored as int via HasConversion&lt;int&gt;() per the
/// project no-free-text rule — referenced by member, never a magic string/int.
/// AI generation lands in P3-06; Curated is the default path.
/// </summary>
public enum GeneratedBy
{
    Curated = 1,
    AI = 2,
}
