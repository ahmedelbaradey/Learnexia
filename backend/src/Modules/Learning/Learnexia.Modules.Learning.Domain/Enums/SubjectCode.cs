namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Stable code that identifies what curriculum subject a <see cref="Entities.Subject"/> represents.
/// Stored as int (<c>HasConversion&lt;int&gt;</c>) per the project no-free-text rule.
/// Used together with <see cref="ContentLanguage"/> to form the unique natural key
/// <c>(GradeId, SubjectCode, Language)</c> for each language-specific subject tree.
/// Values are explicitly assigned — never derived from <c>Name</c>.
/// </summary>
public enum SubjectCode
{
    MATH = 0,
    SCIENCE = 1,
    ARABIC = 2,
    ENGLISH = 3,
}
