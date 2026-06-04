namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// The language of a curriculum content tree rooted at a <see cref="Entities.Subject"/>.
/// Stored as int (<c>HasConversion&lt;int&gt;</c>) per the project no-free-text rule.
/// Used together with <see cref="SubjectCode"/> to form the unique natural key
/// <c>(GradeId, SubjectCode, Language)</c> for each language-specific subject tree.
/// Language is carried only on <see cref="Entities.Subject"/>; child entities (Unit, Lesson,
/// Concept, Skill, QuizQuestion) inherit it via their parent chain.
/// </summary>
public enum ContentLanguage
{
    Ar = 0,
    En = 1,
}
