namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Discriminates the four question types supported by the quiz engine. Stored as int via
/// HasConversion&lt;int&gt;() per the project no-free-text rule — referenced by member, never a magic string/int.
/// </summary>
public enum QuestionType
{
    MCQ = 1,
    TrueFalse = 2,
    Matching = 3,
    FillInBlank = 4,
}
