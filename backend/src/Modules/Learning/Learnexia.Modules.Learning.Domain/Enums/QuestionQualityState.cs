namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// AI/empirical quality state of a <c>QuizQuestion</c> (P5-07 BE-3).
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> — never a magic string/int.
///
/// Non-zero start (members begin at 1) — avoids the enum-0 default trap.
///
/// DB column defaults to 1 (<see cref="Approved"/>) so ALL existing rows are backfilled to
/// Approved, keeping current questions servable. The C# property initializer on
/// <c>QuizQuestion.QualityState</c> also defaults to <see cref="Approved"/> — new inserts
/// start Approved unless the calibration job later flags them.
///
/// Members:
/// - <see cref="Approved"/>          = 1 — question is eligible for auto-serve (default / backfill value).
/// - <see cref="FlaggedForReview"/>  = 2 — empirical signals suggest poor quality;
///                                         excluded from the auto-serve candidate pool
///                                         (<c>StartAttemptService.GetPublishedActiveQuestionsAsync</c>).
///                                         Reversible via the admin Clear-flag endpoint (BE-5).
/// </summary>
public enum QuestionQualityState
{
    /// <summary>Question is eligible for auto-serve. All existing rows backfill to this value (DB DEFAULT 1).</summary>
    Approved = 1,

    /// <summary>Question has been flagged by the calibration job due to extreme empirical p-value. Excluded from auto-serve.</summary>
    FlaggedForReview = 2,
}
